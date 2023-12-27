using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class PackageDownload
{
    public static async Task DownloadPackageAsync(string packagePath)
    {
        using (HttpClient httpClient = new())
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

        static void ConsoleWriteProgress(DownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }
}