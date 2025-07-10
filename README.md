# play-aviutl-plugin-dotnet

AviUtl ExEdit2用の.NETプラグインを開発するためのフレームワークのサンプル実装です。

## 概要

このリポジトリは、AviUtl ExEdit2向けにC#や.NET言語でプラグインを作成するための基盤のサンプルを提供します。Win32 APIとの連携や、AviUtlプラグインのインターフェースを抽象化し、効率的な開発をサポートします。

## 特徴

- AviUtlプラグインの抽象化
- Win32 APIラッパー
- サンプル実装付き

## ディレクトリ構成

```
src/
  AviUtlPluginNet.Abstractions/   # プラグイン共通インターフェース
  AviUtlPluginNet.Core/           # コア機能
  AviUtlPluginNet.Win32/          # Win32 APIラッパー
  AviUtlPluginNet.Example/        # サンプルプラグイン
```

## Exampleのビルドおよび実行方法

1. .NET 9.0 SDKをインストールしてください。
2. `./src/AviUtlPluginNet.Example` ディレクトリで以下のコマンドを実行します。

```sh
dotnet publish /p:NativeLib=Shared --use-current-runtime
```

3. `./src/AviUtlPluginNet.Example/bin/Release/net9.0/win-x64/publish/` ディレクトリに生成されたDLLを `C:\ProgramData\aviutl2\Plugin` にコピーします。
4. コピーしたDLLの拡張子を `.dll` から `.aui2` に変更します。
5. `./src/AviUtlPluginNet.Example/bin/Release/net9.0/win-x64/publish/` ディレクトリに存在する `libSkiaSharp.dll` を aviutl2.exe と同じディレクトリにコピーします。

## Pluginの実装方法

1. Plugin実装に必要なパッケージを参照します。

```xml
<ItemGroup>
  <ProjectReference Include="..\AviUtlPluginNet.Abstractions\AviUtlPluginNet.Abstractions.csproj" />
    <ProjectReference Include="..\AviUtlPluginNet.Core\AviUtlPluginNet.Core.csproj" />
    <ProjectReference Include="..\AviUtlPluginNet.SourceGenerator\AviUtlPluginNet.SourceGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

2. Pluginで読み込むリソースを管理するクラスを実装します。これは `AviUtlPluginNet.Abstractions.IInputHandle` を実装している必要があります。
3. Pluginのメインクラスを実装します。これは `AviUtlPluginNet.Abstractions.IInputPluginAPI` を実装している必要があります。
4. Pluginのクラスに `AviUtlPluginNet.Abstractions.Attribute.AviUtl2InputPluginAttribute` 属性を付与します。

## NOTE

- 映像と音声が両方あるリソースの対応は現在行なっていません。
- AUO2形式プラグインの対応は現在行なっていません。

## ライセンス

MIT License
