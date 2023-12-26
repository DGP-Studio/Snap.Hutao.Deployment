using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class HttpClientExtension
{
    public static Task<HttpResponseMessage> HeadAsync(this HttpClient httpClient, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, HttpCompletionOption completionOption)
    {
        return httpClient.SendAsync(PrivateCreateRequestMessage(httpClient, HttpMethod.Get, CreateUri(default!, requestUri)), completionOption, CancellationToken.None);
    }

    // private HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri? uri)
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CreateRequestMessage")]
    private static extern HttpRequestMessage PrivateCreateRequestMessage(HttpClient httpClient, HttpMethod method, Uri? uri);

    // private static Uri? CreateUri(string? uri)
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CreateUri")]
    private static extern Uri? CreateUri(HttpClient discard, string? uri);
}