using System;
using System.CommandLine;
using System.IO;

namespace Snap.Hutao.Deployment;

internal static class InvocationOptions
{
    public static readonly Option<string> PackagePath = new(
        "--package-path",
        () => Path.Combine(AppContext.BaseDirectory, "Snap.Hutao.msix"),
        "The path of the package to be deployed.");

    public static readonly Option<string> FamilyName = new(
        "--family-name",
        "The family name of the app to be updated.");

    public static readonly Option<bool> UpdateBehavior = new(
        "--update-behavior",
        () => false,
        "Change behavior of the tool into update mode");
}