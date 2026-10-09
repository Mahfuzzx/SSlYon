using Microsoft.Playwright;
using System.Text.RegularExpressions;
namespace SslYon;

public static class Cp
{
    // Natro hosting paneli → "cPanel'e Bağlan" → cpsess oturum kökü (…/frontend/natro-thema/)
    static async Task<string> Ac(Br b, Cfg c)
    {
        await Natro.Giris(b); await Natro.Hosting(b, c);
        var u = await Natro.CpAdres(b, c); // satırın kendi bağlantısı: hostName={alan}
        Log.W("cPanel bağlantısı: " + u);
        await b.P.GotoAsync(u);
        await b.P.WaitForURLAsync(new Regex(@"/cpsess\d+/"), new() { Timeout = 120_000 });
        var p = b.P;
        var bas = Regex.Match(p.Url, @"^https://[^/]+/cpsess\d+/frontend/[^/]+/").Value;
        Log.W("cPanel: " + bas);
        return bas;
    }

    public static async Task<bool> Yukle(Br b, Cfg c, string crt, string key, string cab)
    {
        var bas = await Ac(b, c);
        bool hep = true;
        foreach (var d in c.Kur)
        {
            await b.P.GotoAsync(bas + "ssl/install.html");
            await b.P.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await b.Kesif("cp_install_" + d);

            var dom = await b.Bul("Alan Adı", "#ssldomain", "select[name=domain]", "select");
            var fc = await b.Bul("CRT", "#sslcrt", "textarea[name=crt]", "textarea >> nth=0");
            var fk = await b.Bul("KEY", "#sslkey", "textarea[name=key]", "textarea >> nth=1");
            var fb = await b.Bul("CABUNDLE", "#sslcab", "textarea[name=cabundle]", "textarea >> nth=2");
            if (dom == null || fc == null || fk == null) { hep = false; continue; }

            // Seçenek metinleri "app.example.com (+ subdomain: www)" gibi ekli; değer ya da metnin başı tam alan olmalı
            var ops = await dom.EvaluateAsync<string[][]>("s=>[...s.options].map(o=>[o.value,o.text.trim()])");
            Log.W("Alan seçenekleri:\n" + string.Join("\n", ops.Select(o => $"  [{o[0]}] {o[1]}")));
            var rx = new Regex($@"^{Regex.Escape(d)}(\s|\(|$)", RegexOptions.IgnoreCase);
            var op = ops.FirstOrDefault(o => o[0].Equals(d, StringComparison.OrdinalIgnoreCase)) ?? ops.FirstOrDefault(o => rx.IsMatch(o[1]));
            if (op == null) { Log.W($"✗ '{d}' seçeneği yok — ayar.json'daki Kur listesini yukarıdaki adlara göre düzelt"); hep = false; continue; }
            await dom.SelectOptionAsync(new SelectOptionValue { Value = op[0] });
            Log.W($"Alan seçildi: [{op[0]}] {op[1]}");
            await b.P.WaitForTimeoutAsync(2500); // cPanel seçimden sonra otomatik doldurabilir; üzerine yazıyoruz

            await Br.Isaret(fc); await fc.FillAsync(crt);
            await Br.Isaret(fk); await fk.FillAsync(key);
            if (fb != null) { await Br.Isaret(fb); await fb.FillAsync(cab); }
            Log.W("CRT/KEY/CABUNDLE dolduruldu");

            var btn = await b.Bul("Yükle düğmesi", "#btnInstall", "button:has-text('Sertifikayı Yükle')", "button:has-text('Install Certificate')", "button[type=submit]");
            if (btn == null) { hep = false; continue; }
            if (!await b.Isle(btn, "cp_yukle_" + d)) { if (b.M != Mod.Prova) hep = false; continue; }

            try { await b.P.Locator(".alert-success, [class*=success]").First.WaitForAsync(new() { Timeout = 60000 }); Log.W("✓ Kuruldu: " + d); }
            catch (PlaywrightException) { Log.W("? Başarı mesajı görülmedi: " + d); hep = false; }
            await b.Foto("cp_sonuc_" + d);
        }
        return hep;
    }
}
