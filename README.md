# play-aviutl-plugin-dotnet

AviUtl ExEdit2用の.NETプラグインを開発するためのフレームワークのサンプル実装です。

## 概要

このリポジトリは、AviUtl ExEdit2向けにC#や.NET言語でプラグインを作成するための基盤のサンプルを提供します。Win32 APIとの連携や、AviUtlプラグインのインターフェースを抽象化し、効率的な開発をサポートします。

## 特徴

- AviUtlプラグインの抽象化
- Win32 APIラッパー
- サンプル実装付き
- 包括的なE2Eテスト

## ディレクトリ構成

```
src/
  AviUtlPluginNet.Abstractions/   # プラグイン共通インターフェース
  AviUtlPluginNet.Core/           # コア機能
  AviUtlPluginNet.Win32/          # Win32 APIラッパー
  AviUtlPluginNet.Example/        # サンプルプラグイン
  AviUtlPluginNet.SourceGenerator/ # Source Generator
test/
  AviUtlPluginNet.AbstractionsTests/ # E2Eテスト
    Utils/                          # テストユーティリティ
      PluginFixture.cs              # プラグインビルド・管理
      NativeInputPluginTableProvider.cs # ネイティブライブラリ操作
    templates/                      # テスト用プラグインテンプレート
      TestPlugin.csproj.template    # テストプラグイン用プロジェクトファイル
    SimpleNativeLibraryE2ETests.cs  # メインのE2Eテスト
  AviUtlPluginNet.PreBuiltTests/    # 既存ライブラリのインターフェース検証テスト
    Utils/                          # テストユーティリティ
    vendor/                         # テスト対象ライブラリ配置ディレクトリ
    PreBuiltE2ETests.cs             # 既存ライブラリのE2Eテスト
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
- E2EテストはSource Generatorが生成したアダプター層の動作を検証します。

## テストの実行方法

### 1. 開発用E2Eテスト (AviUtlPluginNet.AbstractionsTests)

.NETで実装したプラグインのSource Generatorとアダプター層をテストします：

```sh
dotnet test test/AviUtlPluginNet.AbstractionsTests/
```

### 2. 既存ライブラリのインターフェース検証テスト (AviUtlPluginNet.PreBuiltTests)

既にビルドされたライブラリ（.NET以外の言語で作成されたものも含む）のインターフェースを検証します：

```sh
# 事前にビルドされたライブラリのテスト
RUN_E2E_TARGET=AviUtlPluginNet.Example.dylib dotnet test test/AviUtlPluginNet.PreBuiltTests/

# 他の言語で作成されたプラグインのテスト
RUN_E2E_TARGET=MyNativePlugin.dll dotnet test test/AviUtlPluginNet.PreBuiltTests/
```

詳細なテスト実行方法は各テストプロジェクトのREADMEを参照してください。

## ライセンス

MIT License
