using System.Runtime.InteropServices;

namespace AviUtlPluginNet.AbstractionsTests.Utils;

/// <summary>
/// NativeAOTでビルドされたプラグインのネイティブライブラリをロードし、
/// 任意のエクスポート関数へアクセスするためのヘルパー
/// </summary>
public sealed class NativePluginLibrary : IDisposable
{
    private IntPtr _libHandle;

    public NativePluginLibrary(string dllPath)
    {
        _libHandle = NativeLibrary.Load(dllPath);
    }

    /// <summary>
    /// エクスポート関数のアドレスを取得します (存在しない場合は例外)
    /// </summary>
    public IntPtr GetExport(string name)
        => NativeLibrary.GetExport(_libHandle, name);

    /// <summary>
    /// エクスポート関数のアドレスの取得を試みます
    /// </summary>
    public bool TryGetExport(string name, out IntPtr address)
        => NativeLibrary.TryGetExport(_libHandle, name, out address);

    public void Dispose()
    {
        if (_libHandle != IntPtr.Zero)
        {
            NativeLibrary.Free(_libHandle);
            _libHandle = IntPtr.Zero;
        }
    }
}
