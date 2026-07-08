using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.Filter2;
using AviUtlPluginNet.Core.Interop.Logger2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class FilterPluginE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public FilterPluginE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();
        PluginFixture.BuildTestPlugin(GetFilterPluginImplementation());
    }

    private static string GetFilterPluginImplementation()
    {
        return """
        using System;
        using AviUtlPluginNet.Abstractions;
        using AviUtlPluginNet.Core.Interop.Filter2;

        namespace AviUtlPluginNet.Example;

        [AviUtl2Plugin]
        partial class TestFilterPlugin : IFilterVideo, IFilterAudio, IUseLogger
        {
            public static string Name => ".NET Filter Plugin";
            public static string Information => ".NET Filter Plugin Example";
            public static string? Label => "テストラベル";

            private ILogger2? _logger;

            public void AttachLogger(ILogger2 logger) => _logger = logger;

            // 設定項目: 属性で宣言し、値はpartialプロパティで常に最新値が読める
            [FilterGroup("画像")]
            [FilterTrack("明るさ", Default = 1.0, Min = 0.0, Max = 2.0, Step = 0.01)]
            public partial double Luminance { get; }

            [FilterCheck("反転", Default = false)]
            public partial bool Invert { get; }

            [FilterSelect("対象", Default = 7)]
            [FilterSelectItem("R成分のみ", 1)]
            [FilterSelectItem("RGB成分", 7)]
            public partial int Component { get; }

            [FilterButton("リセット")]
            public void OnReset()
            {
                _logger?.Info("reset button pressed");
            }

            public bool ProcVideo(FilterVideoContext context)
            {
                int w = context.Width;
                int h = context.Height;
                var pixels = context.GetImageData();

                for (int i = 0; i < pixels.Length; i++)
                {
                    var r = (double)pixels[i].r;
                    if ((Component & 1) != 0)
                    {
                        r = Math.Clamp(r * Luminance, 0.0, 255.0);
                    }
                    if (Invert)
                    {
                        r = 255.0 - r;
                    }
                    pixels[i].r = (byte)r;
                }

                context.SetImageData(pixels, w, h);
                return true;
            }

            public bool ProcAudio(FilterAudioContext context)
            {
                var samples = new float[context.SampleCount];
                for (int channel = 0; channel < context.ChannelCount; channel++)
                {
                    context.GetSampleData(samples, channel);
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] *= 0.5f;
                    }
                    context.SetSampleData(samples, channel);
                }
                return true;
            }
        }
        """;
    }

    public void Dispose()
    {
        PluginFixture.Dispose();
    }
}

/// <summary>
/// フィルタプラグインのNativeAOT E2Eテスト
/// ホスト側のFILTER_PROC_VIDEO/AUDIOをテストコード側で模擬し、
/// 属性宣言→ネイティブitem構造体→partialプロパティのライブバインディングを検証する
/// </summary>
public class FilterPluginE2ETests : IClassFixture<FilterPluginE2ETestsFixture>, IDisposable
{
    private const int Width = 4;
    private const int Height = 2;
    private const int PixelCount = Width * Height;
    private const int AudioSampleCount = 4;

    private readonly NativePluginLibrary _library;
    private unsafe FILTER_PLUGIN_TABLE* _pluginTable = null;

    // ホスト側の状態(UnmanagedCallersOnlyから参照するためstatic)
    private static IntPtr _videoSrcBuffer;
    private static IntPtr _videoDstBuffer;
    private static int _setImageWidth, _setImageHeight;
    private static IntPtr _audioSrcBuffer;
    private static IntPtr _audioDstBuffer;
    private static readonly List<string> _logs = new();

