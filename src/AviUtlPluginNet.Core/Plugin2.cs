using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Plugin2;

/// <summary>
/// 汎用プラグイン構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct COMMON_PLUGIN_TABLE
{
    /// <summary>プラグインの名前(LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>プラグインの情報(LPCWSTR)</summary>
    public IntPtr information;
}

/// <summary>
/// イベント種別
/// </summary>
public enum EVENT_TYPE : int
{
    /// <summary>オブジェクト情報の更新</summary>
    UPDATE_OBJECT = 1,
    /// <summary>現在の編集フレームの移動</summary>
    CHANGE_EDIT_FRAME = 2,
    /// <summary>現在の編集シーンの変更 ※シーン情報の更新も含まれる</summary>
    CHANGE_EDIT_SCENE = 3,
}

/// <summary>
/// ホストアプリケーション構造体
/// 汎用プラグインの RegisterPlugin(HOST_APP_TABLE* host) に渡されます
/// ※EDIT_SECTION*/EDIT_HANDLE*/PROJECT_FILE* 等の未ラップの構造体はIntPtrで表現している
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct HOST_APP_TABLE
{
    /// <summary>
    /// プラグインの情報を設定する ※現在はGetCommonPluginTable()を利用する方法が推奨
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> set_plugin_information;
    /// <summary>入力プラグインを登録する (INPUT_PLUGIN_TABLE*)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_input_plugin;
    /// <summary>出力プラグインを登録する (OUTPUT_PLUGIN_TABLE*)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_output_plugin;
    /// <summary>フィルタプラグインを登録する (FILTER_PLUGIN_TABLE*)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_filter_plugin;
    /// <summary>スクリプトモジュールを登録する (SCRIPT_MODULE_TABLE*)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_script_module;
    /// <summary>インポートメニューを登録する (name, void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_import_menu;
    /// <summary>エクスポートメニューを登録する (name, void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_export_menu;
    /// <summary>
    /// ウィンドウクライアントを登録する (name, HWND)
    /// ウィンドウにはWS_CHILDが追加され親ウィンドウが設定されます ※WS_POPUPは削除されます
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_window_client;
    /// <summary>プロジェクトデータ編集用のハンドル(EDIT_HANDLE*)を取得します</summary>
    public delegate* unmanaged[Stdcall]<IntPtr> create_edit_handle;
    /// <summary>プロジェクトファイルをロードした直後に呼ばれる関数を登録する (void(*)(PROJECT_FILE*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_project_load_handler;
    /// <summary>プロジェクトファイルをセーブする直前に呼ばれる関数を登録する (void(*)(PROJECT_FILE*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_project_save_handler;
    /// <summary>レイヤーメニューを登録する (name, void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_layer_menu;
    /// <summary>オブジェクトメニューを登録する (name, void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_object_menu;
    /// <summary>
    /// 設定メニューを登録する (name, void(*)(HWND, HINSTANCE))
    /// 設定メニューの登録後にウィンドウクライアントを登録するとシステムメニューに「設定」が追加されます
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>, void> register_config_menu;
    /// <summary>編集メニューを登録する (name, void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_edit_menu;
    /// <summary>キャッシュを破棄の操作時に呼ばれる関数を登録する (void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_clear_cache_handler;
    /// <summary>シーンを変更した直後に呼ばれる関数を登録する (void(*)(EDIT_SECTION*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_change_scene_handler;
    /// <summary>インポートメニューを登録する (name, param, void(*)(void* param))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_import_menu_param;
    /// <summary>エクスポートメニューを登録する (name, param, void(*)(void* param))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_export_menu_param;
    /// <summary>レイヤーメニューを登録する (name, param, void(*)(void* param))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_layer_menu_param;
    /// <summary>オブジェクトメニューを登録する (name, param, void(*)(void* param))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_object_menu_param;
    /// <summary>編集メニューを登録する (name, param, void(*)(void* param))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_edit_menu_param;
    /// <summary>ファイルをD&amp;Dした時に呼ばれる関数を登録する (name, filefilter, void(*)(EDIT_SECTION*, LPCWSTR file))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void> register_file_drop_handler;
    /// <summary>ファイルをD&amp;Dした時に呼ばれる関数を登録する (name, filefilter, param, void(*)(void* param, LPCWSTR file))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void*, delegate* unmanaged[Stdcall]<void*, IntPtr, void>, void> register_file_drop_param_handler;
    /// <summary>オブジェクト編集の設定項目メニューを登録する (name, allow_effect_only, void(*)(EDIT_SECTION*, OBJECT_HANDLE, LPCWSTR, LPCWSTR))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool, IntPtr, void> register_object_item_menu;
    /// <summary>オブジェクト編集の設定項目メニューを登録する (name, allow_effect_only, param, void(*)(void* param, OBJECT_HANDLE, LPCWSTR, LPCWSTR))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, bool, void*, delegate* unmanaged[Stdcall]<void*, IntPtr, IntPtr, IntPtr, void>, void> register_object_item_menu_param;
    /// <summary>スクリプトモジュールをモジュール名を指定して登録する (SCRIPT_MODULE_TABLE*, module_name)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void> register_script_module_name;
    /// <summary>フォントコレクションを登録する (IDWriteFontCollection*)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> register_font_collection;
    /// <summary>
    /// 指定のイベントのコールバック関数を登録する (type, param, void(*)(void* param))
    /// コールバック関数はイベント用スレッドから呼ばれます
    /// イベント処理からcall_edit_section()は利用出来ません
    /// </summary>
    public delegate* unmanaged[Stdcall]<EVENT_TYPE, void*, delegate* unmanaged[Stdcall]<void*, void>, void> register_event_listener;
}
