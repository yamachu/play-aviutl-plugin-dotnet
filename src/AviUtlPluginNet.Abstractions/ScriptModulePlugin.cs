namespace AviUtlPluginNet.Abstractions;

using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.Module2;

/// <summary>
/// スクリプトモジュールの種別インターフェース (module2.h / GetScriptModuleTable)
/// 公開する関数は <see cref="ScriptFunctionAttribute"/> を付与したメソッドで宣言します
/// (シグネチャ: void Method(ScriptModuleContext context))
/// </summary>
public interface IScriptModulePlugin
{
    /// <summary>
    /// スクリプトモジュールの情報
    /// </summary>
    static abstract string Information { get; }
}

/// <summary>
/// スクリプトモジュールの公開関数に付与する属性
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScriptFunctionAttribute : Attribute
{
    /// <summary>
    /// スクリプトからの呼び出し名 (未指定の場合はメソッド名)
    /// </summary>
    public string? Name { get; }

    public ScriptFunctionAttribute()
    {
    }

    public ScriptFunctionAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// SCRIPT_MODULE_PARAM* のマネージドラッパー
/// スクリプト関数の呼び出し中のみ有効です
/// ※メタテーブル・関数返却系のAPIは未ラップ
/// </summary>
public sealed unsafe class ScriptModuleContext
{
    private readonly SCRIPT_MODULE_PARAM* _param;

    public ScriptModuleContext(IntPtr param)
    {
        _param = (SCRIPT_MODULE_PARAM*)param;
    }

    /// <summary>引数の数</summary>
    public int ParamCount => _param->get_param_num();

    /// <summary>引数の型を取得します</summary>
    public PARAM_TYPE GetParamType(int index) => _param->get_param_type(index);

    #region 引数取得

    /// <summary>引数を整数で取得します (取得出来ない場合は0)</summary>
    public int GetInt(int index) => _param->get_param_int(index);

    /// <summary>引数を浮動小数点で取得します (取得出来ない場合は0)</summary>
    public double GetDouble(int index) => _param->get_param_double(index);

    /// <summary>引数を文字列で取得します (取得出来ない場合はnull)</summary>
    public string? GetString(int index) => Utf8ToString(_param->get_param_string(index));

    /// <summary>引数をデータのポインタで取得します ※LightUserData等から取得</summary>
    public IntPtr GetData(int index) => (IntPtr)_param->get_param_data(index);

    /// <summary>引数をブール値で取得します (取得出来ない場合はfalse)</summary>
    public bool GetBoolean(int index) => _param->get_param_boolean(index);

    /// <summary>引数の連想配列要素を整数で取得します</summary>
    public int GetTableInt(int index, string key)
    {
        using var k = new Utf8String(key);
        return _param->get_param_table_int(index, k.Pointer);
    }

    /// <summary>引数の連想配列要素を浮動小数点で取得します</summary>
    public double GetTableDouble(int index, string key)
    {
        using var k = new Utf8String(key);
        return _param->get_param_table_double(index, k.Pointer);
    }

    /// <summary>引数の連想配列要素を文字列で取得します</summary>
    public string? GetTableString(int index, string key)
    {
        using var k = new Utf8String(key);
        return Utf8ToString(_param->get_param_table_string(index, k.Pointer));
    }

    /// <summary>引数の連想配列要素をブール値で取得します</summary>
    public bool GetTableBoolean(int index, string key)
    {
        using var k = new Utf8String(key);
        return _param->get_param_table_boolean(index, k.Pointer);
    }

    /// <summary>引数の配列要素の数を取得します</summary>
    public int GetArrayCount(int index) => _param->get_param_array_num(index);

    /// <summary>引数の配列要素を整数で取得します</summary>
    public int GetArrayInt(int index, int key) => _param->get_param_array_int(index, key);

    /// <summary>引数の配列要素を浮動小数点で取得します</summary>
    public double GetArrayDouble(int index, int key) => _param->get_param_array_double(index, key);

    /// <summary>引数の配列要素を文字列で取得します</summary>
    public string? GetArrayString(int index, int key) => Utf8ToString(_param->get_param_array_string(index, key));

    #endregion

    #region 戻り値追加

    /// <summary>整数の戻り値を追加します</summary>
    public void PushResult(int value) => _param->push_result_int(value);

    /// <summary>浮動小数点の戻り値を追加します</summary>
    public void PushResult(double value) => _param->push_result_double(value);

    /// <summary>文字列の戻り値を追加します</summary>
    public void PushResult(string value)
    {
        using var v = new Utf8String(value);
        _param->push_result_string(v.Pointer);
    }

    /// <summary>ブール値の戻り値を追加します</summary>
    public void PushResult(bool value) => _param->push_result_boolean(value);

    /// <summary>データのポインタの戻り値を追加します ※LightUserDataを返却</summary>
    public void PushResultData(IntPtr value) => _param->push_result_data((void*)value);

    /// <summary>整数配列の戻り値を追加します</summary>
    public void PushResult(ReadOnlySpan<int> values)
    {
        fixed (int* v = values)
        {
            _param->push_result_array_int(v, values.Length);
        }
    }

    /// <summary>浮動小数点配列の戻り値を追加します</summary>
    public void PushResult(ReadOnlySpan<double> values)
    {
        fixed (double* v = values)
        {
            _param->push_result_array_double(v, values.Length);
        }
    }

    /// <summary>ブール値配列の戻り値を追加します</summary>
    public void PushResult(ReadOnlySpan<bool> values)
    {
        var bytes = values.Length <= 256 ? stackalloc byte[values.Length] : new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            bytes[i] = values[i] ? (byte)1 : (byte)0;
        }
        fixed (byte* v = bytes)
        {
            _param->push_result_array_boolean(v, values.Length);
        }
    }

    /// <summary>文字列配列の戻り値を追加します</summary>
    public void PushResult(ReadOnlySpan<string> values)
    {
        using var array = new Utf8StringArray(values);
        _param->push_result_array_string(array.Pointers, values.Length);
    }

    /// <summary>整数連想配列の戻り値を追加します</summary>
    public void PushResultTable(ReadOnlySpan<string> keys, ReadOnlySpan<int> values)
    {
        CheckTableLength(keys.Length, values.Length);
        using var keyArray = new Utf8StringArray(keys);
        fixed (int* v = values)
        {
            _param->push_result_table_int(keyArray.Pointers, v, keys.Length);
        }
    }

    /// <summary>浮動小数点連想配列の戻り値を追加します</summary>
    public void PushResultTable(ReadOnlySpan<string> keys, ReadOnlySpan<double> values)
    {
        CheckTableLength(keys.Length, values.Length);
        using var keyArray = new Utf8StringArray(keys);
        fixed (double* v = values)
        {
            _param->push_result_table_double(keyArray.Pointers, v, keys.Length);
        }
    }

    /// <summary>文字列連想配列の戻り値を追加します</summary>
    public void PushResultTable(ReadOnlySpan<string> keys, ReadOnlySpan<string> values)
    {
        CheckTableLength(keys.Length, values.Length);
        using var keyArray = new Utf8StringArray(keys);
        using var valueArray = new Utf8StringArray(values);
        _param->push_result_table_string(keyArray.Pointers, valueArray.Pointers, keys.Length);
    }

    /// <summary>ブール値連想配列の戻り値を追加します</summary>
    public void PushResultTable(ReadOnlySpan<string> keys, ReadOnlySpan<bool> values)
    {
        CheckTableLength(keys.Length, values.Length);
        using var keyArray = new Utf8StringArray(keys);
        var bytes = values.Length <= 256 ? stackalloc byte[values.Length] : new byte[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            bytes[i] = values[i] ? (byte)1 : (byte)0;
        }
        fixed (byte* v = bytes)
        {
            _param->push_result_table_boolean(keyArray.Pointers, v, keys.Length);
        }
    }

    #endregion

    /// <summary>
    /// エラーメッセージを設定します
    /// 呼び出された関数をエラー終了する場合に設定します
    /// </summary>
    public void SetError(string message)
    {
        using var m = new Utf8String(message);
        _param->set_error(m.Pointer);
    }

    private static void CheckTableLength(int keys, int values)
    {
        if (keys != values)
        {
            throw new ArgumentException("keys and values must have the same length");
        }
    }

    private static string? Utf8ToString(IntPtr utf8)
        => utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);

    /// <summary>
    /// UTF-8のネイティブ文字列(呼び出しの間だけ有効)
    /// </summary>
    private readonly ref struct Utf8String
    {
        public IntPtr Pointer { get; }

        public Utf8String(string value)
        {
            Pointer = Marshal.StringToCoTaskMemUTF8(value);
        }

        public void Dispose()
        {
            Marshal.FreeCoTaskMem(Pointer);
        }
    }

    /// <summary>
    /// UTF-8のネイティブ文字列配列(呼び出しの間だけ有効)
    /// </summary>
    private readonly ref struct Utf8StringArray
    {
        private readonly IntPtr[] _pointers;
        private readonly GCHandle _pin;

        public Utf8StringArray(ReadOnlySpan<string> values)
        {
            _pointers = new IntPtr[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                _pointers[i] = Marshal.StringToCoTaskMemUTF8(values[i]);
            }
            _pin = GCHandle.Alloc(_pointers, GCHandleType.Pinned);
        }

        public IntPtr* Pointers => (IntPtr*)_pin.AddrOfPinnedObject();

        public void Dispose()
        {
            _pin.Free();
            foreach (var p in _pointers)
            {
                Marshal.FreeCoTaskMem(p);
            }
        }
    }
}
