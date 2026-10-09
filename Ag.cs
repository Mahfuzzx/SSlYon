using DnsClient;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace SslYon;

public static class Ag
{
    // TXT: Google, Cloudflare ve alanın yetkili NS'lerinde görünüyor mu?
    public static async Task<bool> TxtVar(string ad, string tok, string zone)
    {
        var ips = new List<IPAddress> { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1") };
        try
        {
            var g = new LookupClient(IPAddress.Parse("8.8.8.8"));
            foreach (var ns in (await g.QueryAsync(zone, QueryType.NS)).Answers.NsRecords())
                foreach (var a in (await g.QueryAsync(ns.NSDName.Value, QueryType.A)).Answers.ARecords()) ips.Add(a.Address);
        }
        catch (Exception e) { Log.W("NS bulunamadı: " + e.Message); }

        foreach (var ip in ips)
            try
            {
                var l = new LookupClient(new LookupClientOptions(ip) { UseCache = false, Timeout = TimeSpan.FromSeconds(5) });
                var r = await l.QueryAsync(ad, QueryType.TXT);
                if (!r.Answers.TxtRecords().SelectMany(t => t.Text).Contains(tok)) { Log.W($"DNS {ip}: henüz yok"); return false; }
            }
            catch (Exception e) { Log.W($"DNS {ip}: {e.Message}"); return false; }
        Log.W($"✓ TXT {ips.Count} sunucunun hepsinde görünüyor");
        return true;
    }

    public static async Task<bool> TxtBekle(string ad, string tok, string zone, int dk = 30)
    {
        for (var son = DateTime.Now.AddMinutes(dk); DateTime.Now < son; await Task.Delay(30000))
            if (await TxtVar(ad, tok, zone)) return true;
        Log.W("✗ TXT yayılma zaman aşımı"); return false;
    }

    public static async Task<X509Certificate2?> Sertifika(string h)
    {
        try
        {
            using var t = new TcpClient(); await t.ConnectAsync(h, 443);
            X509Certificate2? x = null;
            using var s = new SslStream(t.GetStream(), false, (_, c, _, _) => { if (c != null) x = new X509Certificate2(c); return true; });
            await s.AuthenticateAsClientAsync(h);
            return x;
        }
        catch (Exception e) { Log.W($"SSL kontrol {h}: {e.Message}"); return null; }
    }

    // Prova için kendinden imzalı sahte sertifika (asla kurulmaz)
    public static (string crt, string key, string cab) Sahte(string[] h)
    {
        using var r = RSA.Create(2048);
        var q = new CertificateRequest("CN=PROVA " + h[0].TrimStart('*', '.'), r, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); foreach (var x in h) san.AddDnsName(x);
        q.CertificateExtensions.Add(san.Build());
        using var c = q.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(7));
        var p = c.ExportCertificatePem();
        return (p, r.ExportPkcs8PrivateKeyPem(), p);
    }

    public static string Token() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
