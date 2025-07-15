# AviUtlPluginNet.PreBuiltTests

## 概要

このテストプロジェクトは、既にビルドされたライブラリが正しくAviUtlプラグインのインターフェースを実装しているかを検証するためのE2Eテストです。.NETで作成したプラグインだけでなく、C++など他の言語で作成されたライブラリも対象としています。

## 目的

- 既存のネイティブライブラリがAviUtlプラグインAPIを正しく実装しているかを検証
- 異なるプログラミング言語で作成されたプラグインの互換性確認
- プラグインのインターフェース仕様の検証

## テストの実行方法

vendorディレクトリに配置されたライブラリをテストするには、`RUN_E2E_TARGET`環境変数を設定します：

```sh
# サンプルプラグインのテスト
RUN_E2E_TARGET=AviUtlPluginNet.Example.dylib dotnet test

# 他のライブラリのテスト（例：C++で作成されたプラグイン）
RUN_E2E_TARGET=MyNativePlugin.dll dotnet test
```

## テストファイルの配置

テスト対象のライブラリは以下のディレクトリに配置してください、以下は例です：

```
test/AviUtlPluginNet.PreBuiltTests/
├── vendor/
│   ├── AviUtlPluginNet.Example.dylib    # サンプルプラグイン（macOS）
│   ├── AviUtlPluginNet.Example.dll      # サンプルプラグイン（Windows）
│   ├── MyNativePlugin.dll               # 他言語で作成されたプラグイン
│   ├── libSkiaSharp.dylib               # プラグインが依存するライブラリ
│   └── test_image.png                   # テスト用画像
└── README.md
```

## テストの種類

### 1. インターフェース検証テスト

- `GetInputPluginTable_ShouldReturnValidPointer()`: プラグインテーブルの基本的な有効性を検証
- 関数ポインタの存在確認
- プラグイン名の取得確認

### 2. ワークフローテスト

- `NativeWorkflow_ShouldWorkEndToEnd()`: 完全なプラグインワークフローを検証
- ファイルオープン → 情報取得 → データ読み取り → ファイルクローズ

## 環境変数リファレンス

| 環境変数 | 説明 | 例 |
|---------|------|-----|
| `RUN_E2E_TARGET` | テスト対象のライブラリファイル名 | `AviUtlPluginNet.Example.dylib` |

## 注意事項

1. **プラットフォーム依存**: ライブラリファイルの拡張子はプラットフォームに依存します
   - Windows: `.dll`
   - macOS: `.dylib`
   - Linux: `.so`

2. **依存関係**: テスト対象のライブラリが他のライブラリに依存している場合、それらも`vendor`ディレクトリに配置する必要があります

3. **テストデータ**: `PreBuiltE2ETests.cs` 内のFIXMEやTODOコメントに従い、実際のテストデータ（画像や動画ファイルなど）を適切なパスに配置し、パスを書き換えください