    public FilterPluginE2ETests(FilterPluginE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);

        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetFilterPluginTable");
            _pluginTable = (FILTER_PLUGIN_TABLE*)getTable();
        }
    }

    /// <summary>
    /// GetFilterPluginTable エントリーポイントのテスト
    /// 属性で宣言した設定項目が宣言順のnull終端リストとして構築されることを検証する
    /// </summary>
    [Fact]
    public void GetFilterPluginTable_ShouldBuildDeclaredItems()
    {
        unsafe
        {
            var table = _pluginTable;
            Assert.False(table == null);

            // IFilterVideo + IFilterAudio → flag は Video | Audio
            Assert.Equal(FilterPluginTableFlag.Video | FilterPluginTableFlag.Audio, table->flag);
            Assert.Equal(".NET Filter Plugin", Marshal.PtrToStringUni(table->name));
            Assert.Equal("テストラベル", Marshal.PtrToStringUni(table->label));
            Assert.Equal(".NET Filter Plugin Example", Marshal.PtrToStringUni(table->information));
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_proc_video);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_proc_audio);

            // 設定項目: [FilterGroup] → track2 → check → select → button の宣言順
            var types = new List<string>();
            for (var p = table->items; *p != null; p++)
            {
                // 全FILTER_ITEM_XXXの先頭フィールドはtype(LPCWSTR)
                types.Add(Marshal.PtrToStringUni(*(IntPtr*)*p)!);
            }
            Assert.Equal(new[] { "group", "track2", "check", "select", "button" }, types);

            // トラックバー項目の内容検証
            var track = (FILTER_ITEM_TRACK*)table->items[1];
            Assert.Equal("明るさ", Marshal.PtrToStringUni(track->name));
            Assert.Equal(1.0, track->value);
            Assert.Equal(0.0, track->s);
            Assert.Equal(2.0, track->e);
            Assert.Equal(0.01, track->step);

            // 選択リストの内容検証 (null終端)
            var select = (FILTER_ITEM_SELECT*)table->items[3];
            Assert.Equal(7, select->value);
            Assert.Equal("R成分のみ", Marshal.PtrToStringUni(select->list[0].name));
            Assert.Equal(1, select->list[0].value);
            Assert.Equal("RGB成分", Marshal.PtrToStringUni(select->list[1].name));
            Assert.Equal(7, select->list[1].value);
            Assert.Equal(IntPtr.Zero, select->list[2].name);
        }
    }

    /// <summary>
    /// func_proc_video のE2Eテスト
    /// ホストがitem構造体のvalueを直接更新 → partialプロパティ経由で最新値がフィルタ処理に反映されることを検証する
    /// </summary>
    [Fact]
    public void FuncProcVideo_ShouldUseLiveItemValues()
    {
        _videoSrcBuffer = Marshal.AllocHGlobal(PixelCount * 4);
        _videoDstBuffer = Marshal.AllocHGlobal(PixelCount * 4);
        try
        {
            unsafe
            {
                // 入力画像: r=100, g=50, b=25, a=255
                var src = (PIXEL_RGBA*)_videoSrcBuffer;
                for (int i = 0; i < PixelCount; i++)
                {
                    src[i] = new PIXEL_RGBA { r = 100, g = 50, b = 25, a = 255 };
                }

                // ホストとして「明るさ」の現在値を2.0、「反転」をtrueに更新する
                // ※テーブルはdylib内のシングルトンのため終了時に元へ戻す
                var track = (FILTER_ITEM_TRACK*)_pluginTable->items[1];
                var check = (FILTER_ITEM_CHECK*)_pluginTable->items[2];
                var originalTrackValue = track->value;
                var originalCheckValue = check->value;
                track->value = 2.0;
                check->value = 1;

                try
                {
                    var scene = new SCENE_INFO { width = 1920, height = 1080, rate = 30, scale = 1, sample_rate = 48000 };
                    var objectInfo = new OBJECT_INFO { width = Width, height = Height };
                    var param = new OBJECT_IMAGE_PARAM { alpha = 1.0f };

                    var proc = new FILTER_PROC_VIDEO
                    {
                        scene = &scene,
                        @object = &objectInfo,
                        get_image_data = &HostGetImageData,
                        set_image_data = &HostSetImageData,
                        param = &param,
                    };

                    var result = _pluginTable->func_proc_video((IntPtr)(&proc));
                    Assert.True(result);

                    // r=100 → ×2.0=200 → 反転で55、g/bは変更なし
                    Assert.Equal(Width, _setImageWidth);
                    Assert.Equal(Height, _setImageHeight);
                    var dst = (PIXEL_RGBA*)_videoDstBuffer;
                    for (int i = 0; i < PixelCount; i++)
                    {
                        Assert.Equal(55, dst[i].r);
                        Assert.Equal(50, dst[i].g);
                        Assert.Equal(25, dst[i].b);
                        Assert.Equal(255, dst[i].a);
                    }
                }
                finally
                {
                    track->value = originalTrackValue;
                    check->value = originalCheckValue;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(_videoSrcBuffer);
            Marshal.FreeHGlobal(_videoDstBuffer);
            _videoSrcBuffer = IntPtr.Zero;
            _videoDstBuffer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// func_proc_audio のE2Eテスト
    /// </summary>
    [Fact]
    public void FuncProcAudio_ShouldProcessSamples()
    {
        _audioSrcBuffer = Marshal.AllocHGlobal(AudioSampleCount * sizeof(float));
        _audioDstBuffer = Marshal.AllocHGlobal(AudioSampleCount * sizeof(float));
        try
        {
            unsafe
            {
                var src = (float*)_audioSrcBuffer;
                for (int i = 0; i < AudioSampleCount; i++)
                {
                    src[i] = 0.8f;
                }

                var scene = new SCENE_INFO { sample_rate = 48000 };
                var objectInfo = new OBJECT_INFO { sample_num = AudioSampleCount, channel_num = 1 };
                var param = new OBJECT_AUDIO_PARAM { vol_l = 1.0f, vol_r = 1.0f };

                var proc = new FILTER_PROC_AUDIO
                {
                    scene = &scene,
                    @object = &objectInfo,
                    get_sample_data = &HostGetSampleData,
                    set_sample_data = &HostSetSampleData,
                    param = &param,
                };

                var result = _pluginTable->func_proc_audio((IntPtr)(&proc));
                Assert.True(result);

                var dst = (float*)_audioDstBuffer;
                for (int i = 0; i < AudioSampleCount; i++)
                {
                    Assert.Equal(0.4f, dst[i], 3);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(_audioSrcBuffer);
            Marshal.FreeHGlobal(_audioDstBuffer);
            _audioSrcBuffer = IntPtr.Zero;
            _audioDstBuffer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// [FilterButton] のコールバックがプラグインのメソッドへ到達することを検証する
    /// </summary>
    [Fact]
    public void FilterButton_ShouldInvokePluginMethod()
    {
        _logs.Clear();

        unsafe
        {
            var logHandle = (LOG_HANDLE*)Marshal.AllocHGlobal(sizeof(LOG_HANDLE));
            try
            {
                logHandle->log = &CaptureLog;
                logHandle->info = &CaptureLog;
                logHandle->warn = &CaptureLog;
                logHandle->error = &CaptureLog;
                logHandle->verbose = &CaptureLog;
                var initializeLogger = (delegate* unmanaged[Stdcall]<IntPtr, void>)_library.GetExport("InitializeLogger");
                initializeLogger((IntPtr)logHandle);

                // items[4] = button のコールバックをホストから呼び出す(ボタン押下の模擬)
                var button = (FILTER_ITEM_BUTTON*)_pluginTable->items[4];
                Assert.Equal("リセット", Marshal.PtrToStringUni(button->name));
                button->callback(IntPtr.Zero);

                Assert.Contains(_logs, m => m.Contains("reset button pressed"));
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)logHandle);
            }
        }
    }

    #region ホスト側コールバック(FILTER_PROC_VIDEO/AUDIO/LOG_HANDLEの関数ポインタ)

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostGetImageData(PIXEL_RGBA* buffer)
    {
        new Span<PIXEL_RGBA>((void*)_videoSrcBuffer, PixelCount).CopyTo(new Span<PIXEL_RGBA>(buffer, PixelCount));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostSetImageData(PIXEL_RGBA* buffer, int width, int height)
    {
        _setImageWidth = width;
        _setImageHeight = height;
        if (buffer != null)
        {
            new Span<PIXEL_RGBA>(buffer, width * height).CopyTo(new Span<PIXEL_RGBA>((void*)_videoDstBuffer, PixelCount));
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostGetSampleData(float* buffer, int channel)
    {
        new Span<float>((void*)_audioSrcBuffer, AudioSampleCount).CopyTo(new Span<float>(buffer, AudioSampleCount));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostSetSampleData(float* buffer, int channel)
    {
        new Span<float>(buffer, AudioSampleCount).CopyTo(new Span<float>((void*)_audioDstBuffer, AudioSampleCount));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void CaptureLog(LOG_HANDLE* handle, IntPtr message)
    {
        var text = Marshal.PtrToStringUni(message);
        if (text != null)
        {
            lock (_logs)
            {
                _logs.Add(text);
            }
        }
    }

    #endregion

    public void Dispose()
    {
        _library.Dispose();
        unsafe
        {
            _pluginTable = null;
        }
    }
}
