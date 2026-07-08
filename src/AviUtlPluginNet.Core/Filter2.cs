using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Filter2;

// filter2.h の型定義
// PIXEL_RGBA / ピクセルフォーマットは cache2.h 等からも参照される

/// <summary>
/// RGBA32bit構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PIXEL_RGBA
{
    public byte r, g, b, a;
}

/// <summary>
/// 画像入力のピクセルフォーマット種別
/// </summary>
public enum INPUT_PIXEL_FORMAT : int
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM ※PIXEL_RGBA</summary>
    RGBA = 28,
    /// <summary>DXGI_FORMAT_B8G8R8A8_UNORM</summary>
    BGRA = 87,
    /// <summary>DXGI_FORMAT_B8G8R8X8_UNORM</summary>
    BGR = 88,
    /// <summary>DXGI_FORMAT_R16G16B16A16_UNORM</summary>
    PA64 = 11,
    /// <summary>DXGI_FORMAT_R16G16B16A16_FLOAT</summary>
    HF64 = 10,
    /// <summary>DXGI_FORMAT_YUY2</summary>
    YUY2 = 107,
    /// <summary>DXGI_FORMAT_R16G16B16A16_SNORM ※互換対応</summary>
    YC48 = 13,
}

/// <summary>
/// 画像出力のピクセルフォーマット種別
/// </summary>
public enum OUTPUT_PIXEL_FORMAT : int
{
    /// <summary>DXGI_FORMAT_R8G8B8A8_UNORM ※PIXEL_RGBA</summary>
    RGBA = 28,
    /// <summary>DXGI_FORMAT_R16G16B16A16_UNORM</summary>
    PA64 = 11,
    /// <summary>DXGI_FORMAT_R16G16B16A16_FLOAT</summary>
    HF64 = 10,
}

[Flags]
public enum FilterPluginTableFlag : int
{
    None = 0,
    /// <summary>
    /// 画像フィルタをサポートする
    /// </summary>
    Video = 1,
    /// <summary>
    /// 音声フィルタをサポートする ※画像と音声のフィルタ処理は別々のスレッドで処理されます
    /// </summary>
    Audio = 2,
    /// <summary>
    /// メディアオブジェクトの初期入力をする (メディアオブジェクトにする場合)
    /// </summary>
    Input = 4,
    /// <summary>
    /// フィルタオブジェクトをサポートする ※フィルタオブジェクトの場合は画像サイズの変更が出来ません
    /// </summary>
    Filter = 8,
}

/// <summary>
/// 合成モードの種別
/// </summary>
public enum BLEND_MODE : int
{
    NONE = 0, ADD = 1, SUB = 2, MUL = 3, SCREEN = 4, OVERLAY = 5,
    LIGHT = 6, DARK = 7, BRIGHTNESS = 8, CHROMA = 9, SHADOW = 10,
    LIGHT_DARK = 11, DIFF = 12,
}

/// <summary>
/// ビルボードの種別
/// </summary>
public enum BILLBOARD_MODE : int
{
    NONE = 0, SIDE = 1, DIRECTION = 2, CAMERA = 3,
}

/// <summary>
/// サンプラー(SampleState)の種別
/// </summary>
public enum SAMPLER_MODE : int
{
    CLIP = 0, CLAMP = 1, LOOP = 2, MIRROR = 3, DOT = 4,
}

/// <summary>
/// 頂点リストの種別
/// </summary>
public enum VERTEX_TYPE : int
{
    TRIANGLE_COLOR = 1, TRIANGLE_COLOR_NORM = 2, TRIANGLE_TEXTURE = 3, TRIANGLE_TEXTURE_NORM = 4,
    QUAD_COLOR = 5, QUAD_COLOR_NORM = 6, QUAD_TEXTURE = 7, QUAD_TEXTURE_NORM = 8,
}

/// <summary>
/// シーン情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SCENE_INFO
{
    /// <summary>シーンの解像度</summary>
    public int width, height;
    /// <summary>シーンのフレームレート</summary>
    public int rate, scale;
    /// <summary>シーンのサンプリングレート</summary>
    public int sample_rate;
}

