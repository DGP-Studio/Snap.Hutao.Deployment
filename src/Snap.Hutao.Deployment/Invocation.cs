using System;
using System.CommandLine.Invocation;
using System.Diagnostics;
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

        if (!isUpdateMode)
        {
            AllocConsole();
        }

        ArgumentException.ThrowIfNullOrEmpty(path);

        Console.WriteLine($"""
            Snap Hutao Deployment Tool [1.16.2]
            PackagePath: {path}
            FamilyName: {name}
            ------------------------------------------------------------
            """);

        try
        {
            if (!Package.EnsurePackage(path))
            {
                Console.WriteLine("""
                    未找到包文件或包文件损坏。
                    Package file not found or corrupted.
                    """);

                if (isUpdateMode)
                {
                    await ExitAsync(true).ConfigureAwait(false);
                    return;
                }
                else
                {
                    Console.WriteLine("""
                        开始下载包文件...
                        Start downloading package...
                        """);
                    await Package.DownloadPackageAsync(path).ConfigureAwait(false);
                }
            }

            await Certificate.EnsureGlobalSignCodeSigningRootR45Async().ConfigureAwait(false);
            await RunDeploymentCoreAsync(path, name, isUpdateMode).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"""
                Exception occured:
                {ex}
                """);
        }
        finally
        {
            await ExitAsync(isUpdateMode).ConfigureAwait(false);
        }
    }

    private static async Task RunDeploymentCoreAsync(string path, string? name, bool isUpdateMode)
    {
        Console.WriteLine("""
            初始化 PackageManager...
            Initializing PackageManager...
            """);
        PackageManager packageManager = new();
        AddPackageOptions addPackageOptions = new()
        {
            ForceAppShutdown = true,
            RetainFilesOnFailure = true,
        };

        Console.WriteLine("""
            开始部署...
            Start deploying...
            """);
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
            Console.WriteLine("""
                包部署成功。
                Package deployed.
                """);
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("""
                    未提供 FamilyName，正在枚举包。
                    FamilyName not provided, enumerating packages.
                    """);

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
                        if (ex.HResult is not (unchecked((int)0x80073B1F) or unchecked((int)0x80070490)))
                        {
                            throw;
                        }
                    }
                }
            }

            Console.WriteLine("""
                正在启动应用...
                Starting app...
                """);
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

    private static async ValueTask ExitAsync(bool isUpdateMode)
    {
        if (!isUpdateMode)
        {
            Console.WriteLine("""
                按下回车键退出...
                Press enter to exit...
                """);
            while (Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                //Pending enter key
            }
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