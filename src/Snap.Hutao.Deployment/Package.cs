using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class Package
{
    public static bool EnsurePackage(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            return false;
        }

        try
        {
            using (FileStream packageStream = File.OpenRead(packagePath))
            {
                using (new ZipArchive(packageStream, ZipArchiveMode.Read))
                {
                    return true;
                }
            }
        }
        catch (InvalidDataException)
        {
            File.Delete(packagePath);
            return false;
        }
    }

    public static async Task DownloadPackageAsync(string packagePath)
    {
        using (HttpClientHandler handler = new() { UseCookies = false })
        {
            using (HttpClient httpClient = new(handler))
            {
                HttpShardCopyWorkerOptions<DownloadStatus> options = new()
                {
                    HttpClient = httpClient,
                    SourceUrl = "https://api.snapgenshin.com/patch/hutao/download",
                    DestinationFilePath = packagePath,
                    StatusFactory = (bytesRead, totalBytes) => new DownloadStatus(bytesRead, totalBytes),
                };

                using (HttpShardCopyWorker<DownloadStatus> worker = await HttpShardCopyWorker<DownloadStatus>.CreateAsync(options).ConfigureAwait(false))
                {
                    Progress<DownloadStatus> progress = new(ConsoleWriteProgress);
                    await worker.CopyAsync(progress).ConfigureAwait(false);
                }
            }
        }

        static void ConsoleWriteProgress(DownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }
}