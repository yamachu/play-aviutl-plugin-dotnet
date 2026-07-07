using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Config2;

/// <summary>
/// フォント情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FONT_INFO
{
    /// <summary>
    /// フォント名(LPCWSTR)
    /// </summary>
    public IntPtr name;
    /// <summary>
    /// フォントサイズ
    /// </summary>
    public float size;
}

/// <summary>
/// 設定ハンドル
/// 各種プラグインで InitializeConfig(CONFIG_HANDLE* config) を外部公開すると呼び出されます
/// ※InitializePlugin()より先に呼ばれます
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CONFIG_HANDLE
{
    /// <summary>
    /// アプリケーションデータフォルダのパス(LPCWSTR)
    /// </summary>
    public IntPtr app_data_path;
    /// <summary>
    /// 現在の言語設定で定義されているテキストを取得します
    /// 参照する言語設定のセクションはInitializeConfig()を定義したプラグインのファイル名になります
    /// <param name="text">元のテキスト(.aul2ファイルのキー名, LPCWSTR)</param>
    /// <returns>定義されているテキストへのポインタ(未定義の場合は引数のテキストのポインタ) ※言語設定が更新されるまで有効</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, IntPtr> translate;
    /// <summary>
    /// 現在の言語設定で定義されているテキストを取得します ※任意のセクションから取得出来ます
    /// <param name="section">言語設定のセクション(.aul2ファイルのセクション名, LPCWSTR)</param>
    /// <param name="text">元のテキスト(.aul2ファイルのキー名, LPCWSTR)</param>
    /// <returns>定義されているテキストへのポインタ(未定義の場合は引数のテキストのポインタ) ※言語設定が更新されるまで有効</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, IntPtr, IntPtr> get_language_text;
    /// <summary>
    /// 設定ファイルで定義されているフォント情報を取得します
    /// <param name="key">設定ファイル(style.conf)の[Font]のキー名(LPCSTR)</param>
    /// <returns>フォント情報構造体へのポインタ(取得出来ない場合はデフォルトのフォント) ※次にこの関数を呼び出すまで有効</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, FONT_INFO*> get_font_info;
    /// <summary>
    /// 設定ファイルで定義されている色コードを取得します ※複数色の場合は最初の色が取得されます
    /// <param name="key">設定ファイル(style.conf)の[Color]のキー名(LPCSTR)</param>
    /// <returns>定義されている色コードの値(取得出来ない場合は0)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, int> get_color_code;
    /// <summary>
    /// 設定ファイルで定義されているレイアウトサイズを取得します
    /// <param name="key">設定ファイル(style.conf)の[Layout]のキー名(LPCSTR)</param>
    /// <returns>定義されているサイズ(取得出来ない場合は0)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, int> get_layout_size;
    /// <summary>
    /// 設定ファイルで定義されている色コードを取得します
    /// <param name="key">設定ファイル(style.conf)の[Color]のキー名(LPCSTR)</param>
    /// <param name="index">取得する色のインデックス(-1を指定すると色の数を返却します)</param>
    /// <returns>定義されている色コードの値(取得出来ない場合は0)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CONFIG_HANDLE*, IntPtr, int, int> get_color_code_index;
}
