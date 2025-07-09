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
        // 画像サイズ
        int w = ih.Width;
        int h = ih.Height;

        // BITMAPINFOHEADERを作成
        var bih = new Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = h,
            biPlanes = 1,
            biBitCount = 24,
            biCompression = 0, // BI_RGB
            biSizeImage = (uint)(w * h * 3),
            biXPelsPerMeter = 0,
            biYPelsPerMeter = 0,
            biClrUsed = 0,
            biClrImportant = 0
        };
        // アンマネージド領域に確保
        IntPtr bihPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>());
        Marshal.StructureToPtr(bih, bihPtr, false);

        // 1秒=30フレーム固定、rate=30, scale=1
        info = new INPUT_INFO()
        {
            flag = InputFlag.Video,
            rate = 30,
            scale = 1,
            n = 30,
            format = bihPtr,
            format_size = Marshal.SizeOf<Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER>(),
            audio_n = 0,
            audio_format = IntPtr.Zero,
            audio_format_size = 0
        };

        return true;
    }
}
