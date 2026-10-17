using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
namespace SslYon;

public enum Mod { Prova, Onay, Oto } // Prova: doldur, tıklama | Onay: sor, tıkla | Oto: sormadan tıkla

public class Site
{
    public string Alan = "example.com";
    public string[] Host = { "example.com", "*.example.com" };
    public string[] Kur = { "example.com" };      // cPanel'de sertifikanın kurulacağı siteler (Alan Adı listesindeki adlar)
    public string HostingNo = "";                  // Natro hosting hesap kodu (Web Hosting Hesapları listesinde); boşsa ilk satır
    public string Ad = "site";                     // PEM dosya öneki + win-acme kayıt id'si (her sitede farklı olmalı)
}

public class Cfg
{
    public List<Site> Siteler = new() { new() };
    public string Secili = "";                      // arayüzde son seçilen site
    public string Mail = "", EabKid = "", EabKeyEnc = "", EabKeyDuz = ""; // EabKeyDuz'a yaz → ilk açılışta şifrelenip silinir
    public string Wacs = @"C:\win-acme\wacs.exe", Pem = @"C:\ssl\out";
    public string TxtAdBicim = "kisa";              // kisa: _acme-challenge | tam: _acme-challenge.example.com
    public string? NatroTxtAd, NatroTxtDeger, NatroTxtKaydet; // null → sezgisel; keşiften sonra seçici gir
    public int Esik = 30;                           // bitişe kaç gün kala uyar
    public bool TxtOto = false;                     // true: TXT ekle/sil onaysız (cPanel yüklemesi her zaman onaylı)
    public string Tarayici = "chromium";            // chromium (yönetici modunda çalışır) | msedge (yalnızca yetkisiz)

    // Etkin site: tüm adımlar bunun üzerinden çalışır
    [JsonIgnore] public Site S = null!;
    [JsonIgnore] public string Alan => S.Alan;
    [JsonIgnore] public string[] Host => S.Host;
    [JsonIgnore] public string[] Kur => S.Kur;
    [JsonIgnore] public string HostingNo => S.HostingNo;
    [JsonIgnore] public string Ad => S.Ad;
    public Cfg Sec(string? alan) { S = Siteler.FirstOrDefault(x => x.Alan.Equals(alan, StringComparison.OrdinalIgnoreCase)) ?? Siteler[0]; return this; }

    // _acme-challenge.yeni.example.com → example.com sitesi (en uzun sonek eşleşmesi)
    public Site? Kayittan(string kayit)
    {
        var h = kayit.TrimEnd('.');
        return Siteler.Where(x => h.Equals(x.Alan, StringComparison.OrdinalIgnoreCase) || h.EndsWith("." + x.Alan, StringComparison.OrdinalIgnoreCase))
                      .OrderByDescending(x => x.Alan.Length).FirstOrDefault();
    }

    [JsonIgnore] public string EabKey { get => Dp(EabKeyEnc, false); set => EabKeyEnc = Dp(value, true); }
    static string Dp(string s, bool e) => string.IsNullOrEmpty(s) ? "" : e
        ? Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser))
        : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser));

    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SslYon");
    public static string F => Path.Combine(Dir, "ayar.json");
    static readonly JsonSerializerOptions O = new() { IncludeFields = true, WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Cfg Yukle()
    {
        Directory.CreateDirectory(Dir);
        if (!File.Exists(F)) { var n = new Cfg().Sec(null); n.Kaydet(); return n; }
        var j = JsonNode.Parse(File.ReadAllText(F))!.AsObject();
        bool yaz = false;
        if (j["Siteler"] == null && j["Alan"] != null)
        {   // eski tek siteli biçim → Siteler[0]
            var s = new JsonObject();
            foreach (var k in new[] { "Alan", "Host", "Kur", "HostingNo", "Ad" })
                if (j[k] is { } v) { j.Remove(k); s[k] = v; }
            j["Siteler"] = new JsonArray(s); yaz = true;
        }
        var c = j.Deserialize<Cfg>(O)!;
        if (c.Siteler.Count == 0) c.Siteler.Add(new());
        if (c.EabKeyDuz != "") { c.EabKey = c.EabKeyDuz; c.EabKeyDuz = ""; yaz = true; }
        c.Sec(c.Secili);
        if (yaz) c.Kaydet();
        return c;
    }
    public void Kaydet() => File.WriteAllText(F, JsonSerializer.Serialize(this, O));
}

public static class Log
{
    public static event Action<string>? On;
    public static readonly string Dir = Path.Combine(Cfg.Dir, "log");
    public static string On_ek = "";   // script süreci: "script-" → ayrı dosya (UI ile çakışmasın)
    static readonly object K = new();
    public static void W(string s)
    {
        var l = $"{DateTime.Now:HH:mm:ss} {s}";
        try { Console.Out.WriteLine(l); Console.Out.Flush(); } catch { } // script modunda wacs çıktısına düşer
        lock (K)
            for (int i = 0; i < 5; i++)
                try
                {
                    Directory.CreateDirectory(Dir);
                    using var f = new FileStream(Path.Combine(Dir, $"{On_ek}{DateTime.Now:yyyyMMdd}.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    var b = Encoding.UTF8.GetBytes(l + Environment.NewLine); f.Write(b); break;
                }
                catch (IOException) { Thread.Sleep(50); }
                catch { break; } // log hatası asla süreci düşürmesin
        try { On?.Invoke(l); } catch { }
    }
}
