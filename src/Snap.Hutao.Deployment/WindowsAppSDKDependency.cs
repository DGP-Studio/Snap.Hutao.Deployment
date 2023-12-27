using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace Snap.Hutao.Deployment;

internal static partial class WindowsAppSDKDependency
{
    private const string SDKInstallerDownloadFormat = "https://aka.ms/windowsappsdk/{0}/{1}/windowsappruntimeinstall-x64.exe";

    public static async Task EnsureAsync(string packagePath)
    {
        using FileStream packageStream = File.OpenRead(packagePath);
        using ZipArchive package = new(packageStream, ZipArchiveMode.Read);

        (string packageName, string msixVersion) = await ExtractRuntimePackageNameAndMsixMinVersionFromAppManifestAsync(package).ConfigureAwait(false);
        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(msixVersion))
        {
            Console.WriteLine("No Windows App Runtime version found in Msix/AppxManifest.xml");
            return;
        }

        if (CheckRuntimeInstalled(packageName, msixVersion))
        {
            return;
        }

        string sdkVersion = await ExtractSDKVersionFromDepsJsonAsync(package).ConfigureAwait(false);

        if (string.IsNullOrEmpty(sdkVersion))
        {
            Console.WriteLine("No Windows App SDK version found in Msix/Snap.Hutao.deps.json");
            return;
        }

        Console.WriteLine("Start downloading SDK installer...");
        await DownloadWindowsAppRuntimeInstallAndInstallAsync(sdkVersion).ConfigureAwait(false);
    }

    private static async Task<string> ExtractSDKVersionFromDepsJsonAsync(ZipArchive package)
    {
        ZipArchiveEntry? depsJson = package.GetEntry("Snap.Hutao.deps.json");
        ArgumentNullException.ThrowIfNull(depsJson);

        using (StreamReader reader = new(depsJson.Open()))
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (WindowsAppSDKVersion().Match(line) is { Success: true } match)
                {
                    string sdkVersion = match.Groups[1].Value;
                    Console.WriteLine($"Using Windows App SDK version: {sdkVersion}");
                    return sdkVersion;
                }
            }
        }

        return string.Empty;
    }

    private static bool CheckRuntimeInstalled(string packageName, string msixVersion)
    {
        Version msixMinVersion = new(msixVersion);

        foreach (Windows.ApplicationModel.Package installed in new PackageManager().FindPackages())
        {
            if (installed.Id.Name == packageName && installed.Id.Version.ToVersion() >= msixMinVersion)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task DownloadWindowsAppRuntimeInstallAndInstallAsync(string version)
    {
        string sdkInstallerPath = Path.Combine(Path.GetTempPath(), "windowsappruntimeinstall-x64.exe");
        try
        {
            using (HttpClient httpClient = new())
            {
                HttpShardCopyWorkerOptions<DownloadStatus> options = new()
                {
                    HttpClient = httpClient,
                    SourceUrl = string.Format(SDKInstallerDownloadFormat, MajorMinorVersion().Match(version).Value, version),
                    DestinationFilePath = sdkInstallerPath,
                    StatusFactory = (bytesRead, totalBytes) => new DownloadStatus(bytesRead, totalBytes),
                };

                using (HttpShardCopyWorker<DownloadStatus> worker = await HttpShardCopyWorker<DownloadStatus>.CreateAsync(options).ConfigureAwait(false))
                {
                    Progress<DownloadStatus> progress = new(ConsoleWriteProgress);
                    await worker.CopyAsync(progress).ConfigureAwait(false);
                }
            }

            Console.WriteLine("Start installing SDK...");
            Process installerProcess = new()
            {
                StartInfo = new()
                {
                    FileName = sdkInstallerPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            using (installerProcess)
            {
                installerProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                installerProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
                installerProcess.Start();
                Console.WriteLine("-----> WindowsAppRuntimeInstall Output begin -----");
                installerProcess.BeginOutputReadLine();
                installerProcess.BeginErrorReadLine();

                await installerProcess.WaitForExitAsync().ConfigureAwait(false);
                Console.WriteLine("<----- WindowsAppRuntimeInstall Output end -------");
            }
        }
        finally
        {
            if (File.Exists(sdkInstallerPath))
            {
                File.Delete(sdkInstallerPath);
            }
        }

        static void ConsoleWriteProgress(DownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }

    private static async Task<(string PackageName, string MsixVersion)> ExtractRuntimePackageNameAndMsixMinVersionFromAppManifestAsync(ZipArchive package)
    {
        ZipArchiveEntry? appxManifest = package.GetEntry("AppxManifest.xml");
        ArgumentNullException.ThrowIfNull(appxManifest);

        using (StreamReader reader = new(appxManifest.Open()))
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (WindowsAppRuntimeMsixMinVersion().Match(line) is { Success: true } match)
                {
                    string packageName = match.Groups[1].Value;
                    string msixVersion = match.Groups[2].Value;
                    Console.WriteLine($"Using {packageName} version: {msixVersion}");
                    return (packageName, msixVersion);
                }
            }
        }

        return (string.Empty, string.Empty);
    }

    [GeneratedRegex("<PackageDependency Name=\"(Microsoft\\.WindowsAppRuntime.+?)\" MinVersion=\"(.+?)\"")]
    private static partial Regex WindowsAppRuntimeMsixMinVersion();

    [GeneratedRegex("\"Microsoft\\.WindowsAppSDK\": \"(.+?)\",")]
    private static partial Regex WindowsAppSDKVersion();

    [GeneratedRegex(@"\d+\.\d+")]
    private static partial Regex MajorMinorVersion();
}