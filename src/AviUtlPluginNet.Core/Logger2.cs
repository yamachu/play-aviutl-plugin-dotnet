using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Logger2;

/// <summary>
/// ログ出力ハンドル
/// ログ出力は1024文字で制限されます
/// 各種プラグインで InitializeLogger(LOG_HANDLE* logger) を外部公開すると呼び出されます
/// ※InitializePlugin()より先に呼ばれます
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct LOG_HANDLE
{
    /// <summary>
    /// プラグイン用のログを出力します
    /// <param name="handle">ログ出力ハンドル</param>
    /// <param name="message">ログメッセージ(LPCWSTR)</param>
    /// </summary>
    public delegate* unmanaged[Stdcall]<LOG_HANDLE*, IntPtr, void> log;
    /// <summary>
    /// infoレベルのログを出力します
    /// </summary>
    public delegate* unmanaged[Stdcall]<LOG_HANDLE*, IntPtr, void> info;
    /// <summary>
    /// warnレベルのログを出力します
    /// </summary>
    public delegate* unmanaged[Stdcall]<LOG_HANDLE*, IntPtr, void> warn;
    /// <summary>
    /// errorレベルのログを出力します
    /// </summary>
    public delegate* unmanaged[Stdcall]<LOG_HANDLE*, IntPtr, void> error;
    /// <summary>
    /// verboseレベルのログを出力します
    /// </summary>
    public delegate* unmanaged[Stdcall]<LOG_HANDLE*, IntPtr, void> verbose;
}
