namespace AviUtlPluginNet.Abstractions;

using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.AUO2;

/// <summary>
/// 出力プラグインAPIの共通インターフェース
/// </summary>
public interface IOutputPluginAPI
{
    static abstract ref OUTPUT_PLUGIN_TABLE pluginTable { get; }
    static abstract ref IntPtr pluginTablePtr { get; }

    #region Public API
    bool FuncOutput(in OUTPUT_INFO outputInfo);
    bool FuncConfig(IntPtr hwnd, IntPtr dllHinst);
    string FuncGetConfigText();
    #endregion

    public static IntPtr GetPluginTablePtr<TPlugin>() where TPlugin : IOutputPluginAPI
        => TPlugin.pluginTablePtr;
}

/// <summary>
/// 出力プラグインの共通ジェネリックインターフェース
/// </summary>
public interface IOutputPlugin : IOutputPluginAPI
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
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcOutput,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig,
        delegate* unmanaged[Stdcall]<IntPtr> funcGetConfigText
    ) where TPlugin : IOutputPlugin
    {
        // 既存領域の解放
        if (_pluginTablePtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_pluginTablePtr);
            _pluginTablePtr = IntPtr.Zero;
        }
        // OUTPUT_PLUGIN_TABLE用のアンマネージド領域を確保
        _pluginTablePtr = Marshal.AllocHGlobal(sizeof(OUTPUT_PLUGIN_TABLE));
        var namePtr = Marshal.StringToHGlobalUni(TPlugin.name);
        var fileFilterPtr = Marshal.StringToHGlobalUni(TPlugin.fileFilter);
        var informationPtr = Marshal.StringToHGlobalUni(TPlugin.information);
        var table = (OUTPUT_PLUGIN_TABLE*)_pluginTablePtr;
        table->flag = OutputPluginTableFlag.None;
        table->name = namePtr;
        table->filefilter = fileFilterPtr;
        table->information = informationPtr;
        table->func_output = funcOutput;
        table->func_config = funcConfig;
        table->func_get_config_text = funcGetConfigText;
    }

    unsafe static ref OUTPUT_PLUGIN_TABLE IOutputPluginAPI.pluginTable
        => ref *(OUTPUT_PLUGIN_TABLE*)_pluginTablePtr;
    static ref IntPtr IOutputPluginAPI.pluginTablePtr => ref _pluginTablePtr;
}

public interface IOutputPluginWithoutConfig : IOutputPlugin
{
    unsafe static new void InitPluginTable<TPlugin>(
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcOutput,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig,
        delegate* unmanaged[Stdcall]<IntPtr> funcGetConfigText
    ) where TPlugin : IOutputPlugin
    {
        IOutputPlugin.InitPluginTable<TPlugin>(funcOutput, funcConfig, funcGetConfigText);
        ref var table = ref TPlugin.pluginTable;
        table.func_config = null; // 設定ダイアログなし
    }

    bool IOutputPluginAPI.FuncConfig(IntPtr hwnd, IntPtr dllHinst)
        => false; // 設定ダイアログなし
}

public interface IOutputPluginWithoutConfigText : IOutputPlugin
{
    unsafe static new void InitPluginTable<TPlugin>(
        delegate* unmanaged[Stdcall]<IntPtr, bool> funcOutput,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, bool> funcConfig,
        delegate* unmanaged[Stdcall]<IntPtr> funcGetConfigText
    ) where TPlugin : IOutputPlugin
    {
        IOutputPlugin.InitPluginTable<TPlugin>(funcOutput, funcConfig, funcGetConfigText);
        ref var table = ref TPlugin.pluginTable;
        table.func_get_config_text = null;
    }

    string IOutputPluginAPI.FuncGetConfigText()
        => string.Empty;
}

// TODO: Test both interfaces call
