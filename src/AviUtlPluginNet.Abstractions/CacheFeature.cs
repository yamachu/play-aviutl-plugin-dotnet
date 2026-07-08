namespace AviUtlPluginNet.Abstractions;

using System;
using AviUtlPluginNet.Core.Interop.Cache2;
using AviUtlPluginNet.Core.Interop.Filter2;

// キャッシュ関連のホストサービス機能 (cache2.h)
// ※入力プラグインではファイルからのキャッシュ取得関数が非推奨になります

/// <summary>
/// キャッシュ関連機能 (cache2.h)
/// アプリケーションの共用のキャッシュ領域に各種キャッシュデータを作成することが出来ます
/// </summary>
public interface ICache2
{
    /// <summary>
    /// 画像キャッシュデータを取得します (取得出来ない場合はnull)
    /// 返却されたオブジェクトは利用後に必ずDisposeしてください(キャッシュ参照の解放)
    /// </summary>
    /// <param name="name">キャッシュ識別の名前</param>
    CacheImage? GetImageCache(string name);

    /// <summary>
    /// 画像キャッシュデータを作成します (返却されたキャッシュに画像データを書き込むことが出来ます)
    /// 返却されたオブジェクトは利用後に必ずDisposeしてください(キャッシュ参照の解放)
    /// </summary>
    /// <param name="name">キャッシュ識別の名前</param>
    /// <param name="width">作成するキャッシュの画像幅</param>
    /// <param name="height">作成するキャッシュの画像高さ</param>
    CacheImage? CreateImageCache(string name, int width, int height);

    /// <summary>
    /// 音声キャッシュデータを取得します (取得出来ない場合はnull)
    /// </summary>
    /// <param name="name">キャッシュ識別の名前</param>
    CacheAudio? GetAudioCache(string name);

    /// <summary>
    /// 音声キャッシュデータを作成します (返却されたキャッシュに音声データを書き込むことが出来ます)
    /// </summary>
    /// <param name="name">キャッシュ識別の名前</param>
    /// <param name="sampleNum">作成する音声キャッシュのサンプル数</param>
    /// <param name="channelNum">チャンネル数 ( 1 = モノラル / 2 = ステレオ )</param>
    CacheAudio? CreateAudioCache(string name, int sampleNum, int channelNum);

    /// <summary>
    /// メディアファイルのビデオ情報を取得します
    /// </summary>
    bool TryGetVideoFileInfo(string file, out VIDEO_INFO info);

    /// <summary>
    /// メディアファイルのオーディオ情報を取得します
    /// </summary>
    bool TryGetAudioFileInfo(string file, out AUDIO_INFO info);

    /// <summary>
    /// 画像ファイルから画像データをキャッシュ経由で取得します (取得出来ない場合はnull)
    /// </summary>
    CacheFileImage? GetImageFileCache(string file);

    /// <summary>
    /// メディアファイルから画像データをキャッシュ経由で取得します (フレーム番号指定)
    /// </summary>
    CacheFileImage? GetVideoFileCache(string file, int track, int frame);

    /// <summary>
    /// メディアファイルから画像データをキャッシュ経由で取得します (時間指定)
    /// </summary>
    CacheFileImage? GetVideoFileCacheByTime(string file, int track, double time);

    /// <summary>
    /// メディアファイルから音声データをキャッシュ経由で取得します ※PCM(float)32bit2ch
    /// </summary>
    /// <param name="file">メディアファイルのパス</param>
    /// <param name="track">オーディオトラック番号</param>
    /// <param name="sampleIndex">取得するサンプル位置</param>
    /// <param name="buffer0">サンプル(左チャンネル)取得先のバッファ</param>
    /// <param name="buffer1">サンプル(右チャンネル)取得先のバッファ</param>
    /// <returns>実際に取得したサンプル数</returns>
    int GetAudioFileData(string file, int track, long sampleIndex, Span<float> buffer0, Span<float> buffer1);
}

/// <summary>
/// キャッシュ関連機能を利用する機能インターフェース (export: InitializeCache)
/// ※InitializePlugin()より先に呼ばれます
/// </summary>
public interface IUseCache
{
    /// <summary>
    /// ホストからキャッシュ関連機能が注入されます
    /// </summary>
    void AttachCache(ICache2 cache);
}

