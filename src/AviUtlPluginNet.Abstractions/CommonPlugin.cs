namespace AviUtlPluginNet.Abstractions;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.Plugin2;

/// <summary>
/// 汎用プラグインの種別インターフェース (plugin2.h / GetCommonPluginTable + RegisterPlugin)
/// </summary>
public interface ICommonPlugin : IAviUtl2Plugin
{
    /// <summary>
    /// プラグイン登録時に呼ばれます (export: RegisterPlugin)
    /// メニュー登録・ウィンドウクライアント登録などをここで行います
    /// </summary>
    void Register(IHostApp host);
}

/// <summary>
/// ホストアプリケーション機能 (HOST_APP_TABLEのラップ)
/// EDIT_SECTIONを引数に取るコールバック等の未ラップAPIは <see cref="RawHandle"/> 経由で利用できます
/// </summary>
public interface IHostApp
{
    /// <summary>
    /// ウィンドウクライアントを登録します
    /// ウィンドウにはWS_CHILDが追加され親ウィンドウが設定されます ※WS_POPUPは削除されます
    /// </summary>
    /// <param name="name">ウィンドウの名称</param>
    /// <param name="hwnd">ウィンドウハンドル</param>
    void RegisterWindowClient(string name, IntPtr hwnd);

    /// <summary>
    /// インポートメニューを登録します (ウィンドウメニューのファイルに追加されます)
    /// </summary>
    void RegisterImportMenu(string name, Action callback);

    /// <summary>
    /// エクスポートメニューを登録します (ウィンドウメニューのファイルに追加されます)
    /// </summary>
    void RegisterExportMenu(string name, Action callback);

    /// <summary>
    /// レイヤーメニューを登録します (レイヤー編集でオブジェクト未選択時の右クリックメニューに追加されます)
    /// ※名称に'\'を入れると表示を複数階層に出来ます
    /// </summary>
    void RegisterLayerMenu(string name, Action callback);

    /// <summary>
    /// オブジェクトメニューを登録します (レイヤー編集でオブジェクト選択時の右クリックメニューに追加されます)
    /// ※名称に'\'を入れると表示を複数階層に出来ます
    /// </summary>
    void RegisterObjectMenu(string name, Action callback);

    /// <summary>
    /// 編集メニューを登録します ※名称に'\'を入れると表示を階層に出来ます
    /// </summary>
    void RegisterEditMenu(string name, Action callback);

    /// <summary>
    /// 設定メニューを登録します
    /// 設定メニューの登録後にウィンドウクライアントを登録するとシステムメニューに「設定」が追加されます
    /// ※ネイティブAPIにユーザーデータ引数が無いため、登録できるのは1プラグインにつき1つです
    /// </summary>
    /// <param name="name">設定メニューの名称</param>
    /// <param name="callback">(hwnd, dllHInstance) を受け取るコールバック</param>
    void RegisterConfigMenu(string name, Action<IntPtr, IntPtr> callback);

    /// <summary>
    /// ファイルをD&amp;Dした時に呼ばれる関数を登録します
    /// </summary>
    /// <param name="name">ドラッグ時のツールチップや入力プラグインの設定で表示する名称</param>
    /// <param name="fileFilter">D&amp;Dに対応するファイルフィルタ</param>
    /// <param name="callback">ファイルパスを受け取るコールバック</param>
    void RegisterFileDropHandler(string name, string fileFilter, Action<string> callback);

    /// <summary>
    /// 指定のイベントのコールバック関数を登録します
    /// コールバック関数はイベント用スレッドから呼ばれます
    /// </summary>
    void RegisterEventListener(EVENT_TYPE type, Action callback);

    /// <summary>
    /// HOST_APP_TABLE* の生ポインタ (未ラップAPI用)
    /// </summary>
    IntPtr RawHandle { get; }
}

/// <summary>
/// HOST_APP_TABLE* をラップした <see cref="IHostApp"/> の実装
/// コールバックはGCHandle経由でネイティブ側のユーザーデータ引数に載せて保持します(解放しない=アプリ生存期間)
/// </summary>
public sealed unsafe class NativeHostApp : IHostApp
{
    private readonly HOST_APP_TABLE* _handle;

    // register_config_menuにはユーザーデータ引数が無いためstaticな1スロットで保持する
    private static Action<IntPtr, IntPtr>? _configMenuCallback;

    public NativeHostApp(IntPtr handle)
    {
        _handle = (HOST_APP_TABLE*)handle;
    }

    public IntPtr RawHandle => (IntPtr)_handle;

    public void RegisterWindowClient(string name, IntPtr hwnd)
        => _handle->register_window_client(PersistentUni(name), hwnd);

    public void RegisterImportMenu(string name, Action callback)
        => _handle->register_import_menu_param(PersistentUni(name), Pin(callback), &ActionTrampoline);

    public void RegisterExportMenu(string name, Action callback)
        => _handle->register_export_menu_param(PersistentUni(name), Pin(callback), &ActionTrampoline);

    public void RegisterLayerMenu(string name, Action callback)
        => _handle->register_layer_menu_param(PersistentUni(name), Pin(callback), &ActionTrampoline);

    public void RegisterObjectMenu(string name, Action callback)
        => _handle->register_object_menu_param(PersistentUni(name), Pin(callback), &ActionTrampoline);

    public void RegisterEditMenu(string name, Action callback)
        => _handle->register_edit_menu_param(PersistentUni(name), Pin(callback), &ActionTrampoline);

    public void RegisterConfigMenu(string name, Action<IntPtr, IntPtr> callback)
    {
        if (_configMenuCallback != null)
        {
            throw new InvalidOperationException("設定メニューは1つのみ登録できます");
        }
        _configMenuCallback = callback;
        _handle->register_config_menu(PersistentUni(name), &ConfigMenuTrampoline);
    }

    public void RegisterFileDropHandler(string name, string fileFilter, Action<string> callback)
        => _handle->register_file_drop_param_handler(PersistentUni(name), PersistentUni(fileFilter), Pin(callback), &FileDropTrampoline);

    public void RegisterEventListener(EVENT_TYPE type, Action callback)
        => _handle->register_event_listener(type, Pin(callback), &ActionTrampoline);

    /// <summary>
    /// 登録用文字列をアンマネージド領域に確保する
    /// ホスト側の保持期間が不明なためアプリ生存期間で保持する(解放しない)
    /// </summary>
    private static IntPtr PersistentUni(string value)
        => Marshal.StringToHGlobalUni(value);

    /// <summary>
    /// コールバックをGCHandleで固定しユーザーデータポインタとして返す(解放しない=アプリ生存期間)
    /// </summary>
    private static void* Pin(Delegate callback)
        => (void*)GCHandle.ToIntPtr(GCHandle.Alloc(callback));

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void ActionTrampoline(void* param)
    {
        try
        {
            (GCHandle.FromIntPtr((IntPtr)param).Target as Action)?.Invoke();
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void FileDropTrampoline(void* param, IntPtr file)
    {
        try
        {
            var path = Marshal.PtrToStringUni(file);
            if (path != null)
            {
                (GCHandle.FromIntPtr((IntPtr)param).Target as Action<string>)?.Invoke(path);
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void ConfigMenuTrampoline(IntPtr hwnd, IntPtr dllHInstance)
    {
        try
        {
            _configMenuCallback?.Invoke(hwnd, dllHInstance);
        }
        catch
        {
        }
    }
}
