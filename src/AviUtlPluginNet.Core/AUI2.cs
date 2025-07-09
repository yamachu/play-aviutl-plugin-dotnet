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
    /// 画像・音声データの同時取得をサポートする ※読み込み関数が同時に呼ばれる
    /// </summary>
    Concurrent = 16
}

/// <summary>
/// 入力ファイル情報構造体
/// 画像フォーマットはRGB24bit,RGBA32bit,YUY2が対応しています
/// 音声フォーマットはPCM16bit,PCM(float)32bitが対応しています
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
}

