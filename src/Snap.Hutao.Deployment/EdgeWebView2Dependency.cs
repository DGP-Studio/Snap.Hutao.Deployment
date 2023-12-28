using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static partial class EdgeWebView2Dependency
{
    private const string EdgeWebView2DownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string EdgeWebView2PerUserPath = @"HKEY_CURRENT_USER\Software\Microsoft\EdgeUpdate\Clients";
    private const string EdgeWebView2PerMachinePath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients";
    private const string EdgeWebView2GuidKey = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    public static async Task EnsureAsync()
    {
        if (Registry.GetValue(EdgeWebView2PerUserPath, EdgeWebView2GuidKey, null) is not null || Registry.GetValue(EdgeWebView2PerMachinePath, EdgeWebView2GuidKey, null) is not null)
        {
            Console.WriteLine("WebView2 already installed.");
            return;
        }

        Console.WriteLine("WebView2 not found, start downloading and installing WebView2...");
        await DownloadWebView2InstallerAndInstallAsync().ConfigureAwait(false);
    }

    private static async Task DownloadWebView2InstallerAndInstallAsync()
    {
        string webView2InstallerPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
        try
        {
            using (HttpClient httpClient = new())
            {
                HttpShardCopyWorkerOptions<DownloadStatus> options = new()
                {
                    HttpClient = httpClient,
                    SourceUrl = EdgeWebView2DownloadUrl,
                    DestinationFilePath = webView2InstallerPath,
                    StatusFactory = (bytesRead, totalBytes) => new DownloadStatus(bytesRead, totalBytes),
                };

                using (HttpShardCopyWorker<DownloadStatus> worker = await HttpShardCopyWorker<DownloadStatus>.CreateAsync(options).ConfigureAwait(false))
                {
                    Progress<DownloadStatus> progress = new(ConsoleWriteProgress);
                    await worker.CopyAsync(progress).ConfigureAwait(false);
                }
            }

            Console.WriteLine("Start installing WebView2...");
            Process installerProcess = new()
            {
                StartInfo = new()
                {
                    Arguments = "/silent /install",
                    FileName = webView2InstallerPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            using (installerProcess)
            {
                installerProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                installerProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
                installerProcess.Start();
                Console.WriteLine("-----> WebView2 Output begin -----");
                installerProcess.BeginOutputReadLine();
                installerProcess.BeginErrorReadLine();

                await installerProcess.WaitForExitAsync().ConfigureAwait(false);
                Console.WriteLine("<----- WebView2 Output end -------");
            }
        }
        finally
        {
            if (File.Exists(webView2InstallerPath))
            {
                File.Delete(webView2InstallerPath);
            }
        }

        static void ConsoleWriteProgress(DownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }
}
