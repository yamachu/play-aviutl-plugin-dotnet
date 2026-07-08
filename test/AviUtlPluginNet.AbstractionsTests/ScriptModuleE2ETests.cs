using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.Module2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class ScriptModuleE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public ScriptModuleE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();
        PluginFixture.BuildTestPlugin(GetScriptModuleImplementation());
    }

    private static string GetScriptModuleImplementation()
    {
        return """
        using System;
        using AviUtlPluginNet.Abstractions;

        namespace AviUtlPluginNet.Example;

        [AviUtl2Plugin]
        class TestScriptModule : IScriptModulePlugin
        {
            public static string Information => ".NET Script Module Example";

            // メソッド名がそのまま関数名になる
            [ScriptFunction]
            public void Sum(ScriptModuleContext context)
            {
                double total = 0.0;
                var count = context.ParamCount;
                for (int i = 0; i < count; i++)
                {
                    total += context.GetDouble(i);
                }
                context.PushResult(total);
            }

            // 属性で関数名を明示できる
            [ScriptFunction("concat")]
            public void ConcatStrings(ScriptModuleContext context)
            {
                var a = context.GetString(0) ?? "";
                var b = context.GetString(1) ?? "";
                context.PushResult(a + b);
            }

            // 例外はSetErrorに変換される
            [ScriptFunction]
            public void Fail(ScriptModuleContext context)
            {
                throw new InvalidOperationException("boom");
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
/// スクリプトモジュールのNativeAOT E2Eテスト
/// ホスト側のSCRIPT_MODULE_PARAM(引数取得・戻り値追加)をテストコード側で模擬して検証する
/// </summary>
public class ScriptModuleE2ETests : IClassFixture<ScriptModuleE2ETestsFixture>, IDisposable
{
    private readonly NativePluginLibrary _library;
    private unsafe SCRIPT_MODULE_TABLE* _moduleTable = null;

    // ホスト側の状態(UnmanagedCallersOnlyから参照するためstatic)
    private static double[] _doubleParams = [];
    private static string[] _stringParams = [];
    private static double? _capturedDouble;
    private static string? _capturedString;
    private static string? _capturedError;

    public ScriptModuleE2ETests(ScriptModuleE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);

        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetScriptModuleTable");
            _moduleTable = (SCRIPT_MODULE_TABLE*)getTable();
        }
    }

    /// <summary>
    /// GetScriptModuleTable エントリーポイントのテスト
    /// [ScriptFunction] メソッドがnull終端の関数リストとして公開されることを検証する
    /// </summary>
    [Fact]
    public void GetScriptModuleTable_ShouldExposeScriptFunctions()
    {
        unsafe
        {
            var table = _moduleTable;
            Assert.False(table == null);

            Assert.Equal(".NET Script Module Example", Marshal.PtrToStringUni(table->information));

            var names = new List<string>();
            for (var f = table->functions; f->name != IntPtr.Zero; f++)
            {
                names.Add(Marshal.PtrToStringUni(f->name)!);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)f->func);
            }

            Assert.Equal(new[] { "Sum", "concat", "Fail" }, names);
        }
    }

    /// <summary>
    /// 引数取得(double)と戻り値追加(double)のE2Eテスト
    /// </summary>
    [Fact]
    public void SumFunction_ShouldReadParamsAndPushResult()
    {
        _doubleParams = [1.5, 2.0, 3.25];
        _capturedDouble = null;

        unsafe
        {
            var param = CreateParam();
            try
            {
                var sum = FindFunction("Sum");
                sum((IntPtr)param);
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)param);
            }
        }

        Assert.Equal(6.75, _capturedDouble);
        Assert.Null(_capturedError);
    }

    /// <summary>
    /// UTF-8文字列の引数取得・戻り値追加のE2Eテスト
    /// </summary>
    [Fact]
    public void ConcatFunction_ShouldRoundTripUtf8Strings()
    {
        _stringParams = ["あいう", "えお😀"];
        _capturedString = null;

        unsafe
        {
            var param = CreateParam();
            try
            {
                var concat = FindFunction("concat");
                concat((IntPtr)param);
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)param);
            }
        }

        Assert.Equal("あいうえお😀", _capturedString);
    }

    /// <summary>
    /// プラグイン側の例外がset_errorに変換されることを検証
    /// </summary>
    [Fact]
    public void FailFunction_ShouldConvertExceptionToError()
    {
        _capturedError = null;

        unsafe
        {
            var param = CreateParam();
            try
            {
                var fail = FindFunction("Fail");
                fail((IntPtr)param);
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)param);
            }
        }

        Assert.NotNull(_capturedError);
        Assert.Contains("boom", _capturedError);
    }

    private unsafe delegate* unmanaged[Stdcall]<IntPtr, void> FindFunction(string name)
    {
        for (var f = _moduleTable->functions; f->name != IntPtr.Zero; f++)
        {
            if (Marshal.PtrToStringUni(f->name) == name)
            {
                return f->func;
            }
        }
        throw new InvalidOperationException($"function not found: {name}");
    }

    private static unsafe SCRIPT_MODULE_PARAM* CreateParam()
    {
        var param = (SCRIPT_MODULE_PARAM*)Marshal.AllocHGlobal(sizeof(SCRIPT_MODULE_PARAM));
        *param = default;
        param->get_param_num = &HostGetParamNum;
        param->get_param_double = &HostGetParamDouble;
        param->get_param_string = &HostGetParamString;
        param->push_result_double = &HostPushResultDouble;
        param->push_result_string = &HostPushResultString;
        param->set_error = &HostSetError;
        return param;
    }

    #region ホスト側コールバック(SCRIPT_MODULE_PARAMの関数ポインタ)

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int HostGetParamNum()
        => Math.Max(_doubleParams.Length, _stringParams.Length);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double HostGetParamDouble(int index)
        => index >= 0 && index < _doubleParams.Length ? _doubleParams[index] : 0.0;

    // 直近に返したUTF-8文字列を保持(次の呼び出しまで有効というSDKのライフタイムを模擬)
    private static IntPtr _lastStringPtr;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static IntPtr HostGetParamString(int index)
    {
        if (index < 0 || index >= _stringParams.Length)
        {
            return IntPtr.Zero;
        }
        if (_lastStringPtr != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(_lastStringPtr);
        }
        _lastStringPtr = Marshal.StringToCoTaskMemUTF8(_stringParams[index]);
        return _lastStringPtr;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostPushResultDouble(double value)
    {
        _capturedDouble = value;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostPushResultString(IntPtr value)
    {
        _capturedString = Marshal.PtrToStringUTF8(value);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void HostSetError(IntPtr message)
    {
        _capturedError = Marshal.PtrToStringUTF8(message);
    }

    #endregion

    public void Dispose()
    {
        _library.Dispose();
        unsafe
        {
            _moduleTable = null;
        }
    }
}
