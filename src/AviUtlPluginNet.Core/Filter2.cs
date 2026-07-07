using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Filter2;

// filter2.h のうち、cache2.h 等からも参照される共有型
// (FILTER_PLUGIN_TABLE 等のフィルタプラグイン本体は未実装)

/// <summary>
/// RGBA32bit構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PIXEL_RGBA
{
    public byte r, g, b, a;
}

/// <summary>
/// 画像入力のピクセルフォーマット種別
/// </summary>
public enum INPUT_PIXEL_FORMAT : int
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM ※PIXEL_RGBA</summary>
    RGBA = 28,
    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM</summary>
    BGRA = 87,
    /// <summary>DXGI_FORMAT_B8G8R8X8_UNORM</summary>
    BGR = 88,
    /// <summary>DXGI_FORMAT_R16G16B16A16_UNORM</summary>
    PA64 = 11,
    /// <summary>DXGI_FORMAT_R16G16B16A16_FLOAT</summary>
    HF64 = 10,
    /// <summary>DXGI_FORMAT_YUY2</summary>
    YUY2 = 107,
    /// <summary>DXGI_FORMAT_R16G16B16A16_SNORM ※互換対応</summary>
    YC48 = 13,
}

/// <summary>
/// 画像出力のピクセルフォーマット種別
/// </summary>
public enum OUTPUT_PIXEL_FORMAT : int
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM ※PIXEL_RGBA</summary>
    RGBA = 28,
    /// <summary>DXGI_FORMAT_R16G16B16A16_UNORM</summary>
    PA64 = 11,
    /// <summary>DXGI_FORMAT_R16G16B16A16_FLOAT</summary>
    HF64 = 10,
}
