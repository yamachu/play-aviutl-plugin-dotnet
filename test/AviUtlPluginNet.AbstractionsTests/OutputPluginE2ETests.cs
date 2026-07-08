using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.AUO2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class OutputPluginE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public OutputPluginE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();
        PluginFixture.BuildTestPlugin(GetOutputPluginImplementation());
    }

    private static string GetOutputPluginImplementation()
    {
        return """
        using System;
        using System.IO;
        using AviUtlPluginNet.Abstractions;
        using AviUtlPluginNet.Core.Interop.AUO2;

        namespace AviUtlPluginNet.Example;

        [AviUtl2Plugin]
        class TestOutputPlugin : IOutputPlugin, IOutputVideo, IOutputAudio, IOutputConfigText
        {
            public static string Name => ".NET Example Output Plugin";
            public static string FileFilter => "Raw File (*.raw)\0*.raw\0";
            public static string Information => ".NET NativeAOT AviUtl Output Plugin Example";

            public bool Output(OutputContext context)
            {
                using var stream = File.Create(context.SaveFile);

                // 全フレームの映像データをそのまま書き込む
                for (int frame = 0; frame < context.FrameCount; frame++)
                {
                    if (context.IsAborted())
                    {
                        return false;
                    }
                    context.ReportProgress(frame, context.FrameCount);

                    var video = context.GetVideoFrame(frame, OutputVideoFormat.Rgb24);
                    stream.Write(video);
                }

                // 音声データをそのまま書き込む
                var audio = context.GetAudio(0, context.AudioSampleCount, out var read, OutputAudioFormat.Pcm16);
                stream.Write(audio);

                return true;
            }

            public string GetConfigText() => "quality=default";
        }
        """;
    }

    public void Dispose()
    {
        PluginFixture.Dispose();
    }
}

/// <summary>
/// 出力プラグインのNativeAOT E2Eテスト
/// ホスト側のOUTPUT_INFO(func_get_video等)をテストコード側で構築してfunc_outputを検証する
/// </summary>
public class OutputPluginE2ETests : IClassFixture<OutputPluginE2ETestsFixture>, IDisposable
{
    private const int Width = 16;
    private const int Height = 8;
    private const int FrameCount = 3;
    private const int AudioChannels = 2;
    private const int AudioSampleCount = 100;

    private const int VideoFrameBytes = Width * Height * 3; // RGB24
    private const int AudioBytes = AudioSampleCount * AudioChannels * 2; // PCM16

    private readonly NativePluginLibrary _library;
    private unsafe OUTPUT_PLUGIN_TABLE* _pluginTable = null;

    // ホスト側コールバックが使用するバッファ(UnmanagedCallersOnlyから参照するためstatic)
    private static IntPtr _videoBuffer;
    private static IntPtr _audioBuffer;
    private static int _progressCallCount;

