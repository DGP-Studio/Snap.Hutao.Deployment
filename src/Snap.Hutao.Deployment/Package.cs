﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class Package
{
    private const string HutaoPatchDownloadEndpoint = "https://api.snapgenshin.com/patch/hutao/download";

    public static async Task<bool> EnsurePackageAsync(string packagePath)
    {
        if (!File.Exists(packagePath))
        {
            return false;
        }

        if (packagePath.Equals(InvocationOptions.DefaultPackagePath, StringComparison.OrdinalIgnoreCase))
        {
            using (HttpClientHandler handler = new() { AllowAutoRedirect = false })
            {
                using (HttpClient httpClient = new(handler))
                {
                    using (HttpResponseMessage headResponse = await httpClient.HeadAsync(HutaoPatchDownloadEndpoint, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                    {
                        if (!headResponse.Headers.TryGetValues("X-Checksum-Sha256", out IEnumerable<string>? checksums))
                        {
                            return false;
                        }

                        string checksum = checksums.First();
                        string actualChecksum = await SHA256.HashFileAsync(packagePath).ConfigureAwait(false);
                        return checksum.Equals(actualChecksum, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
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
        using (HttpClient httpClient = new(new SocketsHttpHandler() { UseCookies = false }))
        {
            using (HttpResponseMessage responseMessage = await httpClient.GetAsync(HutaoPatchDownloadEndpoint, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                long totalBytes = responseMessage.Content.Headers.ContentLength ?? 0;
                using (Stream stream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    using (FileStream fileStream = File.Create(packagePath))
                    {
                        StreamCopyWorker<DownloadStatus> worker = new(stream, fileStream, (_, bytesRead) => new DownloadStatus(bytesRead, totalBytes));
                        Progress<DownloadStatus> progress = new(ConsoleWriteProgress);
                        await worker.CopyAsync(progress).ConfigureAwait(false);
                    }
                }
            }
        }

        static void ConsoleWriteProgress(DownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }
}