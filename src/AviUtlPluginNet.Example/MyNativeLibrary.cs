using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Abstractions;
using AviUtlPluginNet.Core.Interop.AUI2;
using SkiaSharp;

namespace AviUtlPluginNet.Example;

class MyPlugin : IInputVideoPluginWithoutConfig<PluginImageHandle>
{
    public static string name => ".NET Example Input Plugin";
    public static string fileFilter => "All Files (*.*)\0*.*\0";
    public static string information => ".NET NativeAOT AviUtl Input Plugin Example";

    public unsafe MyPlugin(
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr> funcOpen,
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcClose,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcInfoGet,
        delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> funcReadVideo,
        delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr, int> funcReadAudio,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig
    )
    {
        IInputVideoPluginWithoutConfig<PluginImageHandle>.InitPluginTable<MyPlugin>(
            funcOpen,
            funcClose,
            funcInfoGet,
            funcReadVideo,
            funcReadAudio,
            funcConfig
        );
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

public static unsafe class MyNativeLibrary
{
    private static readonly IInputPluginAPI plugin = new MyPlugin(
        &FuncOpen,
        &FuncClose,
        &FuncInfoGet,
        &FuncReadVideo,
        &FuncReadAudio,
        null
    );

    [UnmanagedCallersOnly(EntryPoint = "GetInputPluginTable", CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static IntPtr GetInputPluginTable()
    {
        _ = plugin; // プラグインの初期化を強制的に行う
        return IInputPluginAPI.GetPluginTablePtr<MyPlugin>();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static IntPtr FuncOpen(IntPtr file)
    {
        try
        {
            // AviUtlから渡されたファイルパス（UTF-16）を取得
            string? path = Marshal.PtrToStringUni(file);
            if (string.IsNullOrEmpty(path))
            {
                return IntPtr.Zero;
            }
            var handle = plugin.FuncOpen(path);
            if (handle == null)
            {
                return IntPtr.Zero;
            }
            var gch = GCHandle.Alloc(handle);
            return (IntPtr)gch;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static bool FuncClose(IntPtr ih)
    {
        if (ih == IntPtr.Zero) return false;
        var gch = (GCHandle)ih;
        if (gch.Target is PluginImageHandle handle)
        {
            plugin.FuncClose(handle);
            gch.Free();
            return true;
        }
        return false;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static bool FuncInfoGet(IntPtr ih, IntPtr iip)
    {
        if (ih == IntPtr.Zero || iip == IntPtr.Zero) return false;
        var gch = (GCHandle)ih;
        if (gch.Target is not PluginImageHandle handle) return false;

        if (plugin.FuncInfoGet(handle, out var i) == false)
        {
            return false;
        }

        unsafe
        {
            var info = (INPUT_INFO*)iip;
            info->flag = i.Value.flag;
            info->rate = i.Value.rate;
            info->scale = i.Value.scale;
            info->n = i.Value.n;
            info->format = i.Value.format;
            info->format_size = i.Value.format_size;
            info->audio_n = i.Value.audio_n;
            info->audio_format = i.Value.audio_format;
            info->audio_format_size = i.Value.audio_format_size;
        }
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static int FuncReadVideo(IntPtr ih, int frame, IntPtr buf)
    {
        if (ih == IntPtr.Zero || buf == IntPtr.Zero) return 0;
        var gch = (GCHandle)ih;
        if (gch.Target is not PluginImageHandle handle) return 0;

        var pixels = plugin.FuncReadVideo(handle, frame);

        Marshal.Copy(pixels.ToArray(), 0, buf, pixels.Length);
        return pixels.Length;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    public static int FuncReadAudio(IntPtr ih, int start, int length, IntPtr buf)
    {
        // 音声データ読み込み処理を実装
        return 0;
    }
}
