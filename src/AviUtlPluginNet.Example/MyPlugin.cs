using System;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.AUI2;
using SkiaSharp;

namespace AviUtlPluginNet.Example;

[AviUtl2Plugin]
class MyPlugin : IInputVideo<PluginImageHandle>, IUseLogger, IPluginLifecycle
{
    // 同一ディレクトリに配置された依存ネイティブライブラリ (libSkiaSharp.so/.dylib 等) を
    // P/Invoke が発生する前に探索範囲へ加える。
    // AttachLogger より先に Open が呼ばれる場合があるため static コンストラクタで登録する。
    static MyPlugin()
    {
        // ここに初期化処理を追加できます
        // 例：ログの初期化、設定の読み込み、etc.
        Console.WriteLine("MyPlugin initialized!");
        AssemblyLoadContext.Default.ResolvingUnmanagedDll
            += UnmanagedDllResolveHelper.UnmanagedDllCurrentLibraryLocationResolver.ResolveUnmanagedDll;
    }

    public static string Name => ".NET Example Input Plugin";
    public static string FileFilter => "All Files (*.*)\0*.*\0";
    public static string Information => ".NET NativeAOT AviUtl Input Plugin Example";

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
            _logger?.Warn($"Failed to decode: {file}");
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
