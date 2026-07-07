using System;
using System.Runtime.InteropServices;

namespace AviUtlPluginNet.Core.Interop.Module2;

/// <summary>
/// 引数の型種別
/// </summary>
public enum PARAM_TYPE : int
{
    NONE = -1,
    NIL = 0,
    BOOLEAN = 1,
    LIGHTUSERDATA = 2,
    NUMBER = 3,
    STRING = 4,
    TABLE = 5,
    FUNCTION = 6,
    USERDATA = 7,
    THREAD = 8,
}

/// <summary>
/// スクリプトモジュール引数構造体
/// 文字列はすべてUTF-8(LPCSTR)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SCRIPT_MODULE_PARAM
{
    /// <summary>引数の数を取得する</summary>
    public delegate* unmanaged[Stdcall]<int> get_param_num;
    /// <summary>引数を整数で取得する (取得出来ない場合は0)</summary>
    public delegate* unmanaged[Stdcall]<int, int> get_param_int;
    /// <summary>引数を浮動小数点で取得する (取得出来ない場合は0)</summary>
    public delegate* unmanaged[Stdcall]<int, double> get_param_double;
    /// <summary>引数を文字列(UTF-8)で取得する (取得出来ない場合はnull)</summary>
    public delegate* unmanaged[Stdcall]<int, IntPtr> get_param_string;
    /// <summary>引数をデータのポインタで取得する ※LightUserData等から取得</summary>
    public delegate* unmanaged[Stdcall]<int, void*> get_param_data;

    /// <summary>引数の連想配列要素を整数で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, IntPtr, int> get_param_table_int;
    /// <summary>引数の連想配列要素を浮動小数点で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, IntPtr, double> get_param_table_double;
    /// <summary>引数の連想配列要素を文字列(UTF-8)で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, IntPtr, IntPtr> get_param_table_string;

    /// <summary>引数の配列要素の数を取得する</summary>
    public delegate* unmanaged[Stdcall]<int, int> get_param_array_num;
    /// <summary>引数の配列要素を整数で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, int, int> get_param_array_int;
    /// <summary>引数の配列要素を浮動小数点で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, int, double> get_param_array_double;
    /// <summary>引数の配列要素を文字列(UTF-8)で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, int, IntPtr> get_param_array_string;

    /// <summary>整数の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<int, void> push_result_int;
    /// <summary>浮動小数点の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<double, void> push_result_double;
    /// <summary>文字列(UTF-8)の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> push_result_string;
    /// <summary>データのポインタの戻り値を追加する ※LightUserDataを返却</summary>
    public delegate* unmanaged[Stdcall]<void*, void> push_result_data;

    /// <summary>整数連想配列の戻り値を追加する (key配列, value配列, 要素数)</summary>
    public delegate* unmanaged[Stdcall]<IntPtr*, int*, int, void> push_result_table_int;
    /// <summary>浮動小数点連想配列の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr*, double*, int, void> push_result_table_double;
    /// <summary>文字列(UTF-8)連想配列の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr*, IntPtr*, int, void> push_result_table_string;

    /// <summary>整数配列の戻り値を追加する (value配列, 要素数)</summary>
    public delegate* unmanaged[Stdcall]<int*, int, void> push_result_array_int;
    /// <summary>浮動小数点配列の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<double*, int, void> push_result_array_double;
    /// <summary>文字列(UTF-8)配列の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<IntPtr*, int, void> push_result_array_string;

    /// <summary>
    /// エラーメッセージ(UTF-8)を設定する
    /// 呼び出された関数をエラー終了する場合に設定します
    /// </summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> set_error;

    /// <summary>引数をブール値で取得する (取得出来ない場合はfalse)</summary>
    public delegate* unmanaged[Stdcall]<int, bool> get_param_boolean;
    /// <summary>ブール値の戻り値を追加する</summary>
    public delegate* unmanaged[Stdcall]<bool, void> push_result_boolean;
    /// <summary>引数の連想配列要素をブール値で取得する</summary>
    public delegate* unmanaged[Stdcall]<int, IntPtr, bool> get_param_table_boolean;
    /// <summary>ブール値配列の戻り値を追加する ※boolは1バイト</summary>
    public delegate* unmanaged[Stdcall]<byte*, int, void> push_result_array_boolean;
    /// <summary>ブール値連想配列の戻り値を追加する ※boolは1バイト</summary>
    public delegate* unmanaged[Stdcall]<IntPtr*, byte*, int, void> push_result_table_boolean;

    /// <summary>
    /// 編集セクション関数 (EDIT_SECTION*)
    /// スクリプト処理中は参照系の関数が利用出来ます
    /// </summary>
    public void* edit;

    /// <summary>
    /// 関数を戻り値として追加する (未ラップ: void (*)(void (*func)(SCRIPT_MODULE_PARAM*), void* userdata))
    /// </summary>
    public IntPtr push_result_function;
    /// <summary>
    /// 新しい関数に差し替えるので廃止 (呼び出さないこと)
    /// </summary>
    public IntPtr deprecated_push_result_meta_table;
    /// <summary>
    /// 任意のユーザーデータのポインタ
    /// push_result_function(),push_result_meta_table()の引数の値が格納されます
    /// </summary>
    public void* userdata;
    /// <summary>
    /// メタテーブルの戻り値を追加する (未ラップ: void (*)(META_METHOD_FUNCTION*, void*))
    /// </summary>
    public IntPtr push_result_meta_table;
    /// <summary>
    /// 引数のメタテーブルのuserdataのポインタを取得する (未ラップ: void* (*)(int, META_METHOD_FUNCTION*))
    /// </summary>
    public IntPtr get_param_meta_table;
    /// <summary>引数の型を取得します</summary>
    public delegate* unmanaged[Stdcall]<int, PARAM_TYPE> get_param_type;
}

/// <summary>
/// メタメソッド定義構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct META_METHOD_FUNCTION
{
    /// <summary>メタメソッド名(UTF-8) ※luaのメタメソッドを指定出来ます</summary>
    public IntPtr method;
    /// <summary>コールバック関数 (void (*)(SCRIPT_MODULE_PARAM*))</summary>
    public delegate* unmanaged[Stdcall]<SCRIPT_MODULE_PARAM*, void> func;
}

/// <summary>
/// スクリプトモジュール関数定義構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SCRIPT_MODULE_FUNCTION
{
    /// <summary>関数名(LPCWSTR)</summary>
    public IntPtr name;
    /// <summary>関数へのポインタ (void (*)(SCRIPT_MODULE_PARAM*))</summary>
    public delegate* unmanaged[Stdcall]<IntPtr, void> func;
}

/// <summary>
/// スクリプトモジュール構造体
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SCRIPT_MODULE_TABLE
{
    /// <summary>スクリプトモジュールの情報(LPCWSTR)</summary>
    public IntPtr information;
    /// <summary>
    /// 登録する関数の一覧
    /// (SCRIPT_MODULE_FUNCTIONを列挙して関数名がnullの要素で終端したリストへのポインタ)
    /// </summary>
    public SCRIPT_MODULE_FUNCTION* functions;
}
