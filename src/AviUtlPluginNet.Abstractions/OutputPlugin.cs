namespace AviUtlPluginNet.Abstractions;

using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.AUO2;

/// <summary>
/// 出力プラグインの種別インターフェース (output2.h / GetOutputPluginTable)
/// 対応データは <see cref="IOutputVideo"/> / <see cref="IOutputAudio"/> 等の
/// マーカーを追加実装することで宣言します (少なくともどちらか一方は必須)
/// </summary>
public interface IOutputPlugin : IAviUtl2Plugin
{
    /// <summary>
    /// ファイルのフィルタ (例: "AviFile (*.avi)\0*.avi\0")
    /// </summary>
    static abstract string FileFilter { get; }

    /// <summary>
    /// 出力処理を行います
    /// </summary>
    /// <param name="context">出力情報コンテキスト</param>
    /// <returns>成功時はtrue</returns>
    bool Output(OutputContext context);
}

/// <summary>
/// 画像出力サポートのマーカー (FLAG_VIDEO)
/// </summary>
public interface IOutputVideo;

/// <summary>
/// 音声出力サポートのマーカー (FLAG_AUDIO)
/// </summary>
public interface IOutputAudio;

/// <summary>
/// 静止画出力のみサポートのマーカー (FLAG_IMAGE)
/// OUTPUT_INFOが1フレーム出力になります
/// ※静止画出力では出力完了時の通知やサウンド再生をしません
/// </summary>
public interface IOutputImageOnly;

/// <summary>
/// 出力設定ダイアログ能力 (func_config)
/// 実装しない場合はfunc_configがnullに設定され、ダイアログは表示されません
/// </summary>
public interface IOutputConfigDialog
{
    /// <summary>
    /// 出力設定のダイアログを表示します
    /// </summary>
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="dllHInstance">インスタンスハンドル</param>
    /// <returns>成功時はtrue</returns>
    bool Config(IntPtr hwnd, IntPtr dllHInstance);
}

/// <summary>
/// 出力設定のテキスト情報取得能力 (func_get_config_text)
/// </summary>
public interface IOutputConfigText
{
    /// <summary>
    /// 出力設定のテキスト情報を取得します
    /// </summary>
    string GetConfigText();
}

/// <summary>
/// プロジェクトファイルの設定保持能力 (FLAG_PROJECT_CONFIG / func_load_project_config, func_save_project_config)
/// </summary>
public interface IOutputProjectConfig
{
    /// <summary>
    /// プロジェクトファイル側から出力設定を読み込みます
    /// </summary>
    /// <param name="project">プロジェクトファイル</param>
    /// <returns>成功時はtrue</returns>
    bool LoadProjectConfig(ProjectFile project);

    /// <summary>
    /// プロジェクトファイル側へ出力設定を書き込みます
    /// </summary>
    /// <param name="project">プロジェクトファイル</param>
    /// <returns>成功時はtrue</returns>
    bool SaveProjectConfig(ProjectFile project);
}

/// <summary>
/// プロジェクトファイル構造体(PROJECT_FILE*)のハンドル
/// </summary>
public readonly record struct ProjectFile(IntPtr Pointer);

/// <summary>
/// OUTPUT_INFO* のマネージドラッパー
/// func_outputの呼び出し中のみ有効です
/// </summary>
public sealed unsafe class OutputContext
{
    private readonly OUTPUT_INFO* _info;

    public OutputContext(IntPtr infoPtr)
    {
        _info = (OUTPUT_INFO*)infoPtr;
    }

    /// <summary>
    /// フラグ
    /// </summary>
    public OutputInfoFlag Flag => _info->flag;

    /// <summary>
    /// 画像データがあるか
    /// </summary>
    public bool HasVideo => (_info->flag & OutputInfoFlag.Video) != 0;

    /// <summary>
    /// 音声データがあるか
    /// </summary>
    public bool HasAudio => (_info->flag & OutputInfoFlag.Audio) != 0;

    /// <summary>
    /// 画像の横サイズ
    /// </summary>
    public int Width => _info->w;

