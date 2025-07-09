using System;
using AviUtlPluginNet.Abstractions;
using SkiaSharp;

namespace AviUtlPluginNet.Example;

public class PluginImageHandle : IInputHandle
{
    public SKBitmap Bitmap { get; }
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    public PluginImageHandle(SKBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}
