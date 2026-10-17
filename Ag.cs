using DnsClient;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace SslYon;

public static class Ag
{
    // TXT: alanın yetkili NS'lerinde, sonra Google ve Cloudflare'de görünüyor mu?
    // Yetkililer önce: kayıt NS'e ulaşmadan genel çözücüye sorarsak "yok" cevabı 1 saat önbelleğe girer (Natro SOA 3600)
    public static async Task<bool> TxtVar(string ad, string tok, string zone)
    {
        var ips = new List<IPAddress>();
        try
        {
            var g = new LookupClient(IPAddress.Parse("8.8.8.8"));
            foreach (var ns in (await g.QueryAsync(zone, QueryType.NS)).Answers.NsRecords())
                foreach (var a in (await g.QueryAsync(ns.NSDName.Value, QueryType.A)).Answers.ARecords()) ips.Add(a.Address);
        }
        catch (Exception e) { Log.W("NS bulunamadı: " + e.Message); }
        ips.Add(IPAddress.Parse("8.8.8.8")); ips.Add(IPAddress.Parse("1.1.1.1"));

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

    // Sunucu o ada sertifika kurulu değilse varsayılanını verir → hata bilgisi de dönülür (ad uyuşmazlığı vb.)
    public static async Task<(X509Certificate2 x, SslPolicyErrors e)?> Sertifika(string h)
    {
        try
        {
            using var t = new TcpClient(); await t.ConnectAsync(h, 443);
            X509Certificate2? x = null; var e = SslPolicyErrors.None;
            using var s = new SslStream(t.GetStream(), false, (_, c, _, pe) => { if (c != null) x = new X509Certificate2(c); e = pe; return true; });
            await s.AuthenticateAsClientAsync(h);
            return x == null ? null : (x, e);
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
