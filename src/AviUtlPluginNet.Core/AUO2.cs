using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.AUO2;

[Flags]
public enum OutputFlag : int
{
    None = 0,
    /// <summary>
    /// 画像データあり
    /// </summary>
    Video = 1,
    /// <summary>
    /// 音声データあり
    /// </summary>
    Audio = 2,
}

/// <summary>
/// 出力情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct OUTPUT_INFO
{
    /// <summary>
    /// フラグ
    /// <see cref="OutputFlag"/>
    /// </summary>
    public OutputFlag flag;
    /// <summary>
    /// 縦横サイズ
    /// </summary>
    public int w;
    /// <summary>
    /// 縦横サイズ
    /// </summary>
    public int h;
    /// <summary>
    /// フレームレート
    /// </summary>
    public int rate;
    /// <summary>
    /// スケール
    /// </summary>
    public int scale;
    /// <summary>
    /// フレーム数
    /// </summary>
    public int n;
    /// <summary>
    /// 音声サンプリングレート
    /// </summary>
    public int audio_rate;
    /// <summary>
    /// 音声チャンネル数
    /// </summary>
    public int audio_ch;
    /// <summary>
    /// 音声サンプリング数
    /// </summary>
    public int audio_n;
    /// <summary>
    /// セーブファイル名へのポインタ
    /// </summary>
    public IntPtr /* LPCWSTR */ savefile;
    /// <summary>
    /// DIB形式の画像データを取得します
    /// <param name="frame">フレーム番号</param>
    /// <param name="format">画像フォーマット( 0(BI_RGB) = RGB24bit / 'Y''U''Y''2' = YUY2 )</param>
    /// <returns>データへのポインタ（画像データポインタの内容は次に外部関数を使うかメインに処理を戻すまで有効）</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, uint, IntPtr> func_get_video;
    /// <summary>
    /// PCM形式の音声データへのポインタを取得します
    /// <param name="start">開始サンプル番号</param>
    /// <param name="length">読み込むサンプル数</param>
    /// <param name="readed">読み込まれたサンプル数</param>
    /// <param name="format">音声フォーマット( 1(WAVE_FORMAT_PCM) = PCM16bit / 3(WAVE_FORMAT_IEEE_FLOAT) = PCM(float)32bit )</param>
    /// <returns>データへのポインタ（音声データポインタの内容は次に外部関数を使うかメインに処理を戻すまで有効）</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, IntPtr, uint, IntPtr> func_get_audio;
    /// <summary>
    /// 中断するか調べます
    /// <returns>TRUEなら中断</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<bool> func_is_abort;
    /// <summary>
    /// 残り時間を表示させます
    /// <param name="now">処理しているフレーム番号</param>
    /// <param name="total">処理する総フレーム数</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, void /* Header document的にはBooleanだけどvoid？ */> func_rest_time_disp;
    /// <summary>
    /// データ取得のバッファ数(フレーム数)を設定します ※標準は4になります
    /// バッファ数の半分のデータを先読みリクエストするようになります
    /// <param name="video_size">画像データのバッファ数</param>
    /// <param name="audio_size">音声データのバッファ数</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, void> func_set_buffer_size;
}

[Flags]
public enum OutputPluginTableFlag : int
{
    None = 0,
    /// <summary>
    /// 画像をサポートする
    /// </summary>
    Video = 1,
    /// <summary>
    /// 音声をサポートする
    /// </summary>
    Audio = 2,
}

/// <summary>
/// 出力プラグイン構造体
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct OUTPUT_PLUGIN_TABLE
{
    /// <summary>
    /// フラグ ※未使用
    /// <see cref="OutputPluginTableFlag"/>
    /// </summary>
    public OutputPluginTableFlag flag;
    /// <summary>
    /// プラグインの名前
    /// </summary>
    public IntPtr /* LPCWSTR */ name;
    /// <summary>
    /// ファイルのフィルタ
    /// </summary>
    public IntPtr /* LPCWSTR */ filefilter;
    /// <summary>
    /// プラグインの情報
    /// </summary>
    public IntPtr /* LPCWSTR */ information;
    /// <summary>
    /// 出力時に呼ばれる関数へのポインタ
    /// <param name="oip">出力情報へのポインタ（OUTPUT_INFO*）</param>
    /// <returns>TRUEなら成功</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_output;
    /// <summary>
    /// 出力設定のダイアログを要求された時に呼ばれる関数へのポインタ (nullptrなら呼ばれません)
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="dll_hinst">インスタンスハンドル</param>
    /// <returns>TRUEなら成功</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> func_config;
    /// <summary>
    /// 出力設定のテキスト情報を取得する時に呼ばれる関数へのポインタ (nullptrなら呼ばれません)
    /// <returns>出力設定のテキスト情報(次に関数が呼ばれるまで内容を有効にしておく)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr> func_get_config_text;
}
