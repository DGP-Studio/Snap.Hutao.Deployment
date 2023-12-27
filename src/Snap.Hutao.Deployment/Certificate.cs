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
            if (store.Certificates.Any(cert => cert.FriendlyName == CertificateName))
            {
                Console.WriteLine("Certificate [GlobalSign Code Signing Root R45] found");
                return;
            }

            Console.WriteLine("Required Certificate [GlobalSign Code Signing Root R45] not found, download from GlobalSign");

            using (HttpClient httpClient = new())
            {
                byte[] rawData = await httpClient.GetByteArrayAsync(CertificateUrl).ConfigureAwait(false);

                Console.WriteLine("""
                    正在向本地计算机/受信任的根证书颁发机构添加证书
                    如果你无法理解弹窗中的文本，请点击 [是] 

                    Adding certificate to LocalMachine/ThirdParty Root CA store,
                    please click [yes] on the [Security Waring] dialog

                    For more security information, please visit the url down below
                    https://support.globalsign.com/ca-certificates/root-certificates/globalsign-root-certificates
                    """);
                store.Add(new X509Certificate2(rawData));
            }
        }
    }
}