    public OutputPluginE2ETests(OutputPluginE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);

        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetOutputPluginTable");
            _pluginTable = (OUTPUT_PLUGIN_TABLE*)getTable();
        }
    }

    /// <summary>
    /// GetOutputPluginTable エントリーポイントのテスト
    /// マーカーインターフェースの実装有無がflag・関数ポインタに反映されることを検証する
    /// </summary>
    [Fact]
    public void GetOutputPluginTable_ShouldReturnValidPointer()
    {
        unsafe
        {
            var table = _pluginTable;
            Assert.False(table == null);

            // IOutputVideo + IOutputAudio 実装 → flag は Video | Audio
            Assert.Equal(OutputPluginTableFlag.Video | OutputPluginTableFlag.Audio, table->flag);

            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_output);
            // IOutputConfigDialog 未実装 → func_config は null
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_config);
            // IOutputConfigText 実装 → func_get_config_text は非null
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_get_config_text);
            // IOutputProjectConfig 未実装 → null
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_load_project_config);
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_save_project_config);

            var name = Marshal.PtrToStringUni(table->name);
            Assert.Equal(".NET Example Output Plugin", name);
        }
    }

    /// <summary>
    /// func_get_config_text のテスト
    /// </summary>
    [Fact]
    public void FuncGetConfigText_ShouldReturnConfigText()
    {
        unsafe
        {
            var textPtr = _pluginTable->func_get_config_text();
            Assert.NotEqual(IntPtr.Zero, textPtr);
            Assert.Equal("quality=default", Marshal.PtrToStringUni(textPtr));
        }
    }

    /// <summary>
    /// func_output の完全なワークフローのE2Eテスト
    /// ホスト提供のOUTPUT_INFOコールバックからデータを取得しファイルへ出力する
    /// </summary>
    [Fact]
    public void FuncOutput_ShouldWriteVideoAndAudioData()
    {
        var savePath = Path.Combine(Path.GetTempPath(), $"OutputPluginE2E_{Guid.NewGuid():N}.raw");
        var savePathPtr = Marshal.StringToHGlobalUni(savePath);
        _videoBuffer = Marshal.AllocHGlobal(VideoFrameBytes);
        _audioBuffer = Marshal.AllocHGlobal(AudioBytes);
        _progressCallCount = 0;

        try
        {
            unsafe
            {
                var info = new OUTPUT_INFO
                {
                    flag = OutputInfoFlag.Video | OutputInfoFlag.Audio,
                    w = Width,
                    h = Height,
                    rate = 30,
                    scale = 1,
                    n = FrameCount,
                    audio_rate = 44100,
                    audio_ch = AudioChannels,
                    audio_n = AudioSampleCount,
                    savefile = savePathPtr,
                    func_get_video = &HostGetVideo,
                    func_get_audio = &HostGetAudio,
                    // boolはJITのUnmanagedCallersOnlyでnon-blittableのためbyte(ABI互換)でキャスト
                    func_is_abort = (delegate* unmanaged[Stdcall]<bool>)(delegate* unmanaged[Stdcall]<byte>)&HostIsAbort,
                    func_rest_time_disp = &HostRestTimeDisp,
                    func_set_buffer_size = &HostSetBufferSize,
                };

                var result = _pluginTable->func_output((IntPtr)(&info));
                Assert.True(result);
            }

            // プラグインが全フレーム分の進捗を報告した
            Assert.Equal(FrameCount, _progressCallCount);

            // 出力ファイルの検証: 各フレーム(フレーム番号+1で塗り潰し) + 音声(0xAB)
            var written = File.ReadAllBytes(savePath);
            Assert.Equal(FrameCount * VideoFrameBytes + AudioBytes, written.Length);
            for (int frame = 0; frame < FrameCount; frame++)
            {
                var expected = (byte)(frame + 1);
                for (int i = 0; i < VideoFrameBytes; i++)
                {
                    Assert.Equal(expected, written[frame * VideoFrameBytes + i]);
                }
            }
            for (int i = 0; i < AudioBytes; i++)
            {
                Assert.Equal(0xAB, written[FrameCount * VideoFrameBytes + i]);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(savePathPtr);
            Marshal.FreeHGlobal(_videoBuffer);
            Marshal.FreeHGlobal(_audioBuffer);
            _videoBuffer = IntPtr.Zero;
            _audioBuffer = IntPtr.Zero;
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    #region ホスト側コールバック(OUTPUT_INFOの関数ポインタ)

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void* HostGetVideo(int frame, uint format)
    {
        if (format != (uint)OutputVideoFormat.Rgb24)
        {
            return null;
        }
        // フレーム番号+1でバッファを塗り潰す
        new Span<byte>((void*)_videoBuffer, VideoFrameBytes).Fill((byte)(frame + 1));
        return (void*)_videoBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void* HostGetAudio(int start, int length, int* readed, uint format)
    {
        if (format != (uint)OutputAudioFormat.Pcm16)
        {
            *readed = 0;
            return null;
        }
        *readed = length;
        new Span<byte>((void*)_audioBuffer, length * AudioChannels * 2).Fill(0xAB);
        return (void*)_audioBuffer;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static byte HostIsAbort() => 0; // false

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostRestTimeDisp(int now, int total)
    {
        _progressCallCount++;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostSetBufferSize(int videoSize, int audioSize)
    {
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
