using Microsoft.Playwright;
using System.Text.RegularExpressions;
namespace SslYon;

public static class Natro
{
    const string U = "https://www.natro.com/musteri/";

    // Giriş ekranı → kullanıcı giriş yapana kadar bekle (2FA/captcha kullanıcıda)
    public static async Task Giris(Br b)
    {
        await b.P.GotoAsync(U);
        if (b.P.Url.Contains("/customer/login", StringComparison.OrdinalIgnoreCase))
        {
            Log.W("Natro girişi bekleniyor — tarayıcıda giriş yap (10 dk)");
            await b.P.WaitForURLAsync(new Regex(@"natro\.com/musteri/", RegexOptions.IgnoreCase), new() { Timeout = 600_000 });
        }
        Log.W("Natro: giriş tamam");
    }

    // Web Hosting Hesapları → ilgili satırda "Yönet" → HOSTINGAktifler2.asp
    public static async Task Hosting(Br b, Cfg c)
    {
        await b.P.GotoAsync(U + "HOSTINGAktifler1.asp");
        var r = b.P.Locator("tr").Filter(new() { HasText = c.HostingNo });
        var y = (await r.CountAsync() > 0 ? r.First : b.P.Locator("body")).GetByText("Yönet", new() { Exact = true }).First;
        await y.ClickAsync();
        await b.P.WaitForURLAsync(new Regex("HOSTINGAktifler2", RegexOptions.IgnoreCase));
        Log.W("Natro: hosting paneli açık");
    }

    // Alanın satırı: içinde zoneName={alan}' geçen DNS bağlantısı olan <tr> (sondaki ' önek eşleşmesini önler)
    static string DnsSec(Cfg c) => $"a[onclick*=\"zoneName={c.Alan}'\"]";
    static ILocator Satir(Br b, Cfg c) => b.P.Locator("tr[role=row]:visible").Filter(new() { Has = b.P.Locator(DnsSec(c)) }).First;

    // 1) onclick'teki adrese doğrudan git (modal/iframe yok)  2) olmazsa satırın açılır menüsünden modal
    static bool DnsSayfasi(Br b, Cfg c) => b.P.Url.Contains("/Dns/Records?zoneName=" + c.Alan, StringComparison.OrdinalIgnoreCase);

    public static async Task<ILocator> DnsAc(Br b, Cfg c)
    {
        if (DnsSayfasi(b, c))
        {   // zaten açık: yalnızca yenile
            Log.W("DNS sayfası zaten açık → yenileniyor");
            await b.P.ReloadAsync();
            var bd = b.P.Locator("body");
            await bd.GetByText("Text(TXT) Kayıtları").First.WaitForAsync(new() { Timeout = 30000 });
            return bd;
        }
        var ls = b.P.Locator(DnsSec(c)); var n = await ls.CountAsync();
        if (n == 0) throw new Exception($"'{c.Alan}' için DNS bağlantısı bulunamadı");
        Log.W($"DNS bağlantısı: {n} eşleşme (aynı alan; ilki kullanılıyor)");
        var u = Regex.Match(await ls.First.GetAttributeAsync("onclick") ?? "", @"'(/musteri/HostingPanelineGit_v2\.asp\?[^']+)'").Groups[1].Value;
        ILocator m;
        try
        {
            if (u == "") throw new PlaywrightException("onclick adresi çözülemedi");
            Log.W("DNS sayfası doğrudan: " + u);
            await b.P.GotoAsync("https://www.natro.com" + u);
            m = b.P.Locator("body");
            await m.GetByText("Text(TXT) Kayıtları").First.WaitForAsync(new() { Timeout = 30000 });
        }
        catch (PlaywrightException e)
        {
            Log.W("Doğrudan açılamadı (" + e.Message.Split('\n')[0] + ") → menüden açılıyor");
            await Hosting(b, c);
            var r = Satir(b, c);
            await r.Locator("button.dropdown-toggle").ClickAsync();
            await r.Locator(DnsSec(c)).ClickAsync();
            m = b.P.Locator(".modal:visible, [role=dialog]:visible").Filter(new() { HasText = $"DNS Yönetimi ({c.Alan})" }).Last;
            await m.WaitForAsync(new() { Timeout = 15000 });
            if (await m.Locator("iframe").CountAsync() > 0) { Log.W("DNS penceresi iframe içinde"); m = m.FrameLocator("iframe").First.Locator("body"); }
            await m.GetByText("Text(TXT) Kayıtları").First.WaitForAsync(new() { Timeout = 30000 });
        }
        // GÜVENLİK KİLİDİ: yanlış alanın DNS'i açıldıysa hiçbir şey doldurma
        var tx = await m.InnerTextAsync();
        if (!b.P.Url.Contains("zoneName=" + c.Alan, StringComparison.OrdinalIgnoreCase) && !tx.Contains(c.Alan))
        { await b.Foto("natro_yanlis_alan"); throw new Exception($"Açılan DNS sayfası {c.Alan} değil — durduruldu"); }
        await b.Kesif("natro_dns");
        Log.W($"Natro: DNS Yönetimi ({c.Alan}) açık ✓ doğrulandı");
        return m;
    }

