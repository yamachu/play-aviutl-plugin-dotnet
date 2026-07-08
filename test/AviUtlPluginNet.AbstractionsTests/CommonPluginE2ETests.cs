using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.Logger2;
using AviUtlPluginNet.Core.Interop.Plugin2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class CommonPluginE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public CommonPluginE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();
        PluginFixture.BuildTestPlugin(GetCommonPluginImplementation());
    }

    private static string GetCommonPluginImplementation()
    {
        return """
        using System;
        using AviUtlPluginNet.Abstractions;

        namespace AviUtlPluginNet.Example;

        [AviUtl2Plugin]
        class TestCommonPlugin : ICommonPlugin, IUseLogger
        {
            public static string Name => ".NET Common Plugin";
            public static string Information => ".NET Common Plugin Example";

            private ILogger2? _logger;

            public void AttachLogger(ILogger2 logger)
            {
                _logger = logger;
            }

            public void Register(IHostApp host)
            {
                host.RegisterWindowClient("MyWindow", new IntPtr(0x1000));

                host.RegisterImportMenu("My Import", () =>
                {
                    _logger?.Info("import menu invoked");
                });

                host.RegisterFileDropHandler("My Drop", "All Files (*.*)\0*.*\0", file =>
                {
                    _logger?.Info($"file dropped: {file}");
                });
            }
        }
        """;
    }

    public void Dispose()
    {
        PluginFixture.Dispose();
    }
}

/// <summary>
/// 汎用プラグインのNativeAOT E2Eテスト
/// ホスト側のHOST_APP_TABLEをテストコード側で模擬し、
/// RegisterPluginでの登録内容と登録したコールバックの呼び出しを検証する
/// </summary>
public class CommonPluginE2ETests : IClassFixture<CommonPluginE2ETestsFixture>, IDisposable
{
    private readonly NativePluginLibrary _library;

    // ホスト側の状態(UnmanagedCallersOnlyから参照するためstatic)
    private static string? _registeredWindowName;
    private static IntPtr _registeredWindowHwnd;
    private static string? _registeredImportMenuName;
    private static IntPtr _importMenuParam;
    private static IntPtr _importMenuFunc;
    private static string? _registeredDropName;
    private static string? _registeredDropFilter;
    private static IntPtr _dropParam;
    private static IntPtr _dropFunc;
    private static readonly List<string> _logs = new();

    public CommonPluginE2ETests(CommonPluginE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);
    }

    /// <summary>
    /// GetCommonPluginTable エントリーポイントのテスト
    /// </summary>
    [Fact]
    public void GetCommonPluginTable_ShouldReturnNameAndInformation()
    {
        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetCommonPluginTable");
            var table = (COMMON_PLUGIN_TABLE*)getTable();
            Assert.False(table == null);

            Assert.Equal(".NET Common Plugin", Marshal.PtrToStringUni(table->name));
            Assert.Equal(".NET Common Plugin Example", Marshal.PtrToStringUni(table->information));
        }
    }

    /// <summary>
    /// RegisterPlugin のE2Eテスト
    /// IHostApp経由の登録がHOST_APP_TABLEへ届き、登録されたコールバックが後から呼び出せることを検証する
    /// </summary>
    [Fact]
    public void RegisterPlugin_ShouldRegisterMenusAndCallbacksShouldWork()
    {
        _registeredWindowName = null;
        _registeredImportMenuName = null;
        _registeredDropName = null;
        _logs.Clear();

        unsafe
        {
            // ログ捕捉用のLOG_HANDLEを注入 (コールバック実行の観測用)
            var logHandle = (LOG_HANDLE*)Marshal.AllocHGlobal(sizeof(LOG_HANDLE));
            var hostTable = (HOST_APP_TABLE*)Marshal.AllocHGlobal(sizeof(HOST_APP_TABLE));
            try
            {
                logHandle->log = &CaptureLog;
                logHandle->info = &CaptureLog;
                logHandle->warn = &CaptureLog;
                logHandle->error = &CaptureLog;
                logHandle->verbose = &CaptureLog;
                var initializeLogger = (delegate* unmanaged[Stdcall]<IntPtr, void>)_library.GetExport("InitializeLogger");
                initializeLogger((IntPtr)logHandle);

                // HOST_APP_TABLEを構築してRegisterPluginを呼ぶ
                *hostTable = default;
                hostTable->register_window_client = &HostRegisterWindowClient;
                hostTable->register_import_menu_param = &HostRegisterImportMenuParam;
                hostTable->register_file_drop_param_handler = &HostRegisterFileDropParamHandler;

                var registerPlugin = (delegate* unmanaged[Stdcall]<IntPtr, void>)_library.GetExport("RegisterPlugin");
                registerPlugin((IntPtr)hostTable);

                // 登録内容の検証
                Assert.Equal("MyWindow", _registeredWindowName);
                Assert.Equal(new IntPtr(0x1000), _registeredWindowHwnd);
                Assert.Equal("My Import", _registeredImportMenuName);
                Assert.NotEqual(IntPtr.Zero, _importMenuFunc);
                Assert.Equal("My Drop", _registeredDropName);
                Assert.StartsWith("All Files", _registeredDropFilter);
                Assert.NotEqual(IntPtr.Zero, _dropFunc);

                // 登録されたコールバックをホスト側から呼び出す(メニュー選択の模擬)
                var importCallback = (delegate* unmanaged[Stdcall]<void*, void>)_importMenuFunc;
                importCallback((void*)_importMenuParam);
                Assert.Contains(_logs, m => m.Contains("import menu invoked"));

                // ファイルD&Dの模擬
                var dropCallback = (delegate* unmanaged[Stdcall]<void*, IntPtr, void>)_dropFunc;
                var filePtr = Marshal.StringToHGlobalUni("/tmp/dropped.png");
                try
                {
                    dropCallback((void*)_dropParam, filePtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(filePtr);
                }
                Assert.Contains(_logs, m => m.Contains("file dropped: /tmp/dropped.png"));
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)logHandle);
                Marshal.FreeHGlobal((IntPtr)hostTable);
            }
        }
    }

    #region ホスト側コールバック(HOST_APP_TABLE / LOG_HANDLEの関数ポインタ)

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void CaptureLog(LOG_HANDLE* handle, IntPtr message)
    {
        var text = Marshal.PtrToStringUni(message);
        if (text != null)
        {
            lock (_logs)
            {
                _logs.Add(text);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostRegisterWindowClient(IntPtr name, IntPtr hwnd)
    {
        _registeredWindowName = Marshal.PtrToStringUni(name);
        _registeredWindowHwnd = hwnd;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostRegisterImportMenuParam(IntPtr name, void* param, delegate* unmanaged[Stdcall]<void*, void> func)
    {
        _registeredImportMenuName = Marshal.PtrToStringUni(name);
        _importMenuParam = (IntPtr)param;
        _importMenuFunc = (IntPtr)func;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostRegisterFileDropParamHandler(IntPtr name, IntPtr filefilter, void* param, delegate* unmanaged[Stdcall]<void*, IntPtr, void> func)
    {
        _registeredDropName = Marshal.PtrToStringUni(name);
        _registeredDropFilter = Marshal.PtrToStringUni(filefilter);
        _dropParam = (IntPtr)param;
        _dropFunc = (IntPtr)func;
    }

    #endregion

    public void Dispose()
    {
        _library.Dispose();
    }
}