/// <summary>
/// オブジェクト情報構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct OBJECT_INFO
{
    /// <summary>オブジェクトのID (アプリ起動毎の固有ID) ※描画対象のオブジェクトの固有ID</summary>
    public long id;
    /// <summary>オブジェクトの現在のフレーム番号</summary>
    public int frame;
    /// <summary>オブジェクトの総フレーム数</summary>
    public int frame_total;
    /// <summary>オブジェクトの現在の時間(秒)</summary>
    public double time;
    /// <summary>オブジェクトの総時間(秒)</summary>
    public double time_total;
    /// <summary>オブジェクトの現在の画像サイズ (画像フィルタのみ)</summary>
    public int width, height;
    /// <summary>オブジェクトの現在の音声サンプル位置 (音声フィルタのみ)</summary>
    public long sample_index;
    /// <summary>オブジェクトの総サンプル数 (音声フィルタのみ)</summary>
    public long sample_total;
    /// <summary>オブジェクトの現在の音声サンプル数 (音声フィルタのみ)</summary>
    public int sample_num;
    /// <summary>オブジェクトの現在の音声チャンネル数 (音声フィルタのみ) ※通常2になります</summary>
    public int channel_num;
    /// <summary>オブジェクトの内の対象エフェクトのID (アプリ起動毎の固有ID)</summary>
    public long effect_id;
    /// <summary>フラグ (1 = フィルタオブジェクト)</summary>
    public int flag;
    /// <summary>オブジェクトの現在のレイヤー番号</summary>
    public int layer;
    /// <summary>複数オブジェクト時の現在の対象番号 ※個別オブジェクト用</summary>
    public int index;
    /// <summary>複数オブジェクト時の対象数 (1 = 単体オブジェクト / 0 = 不定) ※個別オブジェクト用</summary>
    public int num;
    /// <summary>全体(シーン)基準のオブジェクトの開始フレーム(0からの番号)</summary>
    public int frame_s;
    /// <summary>全体(シーン)基準のオブジェクトの終了フレーム(0からの番号)</summary>
    public int frame_e;

    /// <summary>フィルタオブジェクトか？</summary>
    public readonly bool IsFilterObject => (flag & 1) != 0;
}

/// <summary>
/// オブジェクトの画像パラメータ構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct OBJECT_IMAGE_PARAM
{
    /// <summary>基準座標</summary>
    public float x, y, z;
    /// <summary>回転角度 (360.0で1回転)</summary>
    public float rx, ry, rz;
    /// <summary>拡大率 (1.0=等倍)</summary>
    public float sx, sy, sz;
    /// <summary>中心座標 (基準座標からの相対)</summary>
    public float cx, cy, cz;
    /// <summary>不透明度 (0.0〜1.0/0.0=透明/1.0=不透明)</summary>
    public float alpha;
}

/// <summary>
/// オブジェクトの音声パラメータ構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct OBJECT_AUDIO_PARAM
{
    /// <summary>音量倍率 (1.0=等倍)</summary>
    public float vol_l, vol_r;
}

// ============================================================================
// FILTER_ITEM_XXX (設定項目構造体)
// C++側はコンストラクタでtypeフィールドに種別文字列を設定する
// value系フィールドはフィルタ処理の呼び出し時にホストが現在の値に更新する
// ============================================================================

/// <summary>
/// トラックバー項目構造体 (type = "track2")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_TRACK
{
    /// <summary>設定の種別 "track2" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (フィルタ処理の呼び出し時に現在の値に更新されます)</summary>
    public double value;
    /// <summary>設定値の最小、最大</summary>
    public double s, e;
    /// <summary>設定値の単位( 1.0 / 0.1 / 0.01 / 0.001 )</summary>
    public double step;
    /// <summary>ゼロ値名称 (設定値が0の時にトラックバーに表示する文字列, LPCWSTR)</summary>
    public IntPtr zero_display;
    /// <summary>操作倍率 (設定値の範囲に対してのトラックバー操作範囲の倍率)</summary>
    public double slider_ratio;
}

