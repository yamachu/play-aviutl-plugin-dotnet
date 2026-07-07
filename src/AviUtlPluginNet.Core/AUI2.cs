using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.AUI2;

[Flags]
public enum InputFlag : int
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
    /// <summary>
    /// フレーム番号を時間から算出する ※func_time_to_frame()が呼ばれるようになる
    /// </summary>
    TimeToFrame = 16,
}

public enum TrackType : int
{
    /// <summary>
    /// 映像
    /// </summary>
    Video = 0,
    /// <summary>
    /// 音声
    /// </summary>
    Audio = 1,
}

/// <summary>
/// 入力ファイル情報構造体
/// 画像フォーマットはRGB24bit,RGBA32bit,PA64,HF64,YUY2,YC48が対応しています
/// 音声フォーマットはPCM16bit,PCM(float)32bitが対応しています
/// ※PA64はDXGI_FORMAT_R16G16B16A16_UNORM(乗算済みα)です
/// ※HF64はDXGI_FORMAT_R16G16B16A16_FLOAT(乗算済みα)です(内部フォーマット)
/// ※YC48は互換対応のフォーマットです
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct INPUT_INFO
{
    /// <summary>
    /// フラグ
    /// <see cref="InputFlag"/>
    /// </summary>
    public InputFlag flag;
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
    /// 画像フォーマットへのポインタ(次に関数が呼ばれるまで内容を有効にしておく)
    /// <see cref="Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER"/>
    /// </summary>
    public IntPtr format;
    /// <summary>
    /// 画像フォーマットのサイズ
    /// </summary>
    public int format_size;
    /// <summary>
    /// 音声のサンプル数
    /// </summary>
    public int audio_n;
    /// <summary>
    /// 音声フォーマットへのポインタ(次に関数が呼ばれるまで内容を有効にしておく)
    /// <see cref="Windows.Win32.Media.Audio.WAVEFORMATEX"/>
    /// </summary>
    public IntPtr audio_format;
    /// <summary>
    /// 音声フォーマットのサイズ
    /// </summary>
    public int audio_format_size;
}

[Flags]
public enum InputPluginTableFlag : int
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
    /// 画像・音声データの同時取得をサポートする
    /// ※同一ハンドルで画像と音声の取得関数が同時に呼ばれる
    /// ※異なるハンドルで各関数が同時に呼ばれる
    /// </summary>
    Concurrent = 16,
    /// <summary>
    /// マルチトラックをサポートする ※func_set_track()が呼ばれるようになる
    /// </summary>
    MultiTrack = 32,
}

/// <summary>
/// 入力プラグイン構造体
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct INPUT_PLUGIN_TABLE
{
    /// <summary>
    /// フラグ
    /// <see cref="InputPluginTableFlag"/>
    /// </summary>
    public InputPluginTableFlag flag;
    /// <summary>
    /// プラグインの名前
    /// </summary>
    public IntPtr /* LPCWSTR */ name;
    /// <summary>
    /// 入力ファイルフィルタ
    /// </summary>
    public IntPtr /* LPCWSTR */ filefilter;
    /// <summary>
    /// プラグインの情報
    /// </summary>
    public IntPtr /* LPCWSTR */ information;
    /// <summary>
    /// 入力ファイルをオープンする関数へのポインタ
    /// <param name="file">ファイル名（LPCWSTR）</param>
    /// <returns>TRUEなら入力ファイルハンドル（INPUT_HANDLE）</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr> func_open;
    /// <summary>
    /// 入力ファイルをクローズする関数へのポインタ
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <returns>TRUEなら成功</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_close;
    /// <summary>
    /// 入力ファイル情報を取得する関数へのポインタ
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <param name="info">入力ファイル情報へのポインタ（INPUT_INFO*）</param>
    /// <returns>TRUEなら成功</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> func_info_get;
    /// <summary>
    /// 画像データを読み込む関数へのポインタ
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <param name="frame">読み込むフレーム番号</param>
    /// <param name="buf">データを読み込むバッファへのポインタ</param>
    /// <returns>読み込んだフレームのサイズ</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> func_read_video;
    /// <summary>
    /// 音声データを読み込む関数へのポインタ
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <param name="start">読み込み開始サンプル番号</param>
    /// <param name="length">読み込むサンプル数</param>
    /// <param name="buf">データを読み込むバッファへのポインタ</param>
    /// <returns>読み込んだサンプル数</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr, int> func_read_audio;
    /// <summary>
    /// 入力設定のダイアログを要求された時に呼ばれる関数へのポインタ (nullptrなら呼ばれません)
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="hInstance">インスタンスハンドル</param>
    /// <returns>TRUEなら成功</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> func_config;
    /// <summary>
    /// 入力ファイルの読み込み対象トラックを設定する関数へのポインタ (FLAG_MULTI_TRACKが有効の時のみ呼ばれます)
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <param name="type">トラックの種類 (TrackType)</param>
    /// <param name="index">トラック番号 ( -1 が指定された場合はトラック数の取得 )</param>
    /// <returns>設定したトラック番号 (失敗した場合は -1 を返却)
    ///			  トラック数の取得の場合は設定可能なトラックの数 (メディアが無い場合は 0 を返却)
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int, int, int> func_set_track;

    /// <summary>
    /// 映像の時間から該当フレーム番号を算出する時に呼ばれる関数へのポインタ (FLAG_TIME_TO_FRAMEが有効の時のみ呼ばれます)
    /// 画像データを読み込む前に呼び出され、結果のフレーム番号で読み込むようになります。
    /// ※FLAG_TIME_TO_FRAMEを利用する場合のINPUT_INFOのrate,scale情報は平均フレームレートを表す値を設定してください
    /// <param name="ih">入力ファイルハンドル（INPUT_HANDLE）</param>
    /// <param name="time">映像の時間(秒)</param>
    /// <returns>映像の時間に対応するフレーム番号</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, double, int> func_time_to_frame;
}

