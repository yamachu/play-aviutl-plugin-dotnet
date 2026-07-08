using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.AUO2;

[Flags]
public enum OutputInfoFlag : int
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
    /// <summary>
    /// 静止画出力のみサポートする (OUTPUT_INFOが1フレーム出力になります)
    /// ※静止画出力では出力完了時の通知やサウンド再生をしません
    /// </summary>
    Image = 4,
    /// <summary>
    /// プロジェクトファイルの設定保持をサポートする
    /// ※プロジェクトファイル側に出力設定を保持する場合に指定します
    /// </summary>
    ProjectConfig = 8,
}

/// <summary>
/// func_get_video に渡す画像フォーマット
/// 値はC++の複数文字リテラル('P''A''6''4'等)と同じバイト列
/// </summary>
public enum OutputVideoFormat : uint
{
    /// <summary>
    /// RGB24bit (BI_RGB)
    /// </summary>
    Rgb24 = 0,
    /// <summary>
    /// PA64 = DXGI_FORMAT_R16G16B16A16_UNORM(乗算済みα)
    /// </summary>
    Pa64 = 0x50413634, // 'PA64'
    /// <summary>
    /// HF64 = DXGI_FORMAT_R16G16B16A16_FLOAT(乗算済みα)(内部フォーマット)
    /// </summary>
    Hf64 = 0x48463634, // 'HF64'
    /// <summary>
    /// YUY2
    /// </summary>
    Yuy2 = 0x59555932, // 'YUY2'
    /// <summary>
    /// YC48 (互換対応のフォーマット, DXGI_FORMAT_R16G16B16A16_SNORM 相当)
    /// </summary>
    Yc48 = 0x59433438, // 'YC48'
}

/// <summary>
/// func_get_audio に渡す音声フォーマット
/// </summary>
public enum OutputAudioFormat : uint
{
    /// <summary>
    /// PCM16bit (WAVE_FORMAT_PCM)
    /// </summary>
    Pcm16 = 1,
    /// <summary>
    /// PCM(float)32bit (WAVE_FORMAT_IEEE_FLOAT)
    /// </summary>
    Float32 = 3,
}

/// <summary>
/// 出力情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct OUTPUT_INFO
{
    /// <summary>
    /// フラグ
    /// <see cref="OutputInfoFlag"/>
    /// </summary>
    public OutputInfoFlag flag;
    /// <summary>
    /// 縦横サイズ
    /// </summary>
    public int w, h;
    /// <summary>
    /// フレームレート、スケール
    /// </summary>
    public int rate, scale;
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
    /// <param name="format">画像フォーマット <see cref="OutputVideoFormat"/></param>
    /// <returns>データへのポインタ(次に外部関数を使うかメインに処理を戻すまで有効)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, uint, void*> func_get_video;
    /// <summary>
    /// PCM形式の音声データへのポインタを取得します
    /// <param name="start">開始サンプル番号</param>
    /// <param name="length">読み込むサンプル数</param>
    /// <param name="readed">読み込まれたサンプル数</param>
    /// <param name="format">音声フォーマット <see cref="OutputAudioFormat"/></param>
    /// <returns>データへのポインタ(次に外部関数を使うかメインに処理を戻すまで有効)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, int*, uint, void*> func_get_audio;
    /// <summary>
    /// 中断するか調べます
    /// <returns>trueなら中断</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<bool> func_is_abort;
    /// <summary>
    /// 残り時間を表示させます
    /// <param name="now">処理しているフレーム番号</param>
    /// <param name="total">処理する総フレーム数</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, void> func_rest_time_disp;
    /// <summary>
    /// データ取得のバッファ数(フレーム数)を設定します ※標準は4になります
    /// <param name="video_size">画像データのバッファ数</param>
    /// <param name="audio_size">音声データのバッファ数</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<int, int, void> func_set_buffer_size;
}

/// <summary>
/// 出力プラグイン構造体
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct OUTPUT_PLUGIN_TABLE
{
    /// <summary>
    /// フラグ
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
    /// <param name="oip">出力情報構造体へのポインタ(OUTPUT_INFO*)</param>
    /// <returns>成功時はtrueを返却</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_output;
    /// <summary>
    /// 出力設定のダイアログを要求された時に呼ばれる関数へのポインタ (nullptrなら呼ばれません)
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="dll_hinst">インスタンスハンドル</param>
    /// <returns>成功時はtrueを返却</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> func_config;
    /// <summary>
    /// 出力設定のテキスト情報を取得する時に呼ばれる関数へのポインタ (nullptrなら呼ばれません)
    /// <returns>出力設定のテキスト情報へのポインタ(次に関数が呼ばれるまで内容を有効にしておく)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr> func_get_config_text;
    /// <summary>
    /// プロジェクトファイル側から出力設定の読み込み要求時に呼ばれる関数へのポインタ (FLAG_PROJECT_CONFIGが有効の時のみ呼ばれます)
    /// <param name="project">プロジェクトファイル構造体へのポインタ(PROJECT_FILE*)</param>
    /// <returns>成功時はtrueを返却</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_load_project_config;
    /// <summary>
    /// プロジェクトファイル側への出力設定の書き込み要求時に呼ばれる関数へのポインタ (FLAG_PROJECT_CONFIGが有効の時のみ呼ばれます)
    /// <param name="project">プロジェクトファイル構造体へのポインタ(PROJECT_FILE*)</param>
    /// <returns>成功時はtrueを返却</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_save_project_config;
}