/// <summary>
/// チェックボックス項目構造体 (type = "check")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_CHECK
{
    /// <summary>設定の種別 "check" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (C++ bool, 1バイト)</summary>
    public byte value;
}

/// <summary>
/// チェックボックス(セクション毎)項目構造体 (type = "checksection2")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_CHECK_SECTION
{
    /// <summary>設定の種別 "checksection2" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (C++ bool, 1バイト)</summary>
    public byte value;
    /// <summary>セクション毎設定の初期値 (C++ bool, 1バイト)</summary>
    public byte multi_section;
}

/// <summary>
/// 色選択項目構造体 (type = "color")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_COLOR
{
    /// <summary>設定の種別 "color" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値の色コード (下位からb,g,r,x)</summary>
    public int value;
}

/// <summary>
/// 選択リストの選択肢項目 (FILTER_ITEM_SELECT::ITEM)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_SELECT_ITEM
{
    /// <summary>選択肢の名前 (LPCWSTR, nullで終端)</summary>
    public IntPtr name;
    /// <summary>選択肢の値</summary>
    public int value;
}

/// <summary>
/// 選択リスト項目構造体 (type = "select")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FILTER_ITEM_SELECT
{
    /// <summary>設定の種別 "select" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (フィルタ処理の呼び出し時に現在の値に更新されます)</summary>
    public int value;
    /// <summary>選択肢リスト (名前がnullのITEMで終端したリストへのポインタ)</summary>
    public FILTER_ITEM_SELECT_ITEM* list;
}

/// <summary>
/// ファイル選択項目構造体 (type = "file")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_FILE
{
    /// <summary>設定の種別 "file" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (フィルタ処理の呼び出し時に現在の値のポインタに更新されます, LPCWSTR)</summary>
    public IntPtr value;
    /// <summary>ファイルフィルタ (LPCWSTR)</summary>
    public IntPtr filefilter;
}

/// <summary>
/// 文字列(string)/テキスト(text)/フォルダ(folder)項目構造体 (共通レイアウト)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_STRING_LIKE
{
    /// <summary>設定の種別 "string"/"text"/"folder" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>設定値 (フィルタ処理の呼び出し時に現在の値のポインタに更新されます, LPCWSTR)</summary>
    public IntPtr value;
}

/// <summary>
/// 設定グループ項目構造体 (type = "group")
/// 自身以降の設定項目をグループ化することが出来ます ※設定名を空にするとグループの終端
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_GROUP
{
    /// <summary>設定の種別 "group" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>デフォルトの表示状態 (C++ bool, 1バイト)</summary>
    public byte default_visible;
}

/// <summary>
/// セパレーター項目構造体 (type = "separator")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FILTER_ITEM_SEPARATOR
{
    /// <summary>設定の種別 "separator" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
}

/// <summary>
/// ボタン項目構造体 (type = "button")
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FILTER_ITEM_BUTTON
{
    /// <summary>設定の種別 "button" (LPCWSTR)</summary>
    public IntPtr type;
    /// <summary>設定名 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>ボタンを押した時のコールバック関数 (void (*)(EDIT_SECTION*)) ※呼び出し時に各設定項目の設定値が更新されます</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> callback;
}

// ============================================================================
// フィルタ処理用構造体
// ============================================================================

