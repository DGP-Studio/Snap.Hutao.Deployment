using System;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace Snap.Hutao.Deployment;

internal static class Invocation
{
    public static async Task RunDeploymentAsync(InvocationContext context)
    {
        string? path = context.ParseResult.GetValueForOption(InvocationOptions.PackagePath);
        string? name = context.ParseResult.GetValueForOption(InvocationOptions.FamilyName);
        bool isUpdateMode = context.ParseResult.GetValueForOption(InvocationOptions.UpdateBehavior);

        ArgumentException.ThrowIfNullOrEmpty(path);

        Console.WriteLine($"""
            PackagePath: {path}
            FamilyName: {name}
            ------------------------------------------------------------
            """);

        if (!File.Exists(path))
        {
            Console.WriteLine($"Package file not found.");

            if (isUpdateMode)
            {
                Console.WriteLine("Exit in 10 seconds...");
                await Task.Delay(10000);
                return;
            }
            else
            {
                Console.WriteLine("Start downloading package...");
                await DownloadPackageAsync(path);
            }
        }

        try
        {
            Console.WriteLine("Initializing PackageManager...");
            PackageManager packageManager = new();
            AddPackageOptions addPackageOptions = new()
            {
                ForceAppShutdown = true,
                RetainFilesOnFailure = true,
                StageInPlace = true,
            };

            Console.WriteLine("Start deploying...");
            IProgress<DeploymentProgress> progress = new Progress<DeploymentProgress>(p =>
            {
                Console.WriteLine($"[Deploying]: State: {p.state} Progress: {p.percentage}%");
            });
            DeploymentResult result = await packageManager.AddPackageByUriAsync(new Uri(path), addPackageOptions).AsTask(progress);

            if (result.IsRegistered)
            {
                Console.WriteLine("Package deployed.");
                if (string.IsNullOrEmpty(name))
                {
                    Console.WriteLine("FamilyName not provided, enumerating packages.");

                    foreach (Windows.ApplicationModel.Package package in packageManager.FindPackages())
                    {
                        
                        if (package is { DisplayName: "Snap Hutao", PublisherDisplayName: "DGP Studio" })
                        {
                            name = package.Id.FamilyName;
                            Console.WriteLine($"Package found: {name}");
                        }
                    }
                }

                Console.WriteLine("Starting app...");
                Process.Start(new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = $@"shell:AppsFolder\{name}!App",
                });
            }
            else
            {
                Console.WriteLine($"""
                    ActivityId: {result.ActivityId}
                    ExtendedErrorCode: {result.ExtendedErrorCode}
                    ErrorText: {result.ErrorText}

                    Exit in 10 seconds...
                    """);

                await Task.Delay(10000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"""
                Exception occured:
                {ex}

                Exit in 10 seconds...
                """);

            await Task.Delay(10000);
        }
    }

    private static async Task DownloadPackageAsync(string packagePath)
    {
        using (HttpClient httpClient = new())
        {
            HttpShardCopyWorkerOptions<PackageDownloadStatus> options = new()
            {
                HttpClient = httpClient,
                SourceUrl = "https://api.snapgenshin.com/patch/hutao/download",
                DestinationFilePath = packagePath,
                StatusFactory = (bytesRead, totalBytes) => new PackageDownloadStatus(bytesRead, totalBytes),
            };

            using (HttpShardCopyWorker<PackageDownloadStatus> worker = await HttpShardCopyWorker<PackageDownloadStatus>.CreateAsync(options).ConfigureAwait(false))
            {
                Progress<PackageDownloadStatus> progress = new(ConsoleWriteProgress);
                await worker.CopyAsync(progress).ConfigureAwait(false);
            }
        }

        static void ConsoleWriteProgress(PackageDownloadStatus status)
        {
            Console.Write($"\r{status.ProgressDescription}");
        }
    }
}