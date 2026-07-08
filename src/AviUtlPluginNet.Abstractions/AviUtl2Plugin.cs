namespace AviUtlPluginNet.Abstractions;

using System;

/// <summary>
/// 全プラグイン種別の共通ベースインターフェース
/// </summary>
public interface IAviUtl2Plugin
{
    /// <summary>
    /// プラグインの名前
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// プラグインの情報
    /// </summary>
    static abstract string Information { get; }
}

/// <summary>
/// AviUtl2 Inputプラグインのハンドルインターフェース
/// </summary>
public interface IInputHandle : IDisposable;
