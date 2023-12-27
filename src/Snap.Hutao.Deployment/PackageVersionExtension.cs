using System;
using Windows.ApplicationModel;

namespace Snap.Hutao.Deployment;

internal static class PackageVersionExtension
{
    public static Version ToVersion(this PackageVersion packageVersion)
    {
        return new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, packageVersion.Revision);
    }
}