/// <summary>
/// 画像キャッシュデータ (CACHE_IMAGEのマネージドラッパー)
/// C++のデストラクタ相当の解放処理をDisposeで行います
/// </summary>
public sealed unsafe class CacheImage : IDisposable
{
    private CACHE_IMAGE _raw;
    private bool _disposed;

    internal CacheImage(in CACHE_IMAGE raw)
    {
        _raw = raw;
    }

    /// <summary>画像キャッシュの画像幅</summary>
    public int Width => _raw.width;

    /// <summary>画像キャッシュの画像高さ</summary>
    public int Height => _raw.height;

    /// <summary>
    /// 画像キャッシュデータ (書き込み可能)
    /// ※Dispose後はアクセスしないこと
    /// </summary>
    public Span<PIXEL_RGBA> Buffer => new(_raw.buffer, _raw.width * _raw.height);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        // C++の ~CACHE_REFERENCE() 相当
        if (_raw.reference.func_release != null && _raw.reference.cache_instance != null)
        {
            _raw.reference.func_release(_raw.reference.cache_instance);
        }
        _raw = default;
    }
}

/// <summary>
/// 音声キャッシュデータ (CACHE_AUDIOのマネージドラッパー)
/// C++のデストラクタ相当の解放処理をDisposeで行います
/// </summary>
public sealed unsafe class CacheAudio : IDisposable
{
    private CACHE_AUDIO _raw;
    private bool _disposed;

    internal CacheAudio(in CACHE_AUDIO raw)
    {
        _raw = raw;
    }

    /// <summary>音声キャッシュのサンプル数</summary>
    public int SampleCount => _raw.sample_num;

    /// <summary>音声キャッシュのチャンネル数 ( 1 = モノラル / 2 = ステレオ )</summary>
    public int ChannelCount => _raw.channel_num;

    /// <summary>
    /// 音声キャッシュデータ(左チャンネル, PCM float 32bit)
    /// ※Dispose後はアクセスしないこと
    /// </summary>
    public Span<float> Buffer0 => new(_raw.buffer0, _raw.sample_num);

    /// <summary>
    /// 音声キャッシュデータ(右チャンネル, PCM float 32bit)
    /// チャンネル数が1の場合は空
    /// </summary>
    public Span<float> Buffer1 => _raw.buffer1 == null ? Span<float>.Empty : new(_raw.buffer1, _raw.sample_num);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_raw.reference.func_release != null && _raw.reference.cache_instance != null)
        {
            _raw.reference.func_release(_raw.reference.cache_instance);
        }
        _raw = default;
    }
}

/// <summary>
/// メディアファイルの画像キャッシュデータ (CACHE_FILE_IMAGEのマネージドラッパー)
/// C++のデストラクタ相当の解放処理をDisposeで行います
/// </summary>
public sealed unsafe class CacheFileImage : IDisposable
{
    private CACHE_FILE_IMAGE _raw;
    private bool _disposed;

    internal CacheFileImage(in CACHE_FILE_IMAGE raw)
    {
        _raw = raw;
    }

    /// <summary>画像キャッシュの画像幅</summary>
    public int Width => _raw.width;

    /// <summary>画像キャッシュの画像高さ</summary>
    public int Height => _raw.height;

    /// <summary>画像キャッシュデータの横1ラインのバイト数</summary>
    public int Pitch => _raw.pitch;

    /// <summary>画像キャッシュのピクセルフォーマット</summary>
    public INPUT_PIXEL_FORMAT Format => _raw.format;

    /// <summary>
    /// 画像キャッシュデータ (pitch * height バイト)
    /// ※Dispose後はアクセスしないこと
    /// </summary>
    public ReadOnlySpan<byte> Buffer => new(_raw.buffer, _raw.pitch * _raw.height);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_raw.reference.func_release != null && _raw.reference.cache_instance != null)
        {
            _raw.reference.func_release(_raw.reference.cache_instance);
        }
        _raw = default;
    }
}

/// <summary>
/// CACHE_HANDLE* をラップした <see cref="ICache2"/> の実装
/// キャッシュ識別のポインタ(identifier)にはCACHE_HANDLE自身のポインタを使用します
/// (cache2.hの「CACHE_HANDLEやFILTER_PLUGIN_TABLE等の任意の静的なポインタ」に準拠)
/// </summary>
public sealed unsafe class NativeCache2 : ICache2
{
    private readonly CACHE_HANDLE* _handle;