    /// <summary>
    /// 画像の縦サイズ
    /// </summary>
    public int Height => _info->h;

    /// <summary>
    /// フレームレート
    /// </summary>
    public int Rate => _info->rate;

    /// <summary>
    /// スケール
    /// </summary>
    public int Scale => _info->scale;

    /// <summary>
    /// フレーム数
    /// </summary>
    public int FrameCount => _info->n;

    /// <summary>
    /// 音声サンプリングレート
    /// </summary>
    public int AudioRate => _info->audio_rate;

    /// <summary>
    /// 音声チャンネル数
    /// </summary>
    public int AudioChannels => _info->audio_ch;

    /// <summary>
    /// 音声サンプリング数
    /// </summary>
    public int AudioSampleCount => _info->audio_n;

    /// <summary>
    /// セーブファイル名
    /// </summary>
    public string SaveFile => Marshal.PtrToStringUni(_info->savefile) ?? string.Empty;

    /// <summary>
    /// DIB形式の画像データへのポインタを取得します
    /// ※ポインタの内容は次に外部関数を使うかメインに処理を戻すまで有効
    /// </summary>
    public IntPtr GetVideoPtr(int frame, OutputVideoFormat format = OutputVideoFormat.Rgb24)
        => (IntPtr)_info->func_get_video(frame, (uint)format);

    /// <summary>
    /// DIB形式の画像データを取得します
    /// ※Spanの内容は次に外部関数を使うかメインに処理を戻すまで有効
    /// </summary>
    public ReadOnlySpan<byte> GetVideoFrame(int frame, OutputVideoFormat format = OutputVideoFormat.Rgb24)
    {
        var ptr = _info->func_get_video(frame, (uint)format);
        if (ptr == null)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        var size = _info->w * _info->h * BytesPerPixel(format);
        return new ReadOnlySpan<byte>(ptr, size);
    }

    /// <summary>
    /// PCM形式の音声データを取得します
    /// ※Spanの内容は次に外部関数を使うかメインに処理を戻すまで有効
    /// </summary>
    /// <param name="start">開始サンプル番号</param>
    /// <param name="length">読み込むサンプル数</param>
    /// <param name="read">読み込まれたサンプル数</param>
    /// <param name="format">音声フォーマット</param>
    public ReadOnlySpan<byte> GetAudio(int start, int length, out int read, OutputAudioFormat format = OutputAudioFormat.Pcm16)
    {
        int readSamples = 0;
        var ptr = _info->func_get_audio(start, length, &readSamples, (uint)format);
        read = readSamples;
        if (ptr == null || readSamples <= 0)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        var size = readSamples * _info->audio_ch * BytesPerSample(format);
        return new ReadOnlySpan<byte>(ptr, size);
    }

    /// <summary>
    /// 中断するか調べます
    /// </summary>
    public bool IsAborted() => _info->func_is_abort();

    /// <summary>
    /// 残り時間を表示させます
    /// </summary>
    /// <param name="now">処理しているフレーム番号</param>
    /// <param name="total">処理する総フレーム数</param>
    public void ReportProgress(int now, int total) => _info->func_rest_time_disp(now, total);

    /// <summary>
    /// データ取得のバッファ数(フレーム数)を設定します ※標準は4になります
    /// </summary>
    /// <param name="videoSize">画像データのバッファ数</param>
    /// <param name="audioSize">音声データのバッファ数</param>
    public void SetBufferSize(int videoSize, int audioSize) => _info->func_set_buffer_size(videoSize, audioSize);

    private static int BytesPerPixel(OutputVideoFormat format) => format switch
    {
        OutputVideoFormat.Rgb24 => 3,
        OutputVideoFormat.Pa64 => 8,
        OutputVideoFormat.Hf64 => 8,
        OutputVideoFormat.Yuy2 => 2,
        OutputVideoFormat.Yc48 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static int BytesPerSample(OutputAudioFormat format) => format switch
    {
        OutputAudioFormat.Pcm16 => 2,
        OutputAudioFormat.Float32 => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}