/// <summary>
/// 画像フィルタ処理用構造体
/// D3D11/シェーダー系の未ラップメンバーはIntPtrで表現している(レイアウト維持のため順序厳守)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FILTER_PROC_VIDEO
{
    /// <summary>シーン情報</summary>
    public SCENE_INFO* scene;
    /// <summary>オブジェクト情報</summary>
    public OBJECT_INFO* @object;
    /// <summary>現在のオブジェクトの画像データをPIXEL_RGBA形式で取得する (VRAMからデータを取得します)</summary>
    public delegate* unmanaged[Stdcall]<PIXEL_RGBA*, void> get_image_data;
    /// <summary>現在のオブジェクトの画像データをPIXEL_RGBA形式で設定する (nullの場合は初期データ無しで画像サイズを変更します)</summary>
    public delegate* unmanaged[Stdcall]<PIXEL_RGBA*, int, int, void> set_image_data;
    /// <summary>現在のオブジェクトのD3D画像リソース(ID3D11Texture2D*)を取得する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr> get_image_texture2d;
    /// <summary>現在のフレームバッファのD3D画像リソース(ID3D11Texture2D*)を取得する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr> get_framebuffer_texture2d;
    /// <summary>編集セクション関数 (EDIT_SECTION*) ※フィルタ処理中は参照系の関数が利用出来ます</summary>
    public IntPtr edit;
    /// <summary>現在のオブジェクトの画像パラメータ情報 (直接変更可能)</summary>
    public OBJECT_IMAGE_PARAM* param;
    /// <summary>指定オブジェクトの画像出力項目のパラメータを取得する (object, offset, param, param_size)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, double, OBJECT_IMAGE_PARAM*, int, bool> get_output_image_param;
    /// <summary>指定のレイヤーにある画像オブジェクト(OBJECT_HANDLE)を取得します (layer, offset)</summary>
    public delegate* unmanaged[Stdcall]<int, double, IntPtr> get_image_object;
    /// <summary>指定の画像リソースをフレームバッファに描画します (resource, x,y,z, rx,ry,rz, sx,sy,sz, alpha)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, float, float, float, float, float, float, float, float, float, float, bool> draw_image;
    /// <summary>指定の頂点リストのポリゴンをフレームバッファに描画します (vertex_type, vertex_list, vertex_num, resource)</summary>
    public delegate* unmanaged[Stdcall]<VERTEX_TYPE, void*, int, IntPtr, bool> draw_poly;
    /// <summary>標準のアンカー枠を設定します (width, height)</summary>
    public delegate* unmanaged[Stdcall]<int, int, void> set_default_anchor;
    /// <summary>描画時の合成モードを設定します</summary>
    public delegate* unmanaged[Stdcall]<BLEND_MODE, void> set_blend_mode;
    /// <summary>描画時の光沢度を設定します (0.0〜1.0)</summary>
    public delegate* unmanaged[Stdcall]<float, void> set_material_shine;
    /// <summary>描画時のサンプラーを設定します</summary>
    public delegate* unmanaged[Stdcall]<SAMPLER_MODE, void> set_sampler_mode;
    /// <summary>描画時に裏面を非表示にするかを設定します</summary>
    public delegate* unmanaged[Stdcall]<bool, void> set_culling_state;
    /// <summary>描画時にオブジェクトをカメラの方向に向けるかを設定します</summary>
    public delegate* unmanaged[Stdcall]<BILLBOARD_MODE, void> set_billboard_mode;
    /// <summary>画像リソースを作成する (resource, buffer, width, height)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, PIXEL_RGBA*, int, int, void> create_image_resource;
    /// <summary>指定の画像リソースのD3D画像リソース(ID3D11Texture2D*)を取得する (resource)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr> get_image_resource_texture2d;
    /// <summary>画像リソースをコピーする (dst_resource, src_resource)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> copy_image_resource;
    /// <summary>画像リソースをクリアする (resource, color)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, PIXEL_RGBA, bool> clear_image_resource;
    /// <summary>指定の画像リソースを描画先の画像リソースに描画します (dst, src, x,y,z, rx,ry,rz, sx,sy,sz, alpha)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float, float, float, float, float, float, float, float, float, float, bool> draw_image_to_resource;
    /// <summary>指定の頂点リストのポリゴンを描画先の画像リソースに描画します (dst, vertex_type, vertex_list, vertex_num, src)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, VERTEX_TYPE, void*, int, IntPtr, bool> draw_poly_to_resource;
    /// <summary>[未ラップ] ピクセルシェーダーを実行します (bool (*)(LPCWSTR cso_file, LPCWSTR target, LPCWSTR* resource_list, int, void* constant, int, ID3D11BlendState*, ID3D11SamplerState*))</summary>
    public IntPtr exec_pixelshader_file;
    /// <summary>[未ラップ] コンピュートシェーダーを実行します</summary>
    public IntPtr exec_computeshader_file;
    /// <summary>[未ラップ] 定義済みのD3D出力ブレンド(ID3D11BlendState*)を取得する</summary>
    public IntPtr get_blend_state;
    /// <summary>[未ラップ] 定義済みのD3Dサンプラー(ID3D11SamplerState*)を取得する</summary>
    public IntPtr get_sampler_state;
    /// <summary>[未ラップ] ピクセルシェーダーを実行します (データ指定)</summary>
    public IntPtr exec_pixelshader_data;
    /// <summary>[未ラップ] コンピュートシェーダーを実行します (データ指定)</summary>
    public IntPtr exec_computeshader_data;
    /// <summary>指定の画像リソースのサイズを取得する (resource, width, height)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, int*, int*, bool> get_image_resource_size;
    /// <summary>画像リソースから指定フォーマットの画像データを取得する (resource, buffer, width, height, pitch, format)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, int, int, int, OUTPUT_PIXEL_FORMAT, bool> get_image_resource_data;
    /// <summary>画像リソースに指定フォーマットの画像データを設定する (resource, buffer, width, height, pitch, format)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, int, int, int, INPUT_PIXEL_FORMAT, bool> set_image_resource_data;
    /// <summary>冗長なので廃止 (呼び出さないこと)</summary>
    public IntPtr deprecated_get_font;
}

