using System.Runtime.InteropServices;

namespace AviUtlPluginNet.AbstractionsTests.Utils;

public class PluginFixture : IDisposable
{
    /// <summary>
    /// テスト用のプラグイン実装コード。テストクラスから設定可能
    /// </summary>
    public static string? CustomPluginImpl { get; set; }

    public string DllPath { get; }

    private string TmpDirPath { get; }
    private string _PublishDirPath => Path.Combine(TmpDirPath, "publish");

    public PluginFixture()
    {
        var tmpDir = Directory.CreateTempSubdirectory("AviUtlPluginNet.AbstractionsTests");
        TmpDirPath = tmpDir.FullName;

        var ext = GetPlatformExtension();
        DllPath = Path.Combine(_PublishDirPath, "TestPlugin" + ext);
    }

    private bool _isBuilt = false;
    private readonly object _buildLock = new object();

    public void EnsureBuilt()
    {
        if (_isBuilt) return;

        lock (_buildLock)
        {
            if (_isBuilt) return;

            if (CustomPluginImpl != null)
            {
                BuildTestPlugin(CustomPluginImpl);
                _isBuilt = true;
            }
            else
            {
                throw new InvalidOperationException("CustomPluginImpl must be set before running tests.");
            }
        }
    }

    private static string GetPlatformExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ".dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ".dylib";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ".so";
        }
        else
        {
            throw new PlatformNotSupportedException($"Unsupported platform: {RuntimeInformation.OSDescription}");
        }
    }

    public void BuildTestPlugin(string pluginImpl)
    {
        File.Copy("templates/TestPlugin.csproj.template", Path.Combine(TmpDirPath, "TestPlugin.csproj"), true);
        File.WriteAllText(Path.Combine(TmpDirPath, "TestPlugin.cs"), pluginImpl);

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish -c Debug -o \"{_PublishDirPath}\" --use-current-runtime --self-contained -p:NativeLib=Shared -p:ProjectSolutionDir={Path.GetFullPath("../../../../../src")} -p:RestoreLockedMode=false",
                WorkingDirectory = TmpDirPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new Exception($"Failed to build test plugin: {process.StandardOutput.ReadToEnd()}");
        }
    }

    public void Dispose()
    {
        Directory.Delete(TmpDirPath, true);
    }
}