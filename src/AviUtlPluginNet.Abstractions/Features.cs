namespace AviUtlPluginNet.Abstractions;

using System;
using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.Config2;
using AviUtlPluginNet.Core.Interop.Logger2;

// ホストサービス機能 (種別横断のcake機能)
// これらのインターフェースを実装すると、SourceGeneratorが対応する任意エクスポート
// (InitializeLogger / InitializeConfig / InitializePlugin / UninitializePlugin / RequiredVersion)
// を生成し、ホストからの初期化呼び出しがプラグインに注入されます

/// <summary>
/// ログ出力機能 (logger2.h)
/// ログ出力は1024文字で制限されます
/// </summary>
public interface ILogger2
{
    /// <summary>プラグイン用のログを出力します</summary>
    void Log(string message);
    /// <summary>infoレベルのログを出力します</summary>
    void Info(string message);
    /// <summary>warnレベルのログを出力します</summary>
    void Warn(string message);
    /// <summary>errorレベルのログを出力します</summary>
    void Error(string message);
    /// <summary>verboseレベルのログを出力します</summary>
    void Verbose(string message);
}

/// <summary>
/// ログ出力機能を利用する機能インターフェース (export: InitializeLogger)
/// ※InitializePlugin()より先に呼ばれます
/// </summary>
public interface IUseLogger
{
    /// <summary>
    /// ホストからログ出力機能が注入されます
    /// </summary>
    void AttachLogger(ILogger2 logger);
}

/// <summary>
/// フォント情報
/// </summary>
public readonly record struct FontInfo(string Name, float Size);

/// <summary>
/// 設定関連機能 (config2.h)
/// </summary>
public interface IConfig2
{
    /// <summary>アプリケーションデータフォルダのパス</summary>
    string AppDataPath { get; }

    /// <summary>
    /// 現在の言語設定で定義されているテキストを取得します
    /// 参照する言語設定のセクションはプラグインのファイル名になります
    /// </summary>
    /// <param name="text">元のテキスト(.aul2ファイルのキー名)</param>
    string Translate(string text);

    /// <summary>
    /// 現在の言語設定で定義されているテキストを取得します ※任意のセクションから取得出来ます
    /// </summary>
    /// <param name="section">言語設定のセクション(.aul2ファイルのセクション名)</param>
    /// <param name="text">元のテキスト(.aul2ファイルのキー名)</param>
    string GetLanguageText(string section, string text);

    /// <summary>
    /// 設定ファイルで定義されているフォント情報を取得します
    /// </summary>
    /// <param name="key">設定ファイル(style.conf)の[Font]のキー名</param>
    FontInfo GetFontInfo(string key);

    /// <summary>
    /// 設定ファイルで定義されている色コードを取得します ※複数色の場合は最初の色が取得されます
    /// </summary>
    /// <param name="key">設定ファイル(style.conf)の[Color]のキー名</param>
    int GetColorCode(string key);

    /// <summary>
    /// 設定ファイルで定義されているレイアウトサイズを取得します
    /// </summary>
    /// <param name="key">設定ファイル(style.conf)の[Layout]のキー名</param>
    int GetLayoutSize(string key);

    /// <summary>
    /// 設定ファイルで定義されている色コードを取得します
    /// </summary>
    /// <param name="key">設定ファイル(style.conf)の[Color]のキー名</param>
    /// <param name="index">取得する色のインデックス(-1を指定すると色の数を返却します)</param>
    int GetColorCode(string key, int index);
}

/// <summary>
/// 設定関連機能を利用する機能インターフェース (export: InitializeConfig)
/// ※InitializePlugin()より先に呼ばれます
/// </summary>
public interface IUseConfig
{
    /// <summary>
    /// ホストから設定関連機能が注入されます
    /// </summary>
    void AttachConfig(IConfig2 config);
}

/// <summary>
/// プラグインDLLの初期化・終了処理の機能インターフェース
/// (export: InitializePlugin / UninitializePlugin)
/// </summary>
public interface IPluginLifecycle
{
    /// <summary>
    /// プラグインDLL初期化時に呼ばれます
    /// </summary>
    /// <param name="hostVersion">本体のバージョン番号</param>
    /// <returns>成功時はtrue</returns>
    bool OnInitialize(uint hostVersion);

    /// <summary>
    /// プラグインDLL終了時に呼ばれます
    /// </summary>
    void OnUninitialize();
}

/// <summary>
/// 必要とする本体バージョン番号を宣言する機能インターフェース (export: RequiredVersion)
/// </summary>
public interface IRequireVersion
{
    /// <summary>
    /// 必要な本体のバージョン番号
    /// </summary>
    static abstract uint RequiredVersion { get; }
}

/// <summary>
/// LOG_HANDLE* をラップした <see cref="ILogger2"/> の実装
/// </summary>
public sealed unsafe class NativeLogger2 : ILogger2
{
    private readonly LOG_HANDLE* _handle;

    public NativeLogger2(IntPtr handle)
    {
        _handle = (LOG_HANDLE*)handle;
    }

    public void Log(string message)
    {
        fixed (char* p = message) _handle->log(_handle, (IntPtr)p);
    }

    public void Info(string message)
    {
        fixed (char* p = message) _handle->info(_handle, (IntPtr)p);
    }

    public void Warn(string message)
    {
        fixed (char* p = message) _handle->warn(_handle, (IntPtr)p);
    }

    public void Error(string message)
    {
        fixed (char* p = message) _handle->error(_handle, (IntPtr)p);
    }

    public void Verbose(string message)
    {
        fixed (char* p = message) _handle->verbose(_handle, (IntPtr)p);
    }
}

/// <summary>
/// CONFIG_HANDLE* をラップした <see cref="IConfig2"/> の実装
/// </summary>
public sealed unsafe class NativeConfig2 : IConfig2
{
    private readonly CONFIG_HANDLE* _handle;

    public NativeConfig2(IntPtr handle)
    {
        _handle = (CONFIG_HANDLE*)handle;
    }

    public string AppDataPath => Marshal.PtrToStringUni(_handle->app_data_path) ?? string.Empty;

    public string Translate(string text)
    {
        fixed (char* p = text)
        {
            var result = _handle->translate(_handle, (IntPtr)p);
            return Marshal.PtrToStringUni(result) ?? text;
        }
    }

    public string GetLanguageText(string section, string text)
    {
        fixed (char* s = section)
        fixed (char* t = text)
        {
            var result = _handle->get_language_text(_handle, (IntPtr)s, (IntPtr)t);
            return Marshal.PtrToStringUni(result) ?? text;
        }
    }

    public FontInfo GetFontInfo(string key)
    {
        var keyPtr = Marshal.StringToHGlobalAnsi(key);
        try
        {
            var info = _handle->get_font_info(_handle, keyPtr);
            if (info == null)
            {
                return new FontInfo(string.Empty, 0f);
            }
            return new FontInfo(Marshal.PtrToStringUni(info->name) ?? string.Empty, info->size);
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
        }
    }

    public int GetColorCode(string key)
    {
        var keyPtr = Marshal.StringToHGlobalAnsi(key);
        try
        {
            return _handle->get_color_code(_handle, keyPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
        }
    }

    public int GetLayoutSize(string key)
    {
        var keyPtr = Marshal.StringToHGlobalAnsi(key);
        try
        {
            return _handle->get_layout_size(_handle, keyPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
        }
    }

    public int GetColorCode(string key, int index)
    {
        var keyPtr = Marshal.StringToHGlobalAnsi(key);
        try
        {
            return _handle->get_color_code_index(_handle, keyPtr, index);
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
        }
    }
}
