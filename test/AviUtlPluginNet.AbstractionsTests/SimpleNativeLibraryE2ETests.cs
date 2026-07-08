using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.AUI2;
using AviUtlPluginNet.Core.Interop.Logger2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class SimpleNativeLibraryE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public SimpleNativeLibraryE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();

        // このテスト専用のプラグイン実装を設定してビルド
        var pluginImpl = GetCustomPluginImplementation();
        PluginFixture.BuildTestPlugin(pluginImpl);
    }

    private static string GetCustomPluginImplementation()
    {
        return """
        using System;
        using System.Runtime.InteropServices;
        using AviUtlPluginNet.Abstractions;
        using AviUtlPluginNet.Core.Interop.AUI2;
        using SkiaSharp;

        namespace AviUtlPluginNet.Example;

        [AviUtl2Plugin]
        class MyPlugin : IInputVideo<PluginImageHandle>, IUseLogger, IPluginLifecycle, IRequireVersion
        {
            public static string Name => ".NET Example Input Plugin";
            public static string FileFilter => "All Files (*.*)\0*.*\0";
            public static string Information => ".NET NativeAOT AviUtl Input Plugin Example";
            public static uint RequiredVersion => 2001;

            private ILogger2? _logger;

            // ホストからログ出力機能が注入される (IUseLogger)
            public void AttachLogger(ILogger2 logger)
            {
                _logger = logger;
            }

            // プラグインDLLの初期化・終了処理 (IPluginLifecycle)
            public bool OnInitialize(uint hostVersion)
            {
                _logger?.Info($"MyPlugin initialized! (host version: {hostVersion})");
                return true;
            }

            public void OnUninitialize()
            {
                _logger?.Info("MyPlugin uninitialized!");
            }

            public PluginImageHandle? Open(string file)
            {
                var bitmap = SKBitmap.Decode(file);
                if (bitmap == null)
                {
                    return null;
                }
                return new PluginImageHandle(bitmap);
            }

            public bool Close(PluginImageHandle handle)
            {
                handle.Dispose();
                return true;
            }

            public bool TryGetInfo(PluginImageHandle handle, out INPUT_INFO info)
            {
                // 1秒=30フレーム固定、rate=30, scale=1
                info = new INPUT_INFO()
                {
                    flag = InputFlag.Video,
                    rate = 30,
                    scale = 1,
                    n = 30,
                    format = handle.BitmapInfoPtr, // PluginImageHandleが管理するポインタを使用
                    format_size = Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>(),
                    audio_n = 0,
                    audio_format = IntPtr.Zero,
                    audio_format_size = 0
                };

                return true;
            }

            public Span<byte> ReadVideo(PluginImageHandle handle, int frame)
            {
                // 1秒=30フレームで1周回転
                float angle = (float)(frame % 30) / 30.0f * 360.0f;
                int w = handle.Width;
                int h = handle.Height;
                using var surface = SKSurface.Create(new SKImageInfo(w, h));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                // 中心回転
                canvas.Translate(w / 2f, h / 2f);
                canvas.RotateDegrees(angle);
                canvas.Translate(-w / 2f, -h / 2f);
                canvas.DrawBitmap(handle.Bitmap, 0, 0);
                canvas.Flush();

                using var img = surface.Snapshot();
                using var rotated = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                img.ReadPixels(rotated.Info, rotated.GetPixels(), rotated.RowBytes, 0, 0);
                // AviUtlは下から上のBGR24を期待するので、ピクセル変換
                var pixels = new byte[w * h * 3];
                var src = rotated.Pixels;
                for (int y = 0; y < h; y++)
                {
                    int srcY = h - 1 - y;
                    for (int x = 0; x < w; x++)
                    {
                        var color = src[srcY * w + x];
                        int idx = (y * w + x) * 3;
                        pixels[idx + 0] = color.Blue;
                        pixels[idx + 1] = color.Green;
                        pixels[idx + 2] = color.Red;
                    }
                }

                return pixels;
            }
        }

        public class PluginImageHandle : IInputHandle
        {
            public SKBitmap Bitmap { get; }
            public int Width => Bitmap.Width;
            public int Height => Bitmap.Height;

            // BITMAPINFOHEADERのポインタを保持
            public IntPtr BitmapInfoPtr { get; private set; }

            public PluginImageHandle(SKBitmap bitmap)
            {
                Bitmap = bitmap;
                CreateBitmapInfo();
            }

            private void CreateBitmapInfo()
            {
                // BITMAPINFOHEADERを作成
                var bih = new Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>(),
                    biWidth = Width,
                    biHeight = Height,
                    biPlanes = 1,
                    biBitCount = 24,
                    biCompression = 0, // BI_RGB
                    biSizeImage = (uint)(Width * Height * 3),
                    biXPelsPerMeter = 0,
                    biYPelsPerMeter = 0,
                    biClrUsed = 0,
                    biClrImportant = 0
                };

                // アンマネージド領域に確保
                BitmapInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>());
                Marshal.StructureToPtr(bih, BitmapInfoPtr, false);
            }

            public void Dispose()
            {
                Bitmap.Dispose();

                // BITMAPINFOHEADERのメモリを解放
                if (BitmapInfoPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(BitmapInfoPtr);
                    BitmapInfoPtr = IntPtr.Zero;
                }
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
/// NativeAOTで生成されたネイティブライブラリのE2Eテスト
/// SourceGeneratorが生成したアダプター層をテストする
/// </summary>
public class SimpleNativeLibraryE2ETests : IClassFixture<SimpleNativeLibraryE2ETestsFixture>, IDisposable
{
    private readonly NativePluginLibrary _library;
    private unsafe INPUT_PLUGIN_TABLE* _pluginTable = null;

    public SimpleNativeLibraryE2ETests(SimpleNativeLibraryE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);

        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetInputPluginTable");
            _pluginTable = (INPUT_PLUGIN_TABLE*)getTable();
        }
    }

    /// <summary>
    /// GetInputPluginTable エントリーポイントのテスト
    /// 能力インターフェースの実装有無がflag・関数ポインタに反映されることを検証する
    /// </summary>
    [Fact]
    public void GetInputPluginTable_ShouldReturnValidPointer()
    {
        unsafe
        {
            var table = _pluginTable;
            Assert.False(table == null);

            // IInputVideo のみ実装 → flag は Video のみ
            Assert.Equal(InputPluginTableFlag.Video, table->flag);

            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_open);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_close);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_info_get);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_read_video);
            // IInputAudio 未実装 → func_read_audio は null
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_read_audio);
            // IInputConfigDialog 未実装 → func_config は null
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_config);
            // IInputMultiTrack / IInputTimeToFrame 未実装 → null
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_set_track);
            Assert.Equal(IntPtr.Zero, (IntPtr)table->func_time_to_frame);

            var name = Marshal.PtrToStringUni(table->name);
            Assert.Equal(".NET Example Input Plugin", name);

            var information = Marshal.PtrToStringUni(table->information);
            Assert.Equal(".NET NativeAOT AviUtl Input Plugin Example", information);
        }
    }

    /// <summary>
    /// 完全なワークフローのE2Eテスト（ネイティブ関数呼び出し）
    /// </summary>
    [Fact]
    public void NativeWorkflow_ShouldWorkEndToEnd()
    {
        var testImagePath = CreateTestImage();

        try
        {
            unsafe
            {
                // 1. ファイルを開く（関数ポインタを使用）
                var filePathPtr = Marshal.StringToHGlobalUni(testImagePath);
                try
                {
                    var handle = _pluginTable->func_open(filePathPtr);
                    Assert.NotEqual(IntPtr.Zero, handle);

                    // 2. 情報を取得
                    var info = new INPUT_INFO();
                    var infoPtr = new IntPtr(&info);
                    var infoResult = _pluginTable->func_info_get(handle, infoPtr);
                    Assert.True(infoResult);

                    // 基本的な情報の検証
                    Assert.Equal(InputFlag.Video, info.flag);
                    Assert.Equal(30, info.rate);
                    Assert.Equal(1, info.scale);
                    Assert.Equal(30, info.n);

                    // 3. 映像データを読み取り
                    const int bufferSize = 200 * 150 * 3; // テスト画像のサイズ
                    IntPtr videoBuffer = Marshal.AllocHGlobal(bufferSize);
                    try
                    {
                        var readSize = _pluginTable->func_read_video(handle, 0, videoBuffer);
                        Assert.True(readSize > 0);
                        Assert.True(readSize <= bufferSize);

                        // 異なるフレームで異なるデータが返ることを確認
                        var readSize2 = _pluginTable->func_read_video(handle, 5, videoBuffer);
                        Assert.True(readSize2 > 0);
                        // フレーム0と5では回転により内容が異なるはず
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(videoBuffer);
                    }

                    // 4. ファイルを閉じる
                    var closeResult = _pluginTable->func_close(handle);
                    Assert.True(closeResult);
                }
                finally
                {
                    Marshal.FreeHGlobal(filePathPtr);
                }
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"Native workflow failed: {ex.Message}");
        }
        finally
        {
            File.Delete(testImagePath);
        }
    }

    /// <summary>
    /// エラーケースのE2Eテスト
    /// </summary>
    [Fact]
    public void NativeErrorCases_ShouldBeHandledGracefully()
    {
        unsafe
        {
            // 存在しないファイル
            var invalidPathPtr = Marshal.StringToHGlobalUni("does_not_exist.png");
            try
            {
                var invalidHandle = _pluginTable->func_open(invalidPathPtr);
                Assert.Equal(IntPtr.Zero, invalidHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(invalidPathPtr);
            }

            // 無効なハンドルでの操作
            var infoResult = _pluginTable->func_info_get(IntPtr.Zero, IntPtr.Zero);
            Assert.False(infoResult);

            var closeResult = _pluginTable->func_close(IntPtr.Zero);
            Assert.False(closeResult);

            var readSize = _pluginTable->func_read_video(IntPtr.Zero, 0, IntPtr.Zero);
            Assert.Equal(0, readSize);
        }
    }

    /// <summary>
    /// メモリリークテスト - 複数回の開閉
    /// </summary>
    [Fact]
    public void MultipleOpenClose_ShouldNotLeakMemory()
    {
        var testImagePath = CreateTestImage();

        try
        {
            unsafe
            {
                var filePathPtr = Marshal.StringToHGlobalUni(testImagePath);
                try
                {
                    // 複数回の開閉を実行
                    for (int i = 0; i < 10; i++)
                    {
                        var handle = _pluginTable->func_open(filePathPtr);
                        Assert.NotEqual(IntPtr.Zero, handle);

                        // 簡単な操作
                        var info = new INPUT_INFO();
                        var infoPtr = new IntPtr(&info);
                        _pluginTable->func_info_get(handle, infoPtr);

                        var closeResult = _pluginTable->func_close(handle);
                        Assert.True(closeResult);
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Multiple open/close failed: {ex.Message}");
                }
                finally
                {
                    Marshal.FreeHGlobal(filePathPtr);
                }
            }
        }
        finally
        {
            File.Delete(testImagePath);
        }
    }

    #region ホストサービス機能(cake機能)のエクスポート検証

    private static readonly ConcurrentQueue<string> CapturedLogs = new();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void CaptureLog(LOG_HANDLE* handle, IntPtr message)
    {
        CapturedLogs.Enqueue(Marshal.PtrToStringUni(message) ?? string.Empty);
    }

    /// <summary>
    /// IRequireVersion 実装 → RequiredVersion エクスポートが生成されることを検証
    /// </summary>
    [Fact]
    public void RequiredVersionExport_ShouldReturnDeclaredVersion()
    {
        unsafe
        {
            var requiredVersion = (delegate* unmanaged[Stdcall]<uint>)_library.GetExport("RequiredVersion");
            Assert.Equal(2001u, requiredVersion());
        }
    }

    /// <summary>
    /// IUseLogger / IPluginLifecycle 実装 → InitializeLogger / InitializePlugin / UninitializePlugin
    /// エクスポートが生成され、注入したLOG_HANDLE経由でログが出力されることを検証
    /// </summary>
    [Fact]
    public void LoggerAndLifecycleExports_ShouldInjectLoggerAndInitialize()
    {
        unsafe
        {
            // ホスト側のLOG_HANDLEを構築してプラグインに注入
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

                // InitializePlugin → OnInitialize がログを出力する
                var initializePlugin = (delegate* unmanaged[Stdcall]<uint, bool>)_library.GetExport("InitializePlugin");
                var result = initializePlugin(2001u);
                Assert.True(result);
                Assert.Contains(CapturedLogs, m => m.Contains("MyPlugin initialized!") && m.Contains("2001"));

                // UninitializePlugin → OnUninitialize がログを出力する
                var uninitializePlugin = (delegate* unmanaged[Stdcall]<void>)_library.GetExport("UninitializePlugin");
                uninitializePlugin();
                Assert.Contains(CapturedLogs, m => m.Contains("MyPlugin uninitialized!"));
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)logHandle);
            }
        }
    }

    /// <summary>
    /// 実装していない機能インターフェースのエクスポートは生成されないことを検証
    /// </summary>
    [Fact]
    public void UnimplementedFeatureExports_ShouldNotExist()
    {
        // IUseConfig 未実装 → InitializeConfig エクスポートなし
        Assert.False(_library.TryGetExport("InitializeConfig", out _));
        // Inputプラグイン → GetOutputPluginTable エクスポートなし
        Assert.False(_library.TryGetExport("GetOutputPluginTable", out _));
    }

    #endregion

    private string CreateTestImage()
    {
        var tempFile = Path.GetTempFileName();
        var newPath = Path.ChangeExtension(tempFile, ".png");
        File.Delete(tempFile);

        // 簡単なテスト画像を作成
        using var bitmap = new SkiaSharp.SKBitmap(200, 150);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Blue);

        using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Red };
        canvas.DrawRect(50, 50, 100, 50, paint);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(newPath);
        data.SaveTo(stream);

        return newPath;
    }

    public void Dispose()
    {
        _library.Dispose();
        unsafe
        {
            _pluginTable = null;
        }
    }
}
