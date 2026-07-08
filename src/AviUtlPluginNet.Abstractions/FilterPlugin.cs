namespace AviUtlPluginNet.Abstractions;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.Filter2;

/// <summary>
/// フィルタプラグインの種別インターフェース (filter2.h / GetFilterPluginTable)
/// 処理は <see cref="IFilterVideo"/> / <see cref="IFilterAudio"/> を追加実装することで有効化されます
/// 設定項目は [FilterTrack] 等を付与した partial プロパティで宣言します
/// </summary>
public interface IFilterPlugin : IAviUtl2Plugin
{
    /// <summary>
    /// ラベルの初期値 (nullならデフォルトのラベルになります)
    /// </summary>
    static virtual string? Label => null;
}

/// <summary>
/// 画像フィルタ処理能力 (FLAG_VIDEO / func_proc_video)
/// ※画像と音声のフィルタ処理は別々のスレッドで処理されます
/// </summary>
public interface IFilterVideo : IFilterPlugin
{
    /// <summary>
    /// 画像フィルタ処理を行います
    /// </summary>
    /// <param name="context">画像フィルタ処理コンテキスト</param>
    /// <returns>falseを返却すると以降のフィルタや出力処理が中断されます</returns>
    bool ProcVideo(FilterVideoContext context);
}

/// <summary>
/// 音声フィルタ処理能力 (FLAG_AUDIO / func_proc_audio)
/// </summary>
public interface IFilterAudio : IFilterPlugin
{
    /// <summary>
    /// 音声フィルタ処理を行います
    /// </summary>
    /// <param name="context">音声フィルタ処理コンテキスト</param>
    /// <returns>falseを返却すると以降のフィルタや出力処理が中断されます</returns>
    bool ProcAudio(FilterAudioContext context);
}

/// <summary>
/// メディアオブジェクトの初期入力をするマーカー (FLAG_INPUT)
/// メディアオブジェクトにする場合に実装します
/// </summary>
public interface IFilterInput;

/// <summary>
/// フィルタオブジェクトをサポートするマーカー (FLAG_FILTER)
/// ※フィルタオブジェクトの場合は画像サイズの変更が出来ません
/// </summary>
public interface IFilterObject;

#region 設定項目の宣言属性

/// <summary>
/// トラックバー項目 (FILTER_ITEM_TRACK)
/// double型のget専用partialプロパティに付与します (値はホストにより常に最新に更新されます)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterTrackAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public double Default { get; set; }
    /// <summary>設定値の最小</summary>
    public double Min { get; set; }
    /// <summary>設定値の最大</summary>
    public double Max { get; set; } = 100.0;
    /// <summary>設定値の単位( 1.0 / 0.1 / 0.01 / 0.001 )</summary>
    public double Step { get; set; } = 1.0;
    /// <summary>ゼロ値名称 (設定値が0の時にトラックバーに表示する文字列)</summary>
    public string? ZeroDisplay { get; set; }
    /// <summary>操作倍率 (設定値の範囲に対してのトラックバー操作範囲の倍率)</summary>
    public double SliderRatio { get; set; } = 1.0;

    public FilterTrackAttribute(string name) => Name = name;
}

/// <summary>
/// チェックボックス項目 (FILTER_ITEM_CHECK)
/// bool型のget専用partialプロパティに付与します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterCheckAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public bool Default { get; set; }

    public FilterCheckAttribute(string name) => Name = name;
}

/// <summary>
/// 色選択項目 (FILTER_ITEM_COLOR)
/// int型のget専用partialプロパティに付与します (色コードは下位からb,g,r)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterColorAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期色コード (例: 0xffffff)</summary>
    public int Default { get; set; }

    public FilterColorAttribute(string name) => Name = name;
}

/// <summary>
/// 選択リスト項目 (FILTER_ITEM_SELECT)
/// int型のget専用partialプロパティに付与し、選択肢は [FilterSelectItem] で列挙します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterSelectAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public int Default { get; set; }

    public FilterSelectAttribute(string name) => Name = name;
}

/// <summary>
/// 選択リストの選択肢 ([FilterSelect] と併用、宣言順に列挙されます)
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class FilterSelectItemAttribute : Attribute
{
    public string Name { get; }
    public int Value { get; }

    public FilterSelectItemAttribute(string name, int value)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// ファイル選択項目 (FILTER_ITEM_FILE)
/// string型のget専用partialプロパティに付与します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterFileAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public string Default { get; set; } = "";
    /// <summary>ファイルフィルタ (例: "AviFile (*.avi)\0*.avi\0")</summary>
    public string FileFilter { get; set; } = "";

    public FilterFileAttribute(string name) => Name = name;
}

