using Xunit;

// 各テストクラスのFixtureがNativeAOT publishで同一のsrcプロジェクト(obj/bin)を参照するため、
// 並列実行するとビルドが競合する。テストコレクションの並列実行を無効化する。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
