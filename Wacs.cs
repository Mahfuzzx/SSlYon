using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace SslYon;

public static class Wacs
{
    const string Z = "https://acme.zerossl.com/v2/DV90", LE = "https://acme-staging-v02.api.letsencrypt.org/";

    // settings.json: script zaman aşımı 600→3600 sn, DNS ön doğrulama 5→20 deneme
    public static void Ayarla(Cfg c)
    {
        var d = Path.GetDirectoryName(c.Wacs)!; var f = Path.Combine(d, "settings.json");
        if (!File.Exists(f))
        {
            var s = Path.Combine(d, "settings_default.json");
            if (!File.Exists(s)) { Log.W("settings.json yok (wacs bir kez çalışınca oluşur)"); return; }
            File.Copy(s, f);
        }
        var j = JsonNode.Parse(File.ReadAllText(f), null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        (j["Script"] ??= new JsonObject())["Timeout"] = 3600;
        var v = (j["Validation"] ??= new JsonObject()); v["PreValidateDnsRetryCount"] = 20;
        v["DisableMultiThreading"] = false; // true iken --dnsscriptparallelism yok sayılır, kök ve wildcard yine tek tek doğrulanır
        // ZeroSSL doğrulamayı dakikalarca "processing"de tutabiliyor; varsayılan 15×5 sn yetmiyor → 60×10 sn
        var ac = (j["Acme"] ??= new JsonObject()); ac["RetryCount"] = 60; ac["RetryInterval"] = 10;
        File.WriteAllText(f, j.ToJsonString(new() { WriteIndented = true }));
        Log.W("wacs settings.json: Script.Timeout=3600, PreValidateDnsRetryCount=20, DisableMultiThreading=false, Acme.RetryCount=60, Acme.RetryInterval=10");
    }

    public static string PemDir(Cfg c, bool test) => test ? c.Pem + "-test" : c.Pem;
    public static bool Var(Cfg c, bool test) => File.Exists(Path.Combine(PemDir(c, test), c.Ad + "-crt.pem"));

    // test=true → Let's Encrypt staging (--test), ZeroSSL'e dokunmaz
    public static async Task<int> Calistir(Cfg c, bool test, Mod m)
    {
        var id = test ? c.Ad + "-test" : c.Ad;
        Directory.CreateDirectory(PemDir(c, test)); // wacs klasörü kendisi açmıyor
        var bu = $" --baseuri {(test ? LE + " --nocache" : Z)}"; // prova: önbellek yok → her seferinde tam akış // --test konsoldan soru soruyor (gizli pencerede takılır) → staging'e doğrudan
        string a = test || !Var(c, test) // staging provası her seferinde baştan (yarım kalmış kayıt sorun olmasın)
            ? $"--source manual --host {string.Join(",", c.Host)} --id {id} --emailaddress {c.Mail} --accepttos" +
              $" --validationmode dns-01 --validation script --dnsscript \"{Environment.ProcessPath}\"" +
              // 2: kök ve wildcard TXT'leri önce birlikte eklenir, sonra birlikte doğrulanır. Aynı ad (_acme-challenge) için
              // tek tek ekle-doğrula-sil yapınca CA'nın çözücüsü ilk cevabı (Natro TTL 3600) önbelleğe alıp ikinciyi görmüyor
              " --dnsscriptparallelism 2" +
              $" --store pemfiles --pemfilespath \"{PemDir(c, test)}\" --pemfilesname {c.Ad} --notaskscheduler --verbose" +
              bu + (test ? "" : $" --eab-key-identifier {c.EabKid} --eab-key {c.EabKey}")
            : $"--renew --id {id} --force --verbose" + bu;

        Log.W("wacs " + Regex.Replace(a, @"--eab-key \S+", "--eab-key ***"));
        var psi = new ProcessStartInfo(c.Wacs, a) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        psi.Environment["SSLYON_MOD"] = m.ToString();
        using var p = Process.Start(psi)!;
        p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.W("wacs> " + e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.W("wacs! " + e.Data); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.StandardInput.Close(); // soru sorarsa beklemesin
        await p.WaitForExitAsync();
        Log.W("wacs çıkış kodu: " + p.ExitCode);
        return p.ExitCode;
    }

    public static (string crt, string key, string cab)? Pem(Cfg c, bool test)
    {
        string F(string s) => Path.Combine(PemDir(c, test), $"{c.Ad}-{s}.pem");
        if (!File.Exists(F("crt"))) { Log.W("PEM yok: " + PemDir(c, test)); return null; }
        return (File.ReadAllText(F("crt")), File.ReadAllText(F("key")), File.ReadAllText(F("chain-only")));
    }
}
