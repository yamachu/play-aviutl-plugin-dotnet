using System.Runtime.InteropServices;
using AviUtlPluginNet.Core.Interop.AUI2;
using AviUtlPluginNet.PreBuiltTests.Utils;

namespace AviUtlPluginNet.PreBuiltTests;

public class PreBuiltE2ETests : IDisposable
{
    private INativeInputPluginTableProvider _pluginTableProvider;
    private unsafe INPUT_PLUGIN_TABLE* _pluginTable = null;

    public PreBuiltE2ETests()
    {
        _pluginTableProvider = new NativePluginTableProviderDynamic($"vendor/{Environment.GetEnvironmentVariable("RUN_E2E_TARGET")}");

        var tablePtr = _pluginTableProvider.GetInputPluginTable();
        if (tablePtr != IntPtr.Zero)
        {
            unsafe
            {
                _pluginTable = (INPUT_PLUGIN_TABLE*)tablePtr;
            }
        }
    }


    [SkipEmptyEnvironmentVariable("RUN_E2E_TARGET")]
    public void GetInputPluginTable_ShouldReturnValidPointer()
    {
        try
        {
            var tablePtr = _pluginTableProvider.GetInputPluginTable();
            Assert.NotEqual(IntPtr.Zero, tablePtr);

            unsafe
            {
                var table = (INPUT_PLUGIN_TABLE*)tablePtr;
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_open);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_close);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_info_get);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_read_video);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)table->func_read_audio);
                // func_config is optional, so it can be IntPtr.Zero

                if (table->name != IntPtr.Zero)
                {
                    var name = Marshal.PtrToStringUni(table->name);
                    Assert.False(string.IsNullOrEmpty(name));
                }
            }
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message);
        }
    }

    [SkipEmptyEnvironmentVariable("RUN_E2E_TARGET")]
    public void NativeWorkflow_ShouldWorkEndToEnd()
    {

        unsafe
        {
            // 1. ファイルを開く（関数ポインタを使用）
            var filePathPtr = Marshal.StringToHGlobalUni("TODO:REPLACE_WITH_ACTUAL_FULL_PATH"); // FIXME: 実際のテスト画像や動画のフルパスを指定
            try
            {
                var handle = _pluginTable->func_open(filePathPtr);
                Assert.NotEqual(IntPtr.Zero, handle);

                // 2. 情報を取得
                var info = new INPUT_INFO();
                var infoPtr = new IntPtr(&info);
                var infoResult = _pluginTable->func_info_get(handle, infoPtr);
                Assert.True(infoResult);

                // 基本的な情報の検証
                Assert.NotEqual(InputFlag.None, info.flag);

                // 3. 映像データを読み取り
                const int bufferSize = 200 * 150 * 3; // FIXME: テスト画像および動画の1フレームあたりのサイズ
                IntPtr videoBuffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    var readSize = _pluginTable->func_read_video(handle, 0, videoBuffer);
                    Assert.True(readSize > 0);
                }
                finally
                {
                    Marshal.FreeHGlobal(videoBuffer);
                }

                // 4. 音声データを読み取り
                IntPtr audioBuffer = Marshal.AllocHGlobal(1000);
                try
                {
                    var audioSize = _pluginTable->func_read_audio(handle, 0, 1000, audioBuffer);
                    // Assert.Equal(0, audioSize); // 実際の読み込み可能なサイズでAssertを行う
                }
                finally
                {
                    Marshal.FreeHGlobal(audioBuffer);
                }

                // 5. ファイルを閉じる
                var closeResult = _pluginTable->func_close(handle);
                Assert.True(closeResult);
            }
            finally
            {
                Marshal.FreeHGlobal(filePathPtr);
            }
        }
    }

    public void Dispose()
    {
        if (_pluginTableProvider is IDisposable disp)
        {
            disp.Dispose();
        }
        unsafe
        {
            _pluginTable = null;
        }
    }
}