/// <summary>
/// フォルダ選択項目 (FILTER_ITEM_FOLDER)
/// string型のget専用partialプロパティに付与します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterFolderAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public string Default { get; set; } = "";

    public FilterFolderAttribute(string name) => Name = name;
}

/// <summary>
/// 文字列項目 ※1行の文字列 (FILTER_ITEM_STRING)
/// string型のget専用partialプロパティに付与します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterStringAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public string Default { get; set; } = "";

    public FilterStringAttribute(string name) => Name = name;
}

/// <summary>
/// テキスト項目 ※複数行の文字列 (FILTER_ITEM_TEXT)
/// string型のget専用partialプロパティに付与します
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FilterTextAttribute : Attribute
{
    public string Name { get; }
    /// <summary>設定値の初期値</summary>
    public string Default { get; set; } = "";

    public FilterTextAttribute(string name) => Name = name;
}

/// <summary>
/// ボタン項目 (FILTER_ITEM_BUTTON)
/// 引数なしvoidメソッドに付与します (ボタンを押すと呼ばれます。呼び出し時に各設定項目の設定値が更新されます)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FilterButtonAttribute : Attribute
{
    public string Name { get; }

    public FilterButtonAttribute(string name) => Name = name;
}

/// <summary>
/// 設定グループの開始 (FILTER_ITEM_GROUP)
/// 項目のプロパティ/メソッドに併記すると、その項目の直前にグループ項目が挿入されます
/// ※Nameを空にするとグループの終端を定義できます
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class FilterGroupAttribute : Attribute
{
    public string Name { get; }
    /// <summary>デフォルトの表示状態</summary>
    public bool DefaultVisible { get; set; } = true;

    public FilterGroupAttribute(string name) => Name = name;
}

/// <summary>
/// セパレーター (FILTER_ITEM_SEPARATOR)
/// 項目のプロパティ/メソッドに併記すると、その項目の直前にセパレーターが挿入されます
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public sealed class FilterSeparatorAttribute : Attribute
{
    public string Name { get; }

    public FilterSeparatorAttribute(string name) => Name = name;
}

#endregion

/// <summary>
/// FILTER_PROC_VIDEO* のマネージドラッパー
/// フィルタ処理の呼び出し中のみ有効です
/// ※D3D11/シェーダー系のAPIは実機未検証のためExperimentalです
/// </summary>
public sealed unsafe class FilterVideoContext
{
    private readonly FILTER_PROC_VIDEO* _proc;

    public FilterVideoContext(IntPtr proc)
    {
        _proc = (FILTER_PROC_VIDEO*)proc;
    }

    /// <summary>シーン情報</summary>
    public ref readonly SCENE_INFO Scene => ref *_proc->scene;

    /// <summary>オブジェクト情報</summary>
    public ref readonly OBJECT_INFO Object => ref *_proc->@object;

    /// <summary>オブジェクトの現在の画像幅</summary>
    public int Width => _proc->@object->width;

    /// <summary>オブジェクトの現在の画像高さ</summary>
    public int Height => _proc->@object->height;

    /// <summary>
    /// 現在のオブジェクトの画像パラメータ情報 (直接変更できます)
    /// ※画像出力項目のパラメータからの相対設定になります (スクリプトのobj.ox等と同じ)
    /// </summary>
    public ref OBJECT_IMAGE_PARAM Param => ref *_proc->param;

    /// <summary>
    /// 編集セクション(EDIT_SECTION*)の生ポインタ (未ラップAPI用)
    /// フィルタ処理中は参照系の関数が利用出来ます
    /// </summary>
    public IntPtr EditSection => _proc->edit;

