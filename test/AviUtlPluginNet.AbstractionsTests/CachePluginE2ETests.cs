using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AviUtlPluginNet.AbstractionsTests.Utils;
using AviUtlPluginNet.Core.Interop.AUI2;
using AviUtlPluginNet.Core.Interop.Cache2;
using AviUtlPluginNet.Core.Interop.Filter2;

namespace AviUtlPluginNet.AbstractionsTests;

/// <summary>
/// このテスト専用のフィクスチャクラス
/// </summary>
public class CachePluginE2ETestsFixture : IDisposable
{
    public PluginFixture PluginFixture { get; }

    public CachePluginE2ETestsFixture()
    {
        PluginFixture = new PluginFixture();
        PluginFixture.BuildTestPlugin(GetCachePluginImplementation());
    }

    private static string GetCachePluginImplementation()
    {
        return """
        using System;
        using System.Runtime.InteropServices;
        using AviUtlPluginNet.Abstractions;
        using AviUtlPluginNet.Core.Interop.AUI2;
        using AviUtlPluginNet.Core.Interop.Filter2;

        namespace AviUtlPluginNet.Example;

        class DummyHandle : IInputHandle
        {
            public void Dispose() { }
        }

        [AviUtl2Plugin]
        class CacheTestPlugin : IInputVideo<DummyHandle>, IUseCache
        {
            public static string Name => ".NET Cache Test Plugin";
            public static string FileFilter => "All Files (*.*)\0*.*\0";
            public static string Information => "Cache Feature E2E";

            private ICache2? _cache;

            // ホストからキャッシュ関連機能が注入される (IUseCache)
            public void AttachCache(ICache2 cache)
            {
                _cache = cache;
            }

            public DummyHandle? Open(string file) => new DummyHandle();

            public bool Close(DummyHandle handle)
            {
                handle.Dispose();
                return true;
            }

            public bool TryGetInfo(DummyHandle handle, out INPUT_INFO info)
            {
                info = new INPUT_INFO { flag = InputFlag.Video, rate = 30, scale = 1, n = 1 };
                return true;
            }

            public Span<byte> ReadVideo(DummyHandle handle, int frame)
            {
                if (_cache == null)
                {
                    return Span<byte>.Empty;
                }

                const int w = 4, h = 2;

                // キャッシュを作成してデータを書き込む
                using (var created = _cache.CreateImageCache("e2e", w, h))
                {
                    if (created == null)
                    {
                        return Span<byte>.Empty;
                    }
                    created.Buffer.Fill(new PIXEL_RGBA { r = (byte)(frame + 1), g = 2, b = 3, a = 4 });
                }

                // キャッシュから読み戻して返却する
                using var fetched = _cache.GetImageCache("e2e");
                if (fetched == null)
                {
                    return Span<byte>.Empty;
                }
                return MemoryMarshal.AsBytes(fetched.Buffer).ToArray();
            }
        }
        """;
    }

    public void Dispose()
    {
        PluginFixture.Dispose();
    }
}

/// <summary>
/// キャッシュ機能(IUseCache / InitializeCache)のNativeAOT E2Eテスト
/// ホスト側のCACHE_HANDLEをsret規約(戻り値バッファを第1引数で渡す)で模擬して、
/// プラグイン側のNativeCache2ラッパーとの相互運用を検証する
/// ※このテストはC#同士の模擬でありC++実ホスト(Windows)とのABI一致は別途検証が必要
/// </summary>
public class CachePluginE2ETests : IClassFixture<CachePluginE2ETestsFixture>, IDisposable
{
    private const int Width = 4;
    private const int Height = 2;

    private readonly NativePluginLibrary _library;
    private unsafe INPUT_PLUGIN_TABLE* _pluginTable = null;

    // ホスト側キャッシュの状態(UnmanagedCallersOnlyから参照するためstatic)
    private static IntPtr _cacheBuffer;
    private static int _cachedWidth, _cachedHeight;
    private static string? _lastCacheName;
    private static int _createCalls;
    private static int _getCalls;
    private static int _releaseCalls;

    public CachePluginE2ETests(CachePluginE2ETestsFixture testFixture)
    {
        _library = new NativePluginLibrary(testFixture.PluginFixture.DllPath);

        unsafe
        {
            var getTable = (delegate* unmanaged[Stdcall]<IntPtr>)_library.GetExport("GetInputPluginTable");
            _pluginTable = (INPUT_PLUGIN_TABLE*)getTable();
        }
    }

