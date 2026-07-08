namespace AviUtlPluginNet.Abstractions;

using System;

/// <summary>
/// AviUtl2 プラグインクラスに付与するマーカー属性
/// SourceGeneratorがこの属性を検出し、実装しているインターフェースから
/// プラグイン種別・機能を推論してネイティブエクスポートを生成します
/// ※NativeAOTのエクスポート名はアセンブリ内で一意のため、1アセンブリに1クラスのみ付与できます
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AviUtl2PluginAttribute : Attribute
{
}
