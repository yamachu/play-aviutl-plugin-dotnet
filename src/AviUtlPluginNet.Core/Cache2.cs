using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.Filter2;

namespace AviUtlPluginNet.Core.Interop.Cache2;

// ============================================================================
// ABIに関する重要な注意 (cache2.h)
//
// CACHE_HANDLE の関数群は CACHE_IMAGE / CACHE_AUDIO / CACHE_FILE_IMAGE を
// 「値返し」するが、これらは非トリビアルなデストラクタを持つC++クラス
// (CACHE_REFERENCE継承、コピー/ムーブ削除)である。
//
// MSVC x64 ABI では非トリビアル型の値返しは「隠れた戻り値ポインタ(sret)」方式になる:
//   - 呼び出し側が戻り値用のバッファを確保し、そのポインタを【第1引数】として渡す
//   - 関数は同じポインタを RAX で返却する
//
// そのため本ファイルの関数ポインタ定義では、C++宣言に存在しない
// 「戻り値バッファへのポインタ」を第1引数として明示している。
//
// また C++ では ~CACHE_REFERENCE() が func_release(cache_instance) を呼ぶため、
// C# 側では利用終了時に func_release を明示的に呼ぶ必要がある
// (Abstractions の CacheImage 等が Dispose で行う)。
// ============================================================================

/// <summary>
/// キャッシュデータ参照の基底部 (CACHE_REFERENCE)
/// C++側ではデストラクタで func_release(cache_instance) が呼ばれる
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CACHE_REFERENCE
{
    /// <summary>
    /// キャッシュ参照の解放関数 (nullの場合は解放不要)
    /// </summary>
    public delegate* unmanaged[Stdcall]<void*, void> func_release;
    /// <summary>
    /// 解放関数に渡すキャッシュインスタンス
    /// </summary>
    public void* cache_instance;
}

/// <summary>
/// 画像キャッシュデータ構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CACHE_IMAGE
{
    public CACHE_REFERENCE reference;
    /// <summary>
    /// 画像キャッシュデータへのポインタ (取得失敗時はnull) ※画像データはPIXEL_RGBA
    /// </summary>
    public PIXEL_RGBA* buffer;
    /// <summary>
    /// 画像キャッシュの画像サイズ
    /// </summary>
    public int width, height;
}

/// <summary>
/// 音声キャッシュデータ構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CACHE_AUDIO
{
    public CACHE_REFERENCE reference;
    /// <summary>
    /// 音声キャッシュデータ(左チャンネル)へのポインタ (取得失敗時はnull) ※PCM(float)32bit
    /// </summary>
    public float* buffer0;
    /// <summary>
    /// 音声キャッシュデータ(右チャンネル)へのポインタ ※チャンネル数が1の場合は利用不可
    /// </summary>
    public float* buffer1;
    /// <summary>
    /// 音声キャッシュのサンプル数
    /// </summary>
    public int sample_num;
    /// <summary>
    /// 音声キャッシュのチャンネル数 ( 1 = モノラル / 2 = ステレオ )
    /// </summary>
    public int channel_num;
}

/// <summary>
/// メディアファイルの画像キャッシュデータ構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CACHE_FILE_IMAGE
{
    public CACHE_REFERENCE reference;
    /// <summary>
    /// 画像キャッシュデータへのポインタ (取得失敗時はnull) ※INPUT_PIXEL_FORMATのいずれか
    /// </summary>
    public void* buffer;
    /// <summary>
    /// 画像キャッシュの画像サイズ
    /// </summary>
    public int width, height;
    /// <summary>
    /// 画像キャッシュデータの横1ラインのバイト数
    /// </summary>
    public int pitch;
    /// <summary>
    /// 画像キャッシュのピクセルフォーマット
    /// </summary>
    public INPUT_PIXEL_FORMAT format;
}

/// <summary>
/// ビデオ情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VIDEO_INFO
{
    /// <summary>総時間</summary>
    public double total_time;
    /// <summary>総フレーム数</summary>
    public int frame_num;
    /// <summary>トラック数</summary>
    public int track_num;
    /// <summary>解像度</summary>
    public int width, height;
    /// <summary>フレームレート</summary>
    public int rate, scale;
}

/// <summary>
/// オーディオ情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct AUDIO_INFO
{
    /// <summary>総時間</summary>
    public double total_time;
    /// <summary>総サンプル数</summary>
    public long sample_num;
    /// <summary>トラック数</summary>
    public int track_num;
    /// <summary>サンプリングレート</summary>
    public int rate;
    /// <summary>チャンネル数</summary>
    public int channel;
}

