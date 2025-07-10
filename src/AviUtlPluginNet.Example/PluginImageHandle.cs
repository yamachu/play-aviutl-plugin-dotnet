using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using SkiaSharp;

namespace AviUtlPluginNet.Example;

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
