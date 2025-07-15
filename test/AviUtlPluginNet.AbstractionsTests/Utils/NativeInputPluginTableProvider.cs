using System.Runtime.InteropServices;

namespace AviUtlPluginNet.AbstractionsTests.Utils;

public class NativePluginTableProviderDynamic : INativeInputPluginTableProvider, IDisposable
{
    private IntPtr _libHandle;
    private delegate IntPtr GetInputPluginTableDelegate();
    private GetInputPluginTableDelegate? _getInputPluginTable;

    public NativePluginTableProviderDynamic(string dllPath)
    {
        _libHandle = NativeLibrary.Load(dllPath);
        var proc = NativeLibrary.GetExport(_libHandle, "GetInputPluginTable");
        _getInputPluginTable = Marshal.GetDelegateForFunctionPointer<GetInputPluginTableDelegate>(proc);
    }

    public IntPtr GetInputPluginTable()
    {
        if (_getInputPluginTable == null)
            throw new InvalidOperationException("Native function not loaded");
        return _getInputPluginTable();
    }

    public void Dispose()
    {
        if (_libHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libHandle);
            _libHandle = IntPtr.Zero;
        }
    }
}