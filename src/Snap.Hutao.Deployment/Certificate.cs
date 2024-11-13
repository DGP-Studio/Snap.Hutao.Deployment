using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal static class Certificate
{
    private const string CertificateName = "GlobalSign Code Signing Root R45";
    private const string CertificateUrl = "https://secure.globalsign.com/cacert/codesigningrootr45.crt";

    public static async Task EnsureGlobalSignCodeSigningRootR45Async()
    {
        using (X509Store store = new(StoreName.Root, StoreLocation.LocalMachine))
        {
            store.Open(OpenFlags.ReadWrite);
            if (store.Certificates.Any(cert => cert.FriendlyName is CertificateName))
            {
                Console.WriteLine("""
                    已找到证书 [GlobalSign Code Signing Root R45]
                    Certificate [GlobalSign Code Signing Root R45] found
                    """);
                return;
            }

            Console.WriteLine("""
                无法找到所需证书 [GlobalSign Code Signing Root R45]，正在从 GlobalSign 下载
                Required Certificate [GlobalSign Code Signing Root R45] not found, downloading from GlobalSign
                """);

            using (HttpClient httpClient = new())
            {
                byte[] rawData = await httpClient.GetByteArrayAsync(CertificateUrl).ConfigureAwait(false);

                Console.WriteLine("""
                    正在向 本地计算机/受信任的根证书颁发机构 添加证书
                    如果你无法理解弹窗中的文本，请点击 [是] 

                    Adding certificate to LocalMachine/ThirdParty Root CA store,
                    please click [yes] on the [Security Waring] dialog

                    关于更多安全信息，请查看下方的网址
                    For more security information, please visit the url down below
                    https://support.globalsign.com/ca-certificates/root-certificates/globalsign-root-certificates
                    """);
                
                store.Add(X509CertificateLoader.LoadCertificate(rawData));
            }
        }
    }
}