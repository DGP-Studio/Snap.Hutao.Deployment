using System;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace Snap.Hutao.Deployment;

internal static partial class Invocation
{
    public static async Task RunDeploymentAsync(InvocationContext context)
    {
        string? path = context.ParseResult.GetValueForOption(InvocationOptions.PackagePath);
        string? name = context.ParseResult.GetValueForOption(InvocationOptions.FamilyName);
        bool isUpdateMode = context.ParseResult.GetValueForOption(InvocationOptions.UpdateBehavior);
        bool installWebView2 = context.ParseResult.GetValueForOption(InvocationOptions.InstallWebView2);

        if (!isUpdateMode)
        {
            AllocConsole();
        }

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
                await ExitAsync(true).ConfigureAwait(false);
                return;
            }
            else
            {
                Console.WriteLine("Start downloading package...");
                await PackageDownload.DownloadPackageAsync(path).ConfigureAwait(false);
            }
        }

        await Certificate.EnsureGlobalSignCodeSigningRootR45Async().ConfigureAwait(false);
        await WindowsAppSDKDependency.EnsureAsync(path).ConfigureAwait(false);
        await EdgeWebView2Dependency.EnsureAsync(installWebView2, isUpdateMode).ConfigureAwait(false);
        await RunDeploymentCoreAsync(path, name, isUpdateMode).ConfigureAwait(false);
        await ExitAsync(isUpdateMode).ConfigureAwait(false);
    }

    private static async Task RunDeploymentCoreAsync(string path, string? name, bool isUpdateMode)
    {
        try
        {
            Console.WriteLine("Initializing PackageManager...");
            PackageManager packageManager = new();
            AddPackageOptions addPackageOptions = new()
            {
                ForceAppShutdown = true,
                RetainFilesOnFailure = true,
            };

            Console.WriteLine("Start deploying...");
            IProgress<DeploymentProgress> progress = new Progress<DeploymentProgress>(p =>
            {
                Console.WriteLine($"[Deploying]: State: {p.state} Progress: {p.percentage}%");
            });
            DeploymentResult result = await packageManager
                .AddPackageByUriAsync(new Uri(path), addPackageOptions)
                .AsTask(progress)
                .ConfigureAwait(false);

            if (result.IsRegistered)
            {
                Console.WriteLine("Package deployed.");
                if (string.IsNullOrEmpty(name))
                {
                    Console.WriteLine("FamilyName not provided, enumerating packages.");

                    foreach (Windows.ApplicationModel.Package package in packageManager.FindPackages())
                    {
                        try
                        {
                            if (package is { DisplayName: "Snap Hutao", PublisherDisplayName: "DGP Studio" })
                            {
                                name = package.Id.FamilyName;
                                Console.WriteLine($"Package found: {name}");
                            }
                        }
                        catch (COMException ex)
                        {
                            // ERROR_MRM_MAP_NOT_FOUND or ERROR_NOT_FOUND
                            if (ex.HResult is not unchecked((int)0x80073B1F) or unchecked((int)0x80070490))
                            {
                                throw;
                            }
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
                    """);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"""
                Exception occured:
                {ex}
                """);
        }
    }

    private static async ValueTask ExitAsync(bool isUpdateMode)
    {
        if (!isUpdateMode)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            FreeConsole();
        }
        else
        {
            Console.WriteLine("Exit in 10 seconds...");
            await Task.Delay(10000).ConfigureAwait(false);
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();
}