/// <summary>
/// キャッシュハンドル
/// アプリケーションの共用のキャッシュ領域に各種キャッシュデータを作成することが出来ます
/// ※スクリプトのキャッシュバッファ(cache:xxxx)とは異なりメインメモリに確保されます
/// 各種プラグインで InitializeCache(CACHE_HANDLE* cache) を外部公開すると呼び出されます
/// ※InitializePlugin()より先に呼ばれます
/// ※CACHE_IMAGE等を値返しする関数は sret 方式のため第1引数に戻り値バッファを渡す(ファイル冒頭の注意を参照)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct CACHE_HANDLE
{
    /// <summary>
    /// 画像キャッシュデータを取得する
    /// <param name="ret">[sret] 戻り値バッファ</param>
    /// <param name="identifier">キャッシュ識別のポインタ ※任意の静的なポインタ</param>
    /// <param name="name">キャッシュ識別の名前(LPCWSTR)</param>
    /// <returns>retと同じポインタ (取得出来ない場合はbufferがnull)</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_IMAGE*, void*, IntPtr, CACHE_IMAGE*> get_image_cache;
    /// <summary>
    /// 画像キャッシュデータを作成する (返却されたキャッシュに画像データを書き込むことが出来る)
    /// <param name="ret">[sret] 戻り値バッファ</param>
    /// <param name="identifier">キャッシュ識別のポインタ</param>
    /// <param name="name">キャッシュ識別の名前(LPCWSTR)</param>
    /// <param name="width">作成するキャッシュの画像幅</param>
    /// <param name="height">作成するキャッシュの画像高さ</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_IMAGE*, void*, IntPtr, int, int, CACHE_IMAGE*> create_image_cache;
    /// <summary>
    /// 音声キャッシュデータを取得する
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_AUDIO*, void*, IntPtr, CACHE_AUDIO*> get_audio_cache;
    /// <summary>
    /// 音声キャッシュデータを作成する
    /// <param name="sample_num">作成する音声キャッシュのサンプル数</param>
    /// <param name="channel_num">チャンネル数 ( 1 = モノラル / 2 = ステレオ )</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_AUDIO*, void*, IntPtr, int, int, CACHE_AUDIO*> create_audio_cache;
    /// <summary>
    /// 新しい関数に差し替えるので廃止 (呼び出さないこと)
    /// </summary>
    public IntPtr deprecated_get_image_file_cache;
    /// <summary>
    /// メディアファイルのビデオ情報を取得する
    /// <param name="file">メディアファイルのパス(LPCWSTR)</param>
    /// <param name="info">ビデオ情報の格納先</param>
    /// <param name="info_size">格納先のサイズ</param>
    /// <returns>取得出来た場合はtrue</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, VIDEO_INFO*, int, bool> get_video_file_info;
    /// <summary>
    /// メディアファイルのオーディオ情報を取得する
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, AUDIO_INFO*, int, bool> get_audio_file_info;
    /// <summary>
    /// 画像ファイルから画像データをキャッシュ経由で取得する
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_FILE_IMAGE*, IntPtr, CACHE_FILE_IMAGE*> get_image_file_cache;
    /// <summary>
    /// メディアファイルから画像データをキャッシュ経由で取得する (フレーム番号指定)
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_FILE_IMAGE*, IntPtr, int, int, CACHE_FILE_IMAGE*> get_video_file_cache;
    /// <summary>
    /// メディアファイルから画像データをキャッシュ経由で取得する (時間指定)
    /// </summary>
    public delegate* unmanaged[Stdcall]<CACHE_FILE_IMAGE*, IntPtr, int, double, CACHE_FILE_IMAGE*> get_video_file_cache_by_time;
    /// <summary>
    /// メディアファイルから音声データをキャッシュ経由で取得する ※PCM(float)32bit2ch
    /// <param name="file">メディアファイルのパス(LPCWSTR)</param>
    /// <param name="track">オーディオトラック番号</param>
    /// <param name="sample_index">取得するサンプル位置</param>
    /// <param name="sample_num">取得するサンプル数</param>
    /// <param name="buffer0">サンプル(左チャンネル)取得先のバッファ</param>
    /// <param name="buffer1">サンプル(右チャンネル)取得先のバッファ</param>
    /// <returns>実際に取得したサンプル数</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int, long, int, float*, float*, int> get_audio_file_data;
}
