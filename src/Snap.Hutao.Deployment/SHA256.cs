// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class SHA256
{
    public static async Task<string> HashFileAsync(string filePath, CancellationToken token = default)
    {
        using (FileStream stream = File.OpenRead(filePath))
        {
            return await HashAsync(stream, token).ConfigureAwait(false);
        }
    }

    public static async Task<string> HashAsync(Stream stream, CancellationToken token = default)
    {
        byte[] bytes = await CryptographicOperations.HashDataAsync(HashAlgorithmName.SHA256, stream, token).ConfigureAwait(false);
        return Convert.ToHexString(bytes);
    }
}