    /// <summary>
    /// IUseCache 実装 → InitializeCache エクスポートが生成され、
    /// 注入したCACHE_HANDLE経由でキャッシュの作成・書き込み・読み戻し・解放が機能することを検証
    /// </summary>
    [Fact]
    public void CacheWorkflow_ShouldCreateWriteReadAndRelease()
    {
        _cacheBuffer = Marshal.AllocHGlobal(Width * Height * sizeof(byte) * 4);
        _cachedWidth = 0;
        _cachedHeight = 0;
        _lastCacheName = null;
        _createCalls = 0;
        _getCalls = 0;
        _releaseCalls = 0;

        unsafe
        {
            var cacheHandle = (CACHE_HANDLE*)Marshal.AllocHGlobal(sizeof(CACHE_HANDLE));
            try
            {
                *cacheHandle = default;
                cacheHandle->get_image_cache = &HostGetImageCache;
                cacheHandle->create_image_cache = &HostCreateImageCache;

                // 1. CACHE_HANDLEをプラグインに注入
                var initializeCache = (delegate* unmanaged[Stdcall]<IntPtr, void>)_library.GetExport("InitializeCache");
                initializeCache((IntPtr)cacheHandle);

                // 2. func_open → func_read_video でキャッシュを使う処理を実行
                var filePathPtr = Marshal.StringToHGlobalUni("dummy");
                var readBuffer = Marshal.AllocHGlobal(Width * Height * 4);
                try
                {
                    var handle = _pluginTable->func_open(filePathPtr);
                    Assert.NotEqual(IntPtr.Zero, handle);

                    const int frame = 6;
                    var readSize = _pluginTable->func_read_video(handle, frame, readBuffer);

                    // 3. キャッシュ経由で往復したデータの検証 (r=frame+1, g=2, b=3, a=4)
                    Assert.Equal(Width * Height * 4, readSize);
                    var pixels = new ReadOnlySpan<byte>((void*)readBuffer, readSize);
                    for (int i = 0; i < Width * Height; i++)
                    {
                        Assert.Equal(frame + 1, pixels[i * 4 + 0]); // r
                        Assert.Equal(2, pixels[i * 4 + 1]);         // g
                        Assert.Equal(3, pixels[i * 4 + 2]);         // b
                        Assert.Equal(4, pixels[i * 4 + 3]);         // a
                    }

                    Assert.True(_pluginTable->func_close(handle));
                }
                finally
                {
                    Marshal.FreeHGlobal(filePathPtr);
                    Marshal.FreeHGlobal(readBuffer);
                }

                // 4. ホスト側の呼び出し検証
                Assert.Equal(1, _createCalls);
                Assert.Equal(1, _getCalls);
                Assert.Equal("e2e", _lastCacheName);
                Assert.Equal(Width, _cachedWidth);
                Assert.Equal(Height, _cachedHeight);
                // C++デストラクタ相当のfunc_releaseがcreate/get両方の参照に対して呼ばれた
                Assert.Equal(2, _releaseCalls);
            }
            finally
            {
                Marshal.FreeHGlobal((IntPtr)cacheHandle);
                Marshal.FreeHGlobal(_cacheBuffer);
                _cacheBuffer = IntPtr.Zero;
            }
        }
    }

    #region ホスト側コールバック(CACHE_HANDLEの関数ポインタ, sret規約)

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe CACHE_IMAGE* HostCreateImageCache(CACHE_IMAGE* ret, void* identifier, IntPtr name, int width, int height)
    {
        _createCalls++;
        _lastCacheName = Marshal.PtrToStringUni(name);
        _cachedWidth = width;
        _cachedHeight = height;

        ret->reference.func_release = &HostRelease;
        ret->reference.cache_instance = (void*)0x1234;
        ret->buffer = (PIXEL_RGBA*)_cacheBuffer;
        ret->width = width;
        ret->height = height;
        return ret;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe CACHE_IMAGE* HostGetImageCache(CACHE_IMAGE* ret, void* identifier, IntPtr name)
    {
        _getCalls++;

        if (_cachedWidth == 0 || Marshal.PtrToStringUni(name) != _lastCacheName)
        {
            // キャッシュなし: bufferがnull(falseに相当)の参照を返す
            *ret = default;
            return ret;
        }

        ret->reference.func_release = &HostRelease;
        ret->reference.cache_instance = (void*)0x5678;
        ret->buffer = (PIXEL_RGBA*)_cacheBuffer;
        ret->width = _cachedWidth;
        ret->height = _cachedHeight;
        return ret;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static unsafe void HostRelease(void* instance)
    {
        _releaseCalls++;
    }

    #endregion

    public void Dispose()
    {
        _library.Dispose();
        unsafe
        {
            _pluginTable = null;
        }
    }
}