    /// <summary>
    /// 現在のオブジェクトの画像データをPIXEL_RGBA形式で取得します (VRAMからデータを取得します)
    /// </summary>
    /// <param name="buffer">Width * Height 以上の長さのバッファ</param>
    public void GetImageData(Span<PIXEL_RGBA> buffer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, Width * Height, nameof(buffer));
        fixed (PIXEL_RGBA* p = buffer)
        {
            _proc->get_image_data(p);
        }
    }

    /// <summary>
    /// 現在のオブジェクトの画像データをPIXEL_RGBA形式で取得します (VRAMからデータを取得します)
    /// </summary>
    public PIXEL_RGBA[] GetImageData()
    {
        var buffer = new PIXEL_RGBA[Width * Height];
        GetImageData(buffer);
        return buffer;
    }

    /// <summary>
    /// 現在のオブジェクトの画像データをPIXEL_RGBA形式で設定します (VRAMへデータを書き込みます)
    /// </summary>
    /// <param name="buffer">width * height 以上の長さの画像データ</param>
    /// <param name="width">画像幅</param>
    /// <param name="height">画像高さ</param>
    public void SetImageData(ReadOnlySpan<PIXEL_RGBA> buffer, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, width * height, nameof(buffer));
        fixed (PIXEL_RGBA* p = buffer)
        {
            _proc->set_image_data(p, width, height);
        }
    }

    /// <summary>
    /// 初期データ無しで現在のオブジェクトの画像サイズを変更します
    /// </summary>
    public void ResizeImage(int width, int height)
        => _proc->set_image_data(null, width, height);

    /// <summary>
    /// 現在のオブジェクトのD3D画像リソース(ID3D11Texture2D*)を取得します
    /// ※現在の画像が変更されるかフィルタ処理の終了まで有効
    /// </summary>
    [Experimental("AUPF001")]
    public IntPtr GetImageTexture2D() => _proc->get_image_texture2d();

    /// <summary>
    /// 現在のフレームバッファのD3D画像リソース(ID3D11Texture2D*)を取得します
    /// ※フィルタ処理の終了まで有効
    /// </summary>
    [Experimental("AUPF001")]
    public IntPtr GetFramebufferTexture2D() => _proc->get_framebuffer_texture2d();

    /// <summary>
    /// FILTER_PROC_VIDEO* の生ポインタ (シェーダー実行等の未ラップAPI用)
    /// </summary>
    [Experimental("AUPF001")]
    public IntPtr RawHandle => (IntPtr)_proc;
}

/// <summary>
/// FILTER_PROC_AUDIO* のマネージドラッパー
/// フィルタ処理の呼び出し中のみ有効です
/// </summary>
public sealed unsafe class FilterAudioContext
{
    private readonly FILTER_PROC_AUDIO* _proc;

    public FilterAudioContext(IntPtr proc)
    {
        _proc = (FILTER_PROC_AUDIO*)proc;
    }

    /// <summary>シーン情報</summary>
    public ref readonly SCENE_INFO Scene => ref *_proc->scene;

    /// <summary>オブジェクト情報</summary>
    public ref readonly OBJECT_INFO Object => ref *_proc->@object;

    /// <summary>オブジェクトの現在の音声サンプル数</summary>
    public int SampleCount => _proc->@object->sample_num;

    /// <summary>オブジェクトの現在の音声チャンネル数 ※通常2になります</summary>
    public int ChannelCount => _proc->@object->channel_num;

    /// <summary>
    /// 現在のオブジェクトの音声パラメータ情報 (直接変更できます)
    /// </summary>
    public ref OBJECT_AUDIO_PARAM Param => ref *_proc->param;

    /// <summary>
    /// 編集セクション(EDIT_SECTION*)の生ポインタ (未ラップAPI用)
    /// </summary>
    public IntPtr EditSection => _proc->edit;

    /// <summary>
    /// 現在のオブジェクトの音声データを取得します ※PCM(float)32bit
    /// </summary>
    /// <param name="buffer">SampleCount 以上の長さのバッファ</param>
    /// <param name="channel">音声データのチャンネル ( 0 = 左チャンネル / 1 = 右チャンネル )</param>
    public void GetSampleData(Span<float> buffer, int channel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, SampleCount, nameof(buffer));
        fixed (float* p = buffer)
        {
            _proc->get_sample_data(p, channel);
        }
    }

    /// <summary>
    /// 現在のオブジェクトの音声データを設定します ※PCM(float)32bit
    /// </summary>
    /// <param name="buffer">SampleCount 以上の長さの音声データ</param>
    /// <param name="channel">音声データのチャンネル ( 0 = 左チャンネル / 1 = 右チャンネル )</param>
    public void SetSampleData(ReadOnlySpan<float> buffer, int channel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, SampleCount, nameof(buffer));
        fixed (float* p = buffer)
        {
            _proc->set_sample_data(p, channel);
        }
    }
}
