namespace AviUtlPluginNet.Abstractions;

using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.AUI2;

/// <summary>
/// AviUtl2 Inputプラグインのハンドルインターフェース
/// </summary>
public interface IInputHandle : IDisposable;

/// <summary>
/// 入力プラグインAPIの共通インターフェース
/// </summary>
public interface IInputPluginAPI
{
    static abstract ref INPUT_PLUGIN_TABLE pluginTable { get; }
    static abstract ref IntPtr pluginTablePtr { get; }

    #region Public API
    IInputHandle? FuncOpen(string file);
    bool FuncClose(IInputHandle ih);
    bool FuncInfoGet(IInputHandle ih, out INPUT_INFO? info);
    Span<byte> FuncReadVideo(IInputHandle ih, int frame);
    Span<byte> FuncReadAudio(IInputHandle ih, int start, int length);
    bool FuncConfig(IntPtr hwnd, IntPtr hInstance);
    #endregion

    public static IntPtr GetPluginTablePtr<TPlugin>() where TPlugin : IInputPluginAPI
        => TPlugin.pluginTablePtr;
}

/// <summary>
/// 入力プラグインの共通ジェネリックインターフェース
/// </summary>
public interface IInputPlugin<TVideo, TAudio> : IInputPluginAPI
    where TVideo : IInputHandle
    where TAudio : IInputHandle
{
    #region Plugin Metadata
    static abstract string name { get; }
    static abstract string fileFilter { get; }
    static abstract string information { get; }
    #endregion

    #region Internal
    static IntPtr _pluginTablePtr;
    #endregion

    unsafe static void InitPluginTable<TPlugin>(
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr> funcOpen,
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcClose,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcInfoGet,
        delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> funcReadVideo,
        delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr, int> funcReadAudio,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig
    ) where TPlugin : IInputPlugin<TVideo, TAudio>
    {
        // 既存領域の解放
        if (_pluginTablePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_pluginTablePtr);
            _pluginTablePtr = IntPtr.Zero;
        }
        // INPUT_PLUGIN_TABLE用のアンマネージド領域を確保
        _pluginTablePtr = Marshal.AllocHGlobal(sizeof(INPUT_PLUGIN_TABLE));
        var namePtr = Marshal.StringToHGlobalUni(TPlugin.name);
        var fileFilterPtr = Marshal.StringToHGlobalUni(TPlugin.fileFilter);
        var informationPtr = Marshal.StringToHGlobalUni(TPlugin.information);
        var table = (INPUT_PLUGIN_TABLE*)_pluginTablePtr;
        table->flag = InputPluginTableFlag.None;
        table->name = namePtr;
        table->filefilter = fileFilterPtr;
        table->information = informationPtr;
        table->func_open = funcOpen;
        table->func_close = funcClose;
        table->func_info_get = funcInfoGet;
        table->func_read_video = funcReadVideo;
        table->func_read_audio = funcReadAudio;
        table->func_config = funcConfig;
    }

    unsafe static ref INPUT_PLUGIN_TABLE IInputPluginAPI.pluginTable
        => ref *(INPUT_PLUGIN_TABLE*)_pluginTablePtr;
    static ref IntPtr IInputPluginAPI.pluginTablePtr => ref _pluginTablePtr;
}

/// <summary>
/// 映像入力プラグイン（設定ダイアログなし）用インターフェース
/// </summary>
public interface IInputVideoPluginWithoutConfig<T> : IInputPlugin<T, IInputHandle> where T : IInputHandle
{
    unsafe static new void InitPluginTable<TPlugin>(
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr> funcOpen,
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcClose,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcInfoGet,
        delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> funcReadVideo,
        delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr, int> funcReadAudio,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig
    ) where TPlugin : IInputVideoPluginWithoutConfig<T>
    {
        IInputPlugin<T, IInputHandle>.InitPluginTable<TPlugin>(funcOpen, funcClose, funcInfoGet, funcReadVideo, funcReadAudio, funcConfig);
        ref var table = ref TPlugin.pluginTable;
        table.flag = InputPluginTableFlag.Video;
        table.func_config = null; // 設定ダイアログなし
    }

    #region Public Type-Safe API
    bool FuncClose(T ih);
    bool FuncInfoGet(T ih, out INPUT_INFO? info);
    Span<byte> FuncReadVideo(T ih, int n);
    #endregion

    bool IInputPluginAPI.FuncClose(IInputHandle ih)
        => ih is T t && FuncClose(t);

    bool IInputPluginAPI.FuncInfoGet(IInputHandle ih, out INPUT_INFO? info)
    {
        if (ih is T t)
            return FuncInfoGet(t, out info);
        info = null;
        return false;
    }

    Span<byte> IInputPluginAPI.FuncReadVideo(IInputHandle ih, int n)
        => ih is T t ? FuncReadVideo(t, n) : Span<byte>.Empty;

    Span<byte> IInputPluginAPI.FuncReadAudio(IInputHandle ih, int start, int length)
        => Span<byte>.Empty; // 映像プラグインは音声未対応

    bool IInputPluginAPI.FuncConfig(IntPtr hwnd, IntPtr hInstance)
        => false; // 設定ダイアログなし
}

/// <summary>
/// 音声入力プラグイン（設定ダイアログなし）用インターフェース
/// </summary>
public interface IInputAudioPluginWithoutConfig<T> : IInputPlugin<IInputHandle, T> where T : IInputHandle
{
    unsafe static new void InitPluginTable<TPlugin>(
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr> funcOpen,
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcClose,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcInfoGet,
        delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr, int> funcReadVideo,
        delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr, int> funcReadAudio,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig
    ) where TPlugin : IInputAudioPluginWithoutConfig<T>
    {
        IInputPlugin<IInputHandle, T>.InitPluginTable<TPlugin>(funcOpen, funcClose, funcInfoGet, funcReadVideo, funcReadAudio, funcConfig);
        ref var table = ref TPlugin.pluginTable;
        table.flag = InputPluginTableFlag.Audio;
        table.func_config = null; // 設定ダイアログなし
    }

    #region Public Type-Safe API
    bool FuncClose(T ih);
    bool FuncInfoGet(T ih, out INPUT_INFO? info);
    Span<byte> FuncReadAudio(T ih, int start, int length);
    #endregion

    bool IInputPluginAPI.FuncClose(IInputHandle ih)
        => ih is T t && FuncClose(t);

    bool IInputPluginAPI.FuncInfoGet(IInputHandle ih, out INPUT_INFO? info)
    {
        if (ih is T t)
            return FuncInfoGet(t, out info);
        info = null;
        return false;
    }

    Span<byte> IInputPluginAPI.FuncReadVideo(IInputHandle ih, int n)
        => Span<byte>.Empty; // 音声プラグインは映像未対応

    Span<byte> IInputPluginAPI.FuncReadAudio(IInputHandle ih, int start, int length)
        => ih is T t ? FuncReadAudio(t, start, length) : Span<byte>.Empty;

    bool IInputPluginAPI.FuncConfig(IntPtr hwnd, IntPtr hInstance)
        => false; // 設定ダイアログなし
}
