namespace AviUtlPluginNet.FilterCacheExample;

using System;
using AviUtlPluginNet.Abstractions;

/// <summary>
/// MediaFilter.cpp をベースに Cache2 デモを追加したサンプルフィルタプラグインです。
/// Filter2 (IFilterVideo / IFilterAudio) と Cache2 (IUseCache) の動作確認を目的としています。
/// </summary>
[AviUtl2Plugin]
partial class SampleFilterPlugin : IFilterVideo, IFilterAudio, IUseCache, IUseLogger, IPluginLifecycle
{
    public static string Name => "Sample Media Filter (.NET)";
    public static string? Label => "サンプル";
    public static string Information => "MediaFilter.cpp ベース + Cache2 デモ (.NET NativeAOT)";

    private ICache2? _cache;
    private ILogger2? _logger;
    // キャッシュ状態遷移を記録 (状態変化時のみログ出力してログが流れすぎるのを防ぐ)
    private bool _cacheHitLogged = false;

    // ===== 画像グループ =====

    /// <summary>選択した成分の明るさ倍率 (0.0 ～ 2.0)</summary>
    [FilterGroup("画像")]
    [FilterTrack("明るさ", Default = 1.0, Min = 0.0, Max = 2.0, Step = 0.01)]
    public partial double Luminance { get; }

    /// <summary>明るさ調整を適用する成分 (bit0=R, bit1=G, bit2=B)</summary>
    [FilterSelect("対象", Default = 7)]
    [FilterSelectItem("R成分のみ", 1)]
    [FilterSelectItem("G成分のみ", 2)]
    [FilterSelectItem("B成分のみ", 4)]
    [FilterSelectItem("RGB成分", 7)]
    public partial int Component { get; }

    // ===== 音声グループ =====

    /// <summary>音量の倍率 (0.0 ～ 2.0)</summary>
    [FilterGroup("音声")]
    [FilterTrack("音量", Default = 1.0, Min = 0.0, Max = 2.0, Step = 0.01)]
    public partial double Volume { get; }

    /// <summary>両チャンネルをモノラルにまとめる</summary>
    [FilterCheck("モノラル化", Default = false)]
    public partial bool Mono { get; }

    // ===== Cake feature 注入 =====

    public void AttachCache(ICache2 cache) => _cache = cache;
    public void AttachLogger(ILogger2 logger) => _logger = logger;

    // ===== ライフサイクル =====

    public bool OnInitialize(uint hostVersion)
    {
        _logger?.Info($"SampleFilterPlugin: initialized (host version: {hostVersion})");
        return true;
    }

    public void OnUninitialize()
    {
        _logger?.Info("SampleFilterPlugin: uninitialized");
    }

    // ===== 画像フィルタ処理 (IFilterVideo) =====

    public bool ProcVideo(FilterVideoContext context)
    {
        int w = context.Width;
        int h = context.Height;
        var pixels = context.GetImageData();

        // --- MediaFilter.cpp 移植: 選択成分の明るさ調整 ---
        double r = (Component & 1) != 0 ? Luminance : 1.0;
        double g = (Component & 2) != 0 ? Luminance : 1.0;
        double b = (Component & 4) != 0 ? Luminance : 1.0;

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = (byte)Math.Clamp(pixels[i].r * r, 0.0, 255.0);
            pixels[i].g = (byte)Math.Clamp(pixels[i].g * g, 0.0, 255.0);
            pixels[i].b = (byte)Math.Clamp(pixels[i].b * b, 0.0, 255.0);
        }

        // --- Cache2 デモ: 初回フレームをキャッシュし、以降のフレームとブレンド ---
        // ※ Cache2 API (sret ABI) の動作確認が目的。実用フィルタでは通常このような使い方はしない。
        if (_cache != null)
        {
            var cacheKey = $"sample_bg_{w}x{h}";
            using var cached = _cache.GetImageCache(cacheKey);
            if (cached == null)
            {
                // キャッシュミス: 現フレームの処理済みピクセルをキャッシュに書き込む
                using var created = _cache.CreateImageCache(cacheKey, w, h);
                if (created != null)
                {
                    pixels.AsSpan().CopyTo(created.Buffer);
                    _logger?.Info($"SampleFilterPlugin: cache created ({cacheKey})");
                }
                _cacheHitLogged = false;
            }
            else
            {
                // キャッシュヒット: 処理済みピクセルとキャッシュフレームを 50/50 ブレンド
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i].r = (byte)((pixels[i].r + cached.Buffer[i].r) >> 1);
                    pixels[i].g = (byte)((pixels[i].g + cached.Buffer[i].g) >> 1);
                    pixels[i].b = (byte)((pixels[i].b + cached.Buffer[i].b) >> 1);
                    pixels[i].a = (byte)((pixels[i].a + cached.Buffer[i].a) >> 1);
                }
                if (!_cacheHitLogged)
                {
                    _logger?.Verbose($"SampleFilterPlugin: cache hit ({cacheKey}), blending enabled");
                    _cacheHitLogged = true;
                }
            }
        }

        context.SetImageData(pixels, w, h);
        return true;
    }

    // ===== 音声フィルタ処理 (IFilterAudio) =====

    public bool ProcAudio(FilterAudioContext context)
    {
        int sampleCount = context.SampleCount;
        var left = new float[sampleCount];
        var right = new float[sampleCount];

        context.GetSampleData(left, 0);
        context.GetSampleData(right, 1);

        // --- MediaFilter.cpp 移植: 音量調整 ---
        float v = (float)Volume;
        for (int i = 0; i < sampleCount; i++)
        {
            left[i] *= v;
            right[i] *= v;
        }

        // --- MediaFilter.cpp 移植: モノラル化 ---
        if (Mono)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                left[i] += right[i];
            }
            context.SetSampleData(left, 0);
            context.SetSampleData(left, 1);
        }
        else
        {
            context.SetSampleData(left, 0);
            context.SetSampleData(right, 1);
        }

        return true;
    }
}