    // Satırdaki "cPanel'e Bağlan" adresi (hostName={alan}%26full=true)
    public static async Task<string> CpAdres(Br b, Cfg c)
    {
        var a = b.P.Locator($"a[href*=\"hostName={c.Alan}%26full=true\"]").First;
        var h = await a.GetAttributeAsync("href") ?? throw new Exception("cPanel bağlantısı bulunamadı");
        return h.StartsWith("http") ? h : "https://www.natro.com" + h;
    }

    static ILocator Dugme(ILocator k, string rx) =>
        k.Locator("button:visible, a:visible, input[type=submit]:visible, input[type=button]:visible")
         .Filter(new() { HasTextRegex = new Regex(rx, RegexOptions.IgnoreCase) });

    // Oturum varsa cloudpanel DNS sayfasına doğrudan git; yoksa giriş → hosting yolu
    static async Task Hazirla(Br b, Cfg c)
    {
        if (DnsSayfasi(b, c)) return;
        try
        {
            await b.P.GotoAsync("https://cloudpanel.natro.com/Dns/Records?zoneName=" + c.Alan, new() { Timeout = 30000 });
            if (DnsSayfasi(b, c)) { Log.W("DNS sayfası doğrudan açıldı (oturum geçerli)"); return; }
        }
        catch (PlaywrightException e) { Log.W("Doğrudan DNS açılamadı: " + e.Message.Split('\n')[0]); }
        await Giris(b); await Hosting(b, c);
    }

    public static async Task<bool> TxtEkle(Br b, Cfg c, string ad, string tok)
    {
        await Hazirla(b, c);
        var m = await DnsAc(b, c);
        if ((await m.InnerTextAsync()).Contains(tok)) { Log.W("Bu token zaten kayıtlı"); return true; }

        var yeni = m.GetByText("Yeni Kayıt (TXT)", new() { Exact = true }).First;
        await yeni.ScrollIntoViewIfNeededAsync(); await yeni.ClickAsync();
        await b.P.WaitForTimeoutAsync(1200);
        await b.Kesif("natro_txt_form");

        var n = ad;
        if (c.TxtAdBicim == "kisa" && n.EndsWith("." + c.Alan)) n = n[..^(c.Alan.Length + 1)];

        // Keşiften: input#Name (ph=Örnek TXT Key), textarea#Value (ph=Örnek TXT Value), submit "Değiştir"
        var fa = await b.Bul("TXT ad", c.NatroTxtAd, "input#Name:visible", "input[placeholder='Örnek TXT Key']:visible");
        var fd = await b.Bul("TXT değer", c.NatroTxtDeger, "textarea#Value:visible", "textarea[placeholder='Örnek TXT Value']:visible");
        if (fa == null || fd == null) { Log.W("✗ TXT form alanları bulunamadı"); await b.Foto("natro_hata"); return false; }
        await Br.Isaret(fa); await fa.FillAsync(n);
        await Br.Isaret(fd); await fd.FillAsync(tok);
        Log.W($"Dolduruldu: {n} = {tok}");

        var kd = await b.Bul("TXT kaydet", c.NatroTxtKaydet,
            "form:has(#Value) button[type=submit]:has-text('Değiştir'):visible", "button.btn-success:has-text('Değiştir'):visible");
        if (kd == null) { Log.W("✗ Değiştir düğmesi bulunamadı"); await b.Foto("natro_hata"); return false; }
        if (!await b.Isle(kd, "natro_txt_kaydet")) return b.M == Mod.Prova;

        await b.P.WaitForTimeoutAsync(2500);
        var ok = (await m.InnerTextAsync()).Contains(tok) || (await b.P.InnerTextAsync("body")).Contains(tok);
        await b.Foto("natro_txt_sonra");
        Log.W(ok ? "✓ TXT kaydı listede görünüyor" : "✗ TXT kaydı listede görünmüyor");
        return ok;
    }

    public static async Task TxtSil(Br b, Cfg c, string tok)
    {
        await Hazirla(b, c);
        var m = await DnsAc(b, c);
        var r = m.Locator("tr").Filter(new() { HasText = tok });
        if (await r.CountAsync() == 0) { Log.W("Silinecek TXT bulunamadı"); return; }
        var s = r.First.Locator("button, a").Last;
        if (await b.Isle(s, "natro_txt_sil"))
        {   // olası onay penceresi
            var e = Dugme(b.P.Locator("body"), @"^\s*(Evet|Sil|Tamam|Onayla)\s*$");
            if (await e.CountAsync() > 0) await b.Isle(e.Last, "natro_sil_onay");
        }
    }
}
