using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.AUI2;

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

        [Abstractions.Attribute.AviUtl2InputPlugin]
        partial class MyPlugin : IInputVideoPluginWithoutConfig<PluginImageHandle>
        {
            public static string name => ".NET Example Input Plugin";
            public static string fileFilter => "All Files (*.*)\0*.*\0";
            public static string information => ".NET NativeAOT AviUtl Input Plugin Example";

            // パラメータなしコンストラクタ - 初期化処理をここに書ける
            public MyPlugin()
            {
                // ここに初期化処理を追加できます
                // 例：ログの初期化、設定の読み込み、etc.
                // WARNING: dotnet test経由では、一つのテストクラスでUnloadを行なっても一度しか呼ばれません
                Console.WriteLine("MyPlugin initialized!");
            }

            public bool FuncClose(PluginImageHandle ih)
            {
                ih.Dispose();
                return true;
            }

            public IInputHandle? FuncOpen(string file)
            {
                var bitmap = SKBitmap.Decode(file);
                if (bitmap == null)
                {
                    return null;
                }
                return new PluginImageHandle(bitmap);
            }

            public Span<byte> FuncReadVideo(PluginImageHandle ih, int frame)
            {
                // 1秒=30フレームで1周回転
                float angle = (float)(frame % 30) / 30.0f * 360.0f;
                int w = ih.Width;
                int h = ih.Height;
                using var surface = SKSurface.Create(new SKImageInfo(w, h));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                // 中心回転
                canvas.Translate(w / 2f, h / 2f);
                canvas.RotateDegrees(angle);
                canvas.Translate(-w / 2f, -h / 2f);
                canvas.DrawBitmap(ih.Bitmap, 0, 0);
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

            public bool FuncInfoGet(PluginImageHandle ih, out INPUT_INFO? info)
            {
                // 1秒=30フレーム固定、rate=30, scale=1
                info = new INPUT_INFO()
                {
                    flag = InputFlag.Video,
                    rate = 30,
                    scale = 1,
                    n = 30,
                    format = ih.BitmapInfoPtr, // PluginImageHandleが管理するポインタを使用
                    format_size = Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>(),
                    audio_n = 0,
                    audio_format = IntPtr.Zero,
                    audio_format_size = 0
                };

                return true;
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
    private readonly PluginFixture _fixture;
    private INativeInputPluginTableProvider _pluginTableProvider;
    private unsafe INPUT_PLUGIN_TABLE* _pluginTable = null;

    public SimpleNativeLibraryE2ETests(SimpleNativeLibraryE2ETestsFixture testFixture)
    {
        _fixture = testFixture.PluginFixture;
        _pluginTableProvider = new NativePluginTableProviderDynamic(_fixture.DllPath);

        var tablePtr = _pluginTableProvider.GetInputPluginTable();
        if (tablePtr != IntPtr.Zero)
        {
            unsafe
            {
                _pluginTable = (INPUT_PLUGIN_TABLE*)tablePtr;
            }
        }
    }

    /// <summary>
    /// GetInputPluginTable エントリーポイントのテスト
    /// </summary>
    [Fact]
    public void GetInputPluginTable_ShouldReturnValidPointer()
    {
        try
        {
            var tablePtr = _pluginTableProvider.GetInputPluginTable();
            Assert.NotEqual(IntPtr.Zero, tablePtr);

            unsafe
            {
                var table = (INPUT_PLUGIN_TABLE*)tablePtr;
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_open);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_close);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_info_get);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_read_video);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_read_audio);
                Assert.Equal(IntPtr.Zero, (IntPtr)table->func_config);

                if (table->name != IntPtr.Zero)
                {
                    var name = Marshal.PtrToStringUni(table->name);
                    Assert.False(string.IsNullOrEmpty(name));
                }
            }
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message);
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

                    // 4. 音声データを読み取り（映像プラグインなので0を期待）
                    IntPtr audioBuffer = Marshal.AllocHGlobal(1000);
                    try
                    {
                        var audioSize = _pluginTable->func_read_audio(handle, 0, 1000, audioBuffer);
                        Assert.Equal(0, audioSize); // 映像プラグインなので音声なし
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(audioBuffer);
                    }

                    // 5. ファイルを閉じる
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
        if (_pluginTableProvider is IDisposable disp)
        {
            disp.Dispose();
        }
        unsafe
        {
            _pluginTable = null;
        }
    }
}