    public NativeCache2(IntPtr handle)
    {
        _handle = (CACHE_HANDLE*)handle;
    }

    private void* Identifier => _handle;

    public CacheImage? GetImageCache(string name)
    {
        fixed (char* n = name)
        {
            CACHE_IMAGE result = default;
            _handle->get_image_cache(&result, Identifier, (IntPtr)n);
            return ToWrapper(result);
        }
    }

    public CacheImage? CreateImageCache(string name, int width, int height)
    {
        fixed (char* n = name)
        {
            CACHE_IMAGE result = default;
            _handle->create_image_cache(&result, Identifier, (IntPtr)n, width, height);
            return ToWrapper(result);
        }
    }

    public CacheAudio? GetAudioCache(string name)
    {
        fixed (char* n = name)
        {
            CACHE_AUDIO result = default;
            _handle->get_audio_cache(&result, Identifier, (IntPtr)n);
            return ToWrapper(result);
        }
    }

    public CacheAudio? CreateAudioCache(string name, int sampleNum, int channelNum)
    {
        fixed (char* n = name)
        {
            CACHE_AUDIO result = default;
            _handle->create_audio_cache(&result, Identifier, (IntPtr)n, sampleNum, channelNum);
            return ToWrapper(result);
        }
    }

    public bool TryGetVideoFileInfo(string file, out VIDEO_INFO info)
    {
        fixed (char* f = file)
        {
            VIDEO_INFO local = default;
            var result = _handle->get_video_file_info((IntPtr)f, &local, sizeof(VIDEO_INFO));
            info = local;
            return result;
        }
    }

    public bool TryGetAudioFileInfo(string file, out AUDIO_INFO info)
    {
        fixed (char* f = file)
        {
            AUDIO_INFO local = default;
            var result = _handle->get_audio_file_info((IntPtr)f, &local, sizeof(AUDIO_INFO));
            info = local;
            return result;
        }
    }

    public CacheFileImage? GetImageFileCache(string file)
    {
        fixed (char* f = file)
        {
            CACHE_FILE_IMAGE result = default;
            _handle->get_image_file_cache(&result, (IntPtr)f);
            return ToWrapper(result);
        }
    }

    public CacheFileImage? GetVideoFileCache(string file, int track, int frame)
    {
        fixed (char* f = file)
        {
            CACHE_FILE_IMAGE result = default;
            _handle->get_video_file_cache(&result, (IntPtr)f, track, frame);
            return ToWrapper(result);
        }
    }

    public CacheFileImage? GetVideoFileCacheByTime(string file, int track, double time)
    {
        fixed (char* f = file)
        {
            CACHE_FILE_IMAGE result = default;
            _handle->get_video_file_cache_by_time(&result, (IntPtr)f, track, time);
            return ToWrapper(result);
        }
    }

    public int GetAudioFileData(string file, int track, long sampleIndex, Span<float> buffer0, Span<float> buffer1)
    {
        var sampleNum = Math.Min(buffer0.Length, buffer1.Length);
        fixed (char* f = file)
        fixed (float* b0 = buffer0)
        fixed (float* b1 = buffer1)
        {
            return _handle->get_audio_file_data((IntPtr)f, track, sampleIndex, sampleNum, b0, b1);
        }
    }

    // 取得失敗(buffer==null)の場合はC++デストラクタ相当の解放だけ行いnullを返す
    private static CacheImage? ToWrapper(in CACHE_IMAGE raw)
    {
        if (raw.buffer == null)
        {
            Release(raw.reference);
            return null;
        }
        return new CacheImage(raw);
    }

    private static CacheAudio? ToWrapper(in CACHE_AUDIO raw)
    {
        if (raw.buffer0 == null)
        {
            Release(raw.reference);
            return null;
        }
        return new CacheAudio(raw);
    }

    private static CacheFileImage? ToWrapper(in CACHE_FILE_IMAGE raw)
    {
        if (raw.buffer == null)
        {
            Release(raw.reference);
            return null;
        }
        return new CacheFileImage(raw);
    }

    private static void Release(in CACHE_REFERENCE reference)
    {
        if (reference.func_release != null && reference.cache_instance != null)
        {
            reference.func_release(reference.cache_instance);
        }
    }
}
