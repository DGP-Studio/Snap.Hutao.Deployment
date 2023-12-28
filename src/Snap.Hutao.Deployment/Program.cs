using System.CommandLine;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static partial class Program
{
    internal static async Task<int> Main(string[] args)
    {
        string description = $@"
            Snap Hutao Updater
            Copyright (c) DGP Studio. All rights reserved.
            ";
        RootCommand root = new(description);
        root.AddOption(InvocationOptions.PackagePath);
        root.AddOption(InvocationOptions.FamilyName);
        root.AddOption(InvocationOptions.UpdateBehavior);

        root.SetHandler(Invocation.RunDeploymentAsync);

        return await root.InvokeAsync(args);
    }
}