/// <summary>
/// 音声フィルタ処理用構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FILTER_PROC_AUDIO
{
    /// <summary>シーン情報</summary>
    public SCENE_INFO* scene;
    /// <summary>オブジェクト情報</summary>
    public OBJECT_INFO* @object;
    /// <summary>現在のオブジェクトの音声データを取得する (buffer, channel: 0=左/1=右) ※PCM(float)32bit</summary>
    public delegate* unmanaged[Stdcall]<float*, int, void> get_sample_data;
    /// <summary>現在のオブジェクトの音声データを設定する (buffer, channel: 0=左/1=右) ※PCM(float)32bit</summary>
    public delegate* unmanaged[Stdcall]<float*, int, void> set_sample_data;
    /// <summary>編集セクション関数 (EDIT_SECTION*) ※フィルタ処理中は参照系の関数が利用出来ます</summary>
    public IntPtr edit;
    /// <summary>現在のオブジェクトの音声パラメータ情報 (直接変更可能)</summary>
    public OBJECT_AUDIO_PARAM* param;
    /// <summary>指定オブジェクトの音声出力項目のパラメータを取得する (object, offset, param, param_size)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, double, OBJECT_AUDIO_PARAM*, int, bool> get_output_audio_param;
    /// <summary>指定のレイヤー位置にある音声オブジェクト(OBJECT_HANDLE)を取得します (layer, offset)</summary>
    public delegate* unmanaged[Stdcall]<int, double, IntPtr> get_audio_object;
}

/// <summary>
/// フィルタプラグイン構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct FILTER_PLUGIN_TABLE
{
    /// <summary>
    /// フラグ
    /// <see cref="FilterPluginTableFlag"/>
    /// </summary>
    public FilterPluginTableFlag flag;
    /// <summary>プラグインの名前 (LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>ラベルの初期値 (nullならデフォルトのラベルになります, LPCWSTR)</summary>
    public IntPtr label;
    /// <summary>プラグインの情報 (LPCWSTR)</summary>
    public IntPtr information;
    /// <summary>設定項目の定義 (FILTER_ITEM_XXXポインタを列挙してnull終端したリストへのポインタ)</summary>
    public void** items;
    /// <summary>
    /// 画像フィルタ処理関数へのポインタ (FLAG_VIDEOが有効の時のみ呼ばれます)
    /// <param name="video">FILTER_PROC_VIDEO*</param>
    /// <returns>falseを返却すると以降のフィルタや出力処理が中断されます</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_proc_video;
    /// <summary>
    /// 音声フィルタ処理関数へのポインタ (FLAG_AUDIOが有効の時のみ呼ばれます)
    /// <param name="audio">FILTER_PROC_AUDIO*</param>
    /// <returns>falseを返却すると以降のフィルタや出力処理が中断されます</returns>
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool> func_proc_audio;
}
