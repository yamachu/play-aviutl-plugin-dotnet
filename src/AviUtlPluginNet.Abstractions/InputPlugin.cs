namespace AviUtlPluginNet.Abstractions;

using System;
using AviUtlPluginNet.Core.Interop.AUI2;

/// <summary>
/// 入力プラグインの種別インターフェース (input2.h / GetInputPluginTable)
/// 能力は <see cref="IInputVideo{THandle}"/> / <see cref="IInputAudio{THandle}"/> 等を
/// 追加実装することで有効化されます (少なくともどちらか一方は必須)
/// </summary>
/// <typeparam name="THandle">入力ファイルハンドルの型</typeparam>
public interface IInputPlugin<THandle> : IAviUtl2Plugin
    where THandle : class, IInputHandle
{
    /// <summary>
    /// 入力ファイルフィルタ (例: "AviFile (*.avi)\0*.avi\0")
    /// </summary>
    static abstract string FileFilter { get; }

    /// <summary>
    /// 入力ファイルをオープンします
    /// </summary>
    /// <param name="file">ファイル名</param>
    /// <returns>入力ファイルハンドル (失敗時はnull)</returns>
    THandle? Open(string file);

    /// <summary>
    /// 入力ファイルをクローズします
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <returns>成功時はtrue</returns>
    bool Close(THandle handle);

    /// <summary>
    /// 入力ファイル情報を取得します
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <param name="info">入力ファイル情報</param>
    /// <returns>成功時はtrue</returns>
    bool TryGetInfo(THandle handle, out INPUT_INFO info);
}

/// <summary>
/// 画像データの読み込み能力 (FLAG_VIDEO / func_read_video)
/// </summary>
public interface IInputVideo<THandle> : IInputPlugin<THandle>
    where THandle : class, IInputHandle
{
    /// <summary>
    /// 画像データを読み込みます
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <param name="frame">読み込むフレーム番号</param>
    /// <returns>読み込んだフレームのデータ (空なら失敗)</returns>
    Span<byte> ReadVideo(THandle handle, int frame);
}

/// <summary>
/// 音声データの読み込み能力 (FLAG_AUDIO / func_read_audio)
/// </summary>
public interface IInputAudio<THandle> : IInputPlugin<THandle>
    where THandle : class, IInputHandle
{
    /// <summary>
    /// 音声データを読み込みます
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <param name="start">読み込み開始サンプル番号</param>
    /// <param name="length">読み込むサンプル数</param>
    /// <returns>読み込んだサンプルのデータ (空なら失敗)</returns>
    Span<byte> ReadAudio(THandle handle, int start, int length);
}

/// <summary>
/// 画像・音声データの同時取得サポートのマーカー (FLAG_CONCURRENT)
/// ※同一ハンドルで画像と音声の取得関数が同時に呼ばれる
/// ※異なるハンドルで各関数が同時に呼ばれる
/// </summary>
public interface IInputConcurrent;

/// <summary>
/// マルチトラック能力 (FLAG_MULTI_TRACK / func_set_track)
/// </summary>
public interface IInputMultiTrack<THandle> : IInputPlugin<THandle>
    where THandle : class, IInputHandle
{
    /// <summary>
    /// 入力ファイルの読み込み対象トラックを設定します
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <param name="type">トラックの種類</param>
    /// <param name="index">トラック番号 (-1が指定された場合はトラック数の取得)</param>
    /// <returns>設定したトラック番号 (失敗した場合は-1)
    /// トラック数の取得の場合は設定可能なトラックの数 (メディアが無い場合は0)</returns>
    int SetTrack(THandle handle, TrackType type, int index);
}

/// <summary>
/// 映像の時間からフレーム番号を算出する能力 (func_time_to_frame)
/// ※INPUT_INFOのflagにFLAG_TIME_TO_FRAMEを設定した場合に呼ばれます
/// ※利用する場合のINPUT_INFOのrate,scale情報は平均フレームレートを表す値を設定してください
/// </summary>
public interface IInputTimeToFrame<THandle> : IInputPlugin<THandle>
    where THandle : class, IInputHandle
{
    /// <summary>
    /// 映像の時間から該当フレーム番号を算出します
    /// </summary>
    /// <param name="handle">入力ファイルハンドル</param>
    /// <param name="time">映像の時間(秒)</param>
    /// <returns>映像の時間に対応するフレーム番号</returns>
    int TimeToFrame(THandle handle, double time);
}

/// <summary>
/// 入力設定ダイアログ能力 (func_config)
/// 実装しない場合はfunc_configがnullに設定され、ダイアログは表示されません
/// </summary>
public interface IInputConfigDialog
{
    /// <summary>
    /// 入力設定のダイアログを表示します
    /// </summary>
    /// <param name="hwnd">ウィンドウハンドル</param>
    /// <param name="dllHInstance">インスタンスハンドル</param>
    /// <returns>成功時はtrue</returns>
    bool Config(IntPtr hwnd, IntPtr dllHInstance);
}
