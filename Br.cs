using Microsoft.Playwright;
namespace SslYon;

// Tarayıcı: kalıcı Edge profili (Natro/cPanel oturumu saklanır, şifre uygulamada tutulmaz)
public sealed class Br : IAsyncDisposable
{
    IPlaywright pw = null!;
    public IBrowserContext C = null!;
    public IPage P = null!;
    public Mod M;

    // Edge yönetici yetkisiyle açılınca kendini yetkisiz yeniden başlatıp çıkıyor → varsayılan Playwright Chromium
    static readonly bool Edge = Cfg.Yukle().Tarayici == "msedge";
    static readonly string Prf = Path.Combine(Cfg.Dir, Edge ? "profil" : "profil-cr");

    // Profilimizi tutan artık tarayıcı süreçlerini kapat (yalnızca --user-data-dir=…\SslYon\profil… olanlar)
    static void Temizle()
    {
        try
        {
            using var s = new System.Management.ManagementObjectSearcher("SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='msedge.exe' OR Name='chrome.exe'");
            foreach (System.Management.ManagementObject o in s.Get())
                if (o["CommandLine"] is string cl && cl.Contains(Prf, StringComparison.OrdinalIgnoreCase))
                    try { System.Diagnostics.Process.GetProcessById(Convert.ToInt32(o["ProcessId"])).Kill(true); Log.W("Artık Edge kapatıldı: pid " + o["ProcessId"]); } catch { }
        }
        catch (Exception e) { Log.W("Edge temizliği yapılamadı: " + e.Message); }
    }

    public static async Task<Br> Ac(Mod m)
    {
        var b = new Br { M = m, pw = await Playwright.CreateAsync() };
        var op = new BrowserTypeLaunchPersistentContextOptions
        { Headless = false, Channel = Edge ? "msedge" : null, ViewportSize = ViewportSize.NoViewport, SlowMo = m == Mod.Prova ? 150 : 0 };
        Temizle();
        try { b.C = await b.pw.Chromium.LaunchPersistentContextAsync(Prf, op); }
        catch (PlaywrightException e)
        {
            Log.W("Tarayıcı açılamadı, tekrar deneniyor: " + e.Message.Split('\n')[0]);
            if (e.Message.Contains("Executable doesn't exist"))
            {   // ilk kullanım: Playwright Chromium'u indir (~150 MB, bir kez)
                Log.W("Chromium indiriliyor (bir kerelik, birkaç dakika sürebilir)…");
                var r = await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", Edge ? "msedge" : "chromium" }));
                Log.W(r == 0 ? "✓ Chromium indirildi" : $"✗ İndirme başarısız (kod {r})");
            }
            await Task.Delay(2000); Temizle(); await Task.Delay(1000);
            b.C = await b.pw.Chromium.LaunchPersistentContextAsync(Prf, op);
        }
        b.P = b.C.Pages.Count > 0 ? b.C.Pages[0] : await b.C.NewPageAsync();
        b.P.SetDefaultTimeout(20000);
        Log.W($"Tarayıcı açıldı (mod={m})");
        return b;
    }
    public async ValueTask DisposeAsync() { try { await C.CloseAsync(); } catch { } pw.Dispose(); }

    public async Task Foto(string ad)
    {
        var d = Path.Combine(Log.Dir, "foto"); Directory.CreateDirectory(d);
        var f = Path.Combine(d, $"{DateTime.Now:yyyyMMdd_HHmmss}_{ad}.png");
        try { await P.ScreenshotAsync(new() { Path = f, FullPage = true }); Log.W("📷 " + f); } catch (Exception e) { Log.W("Foto alınamadı: " + e.Message); }
    }

    public static Task Isaret(ILocator l) =>
        l.EvaluateAsync("e=>{e.scrollIntoView({block:'center'});e.style.outline='4px solid #e00';e.style.outlineOffset='2px'}");

    // Seçicileri sırayla dener, ilk görüneni döner; hangisinin tuttuğunu loglar
    public async Task<ILocator?> Bul(string ne, params string?[] ss)
    {
        foreach (var s in ss)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var l = P.Locator(s).First;
            try { await l.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 }); Log.W($"✓ {ne}: {s}"); return l; }
            catch (PlaywrightException) { }
        }
        Log.W($"✗ {ne} bulunamadı ({string.Join(" | ", ss)})");
        return null;
    }

    // Kalıcı eylem kapısı: Prova → işaretle+foto, tıklama | Onay → sor | Oto → tıkla
    public async Task<bool> Isle(ILocator l, string ne)
    {
        await Isaret(l); await Foto(ne);
        if (M == Mod.Prova) { Log.W($"PROVA: '{ne}' tıklanmadı"); return false; }
        if (M == Mod.Onay)
        {
            if (MessageBox.Show($"'{ne}' düğmesine basılsın mı?\n(Tarayıcıda kırmızı çerçeveli düğme)", "SslYon",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.DefaultDesktopOnly) != DialogResult.Yes)
            { Log.W($"'{ne}' iptal edildi"); return false; }
        }
        await l.ClickAsync(); Log.W($"'{ne}' tıklandı");
        return true;
    }

    const string JS = "()=>[...document.querySelectorAll('input,textarea,select,button,a.btn,[role=button],[onclick]')]" +
        ".filter(e=>e.offsetParent).map(e=>[e.tagName,e.type||'','#'+e.id,'n='+(e.name||''),'cls='+(e.className||'').toString().slice(0,40)," +
        "'ph='+(e.placeholder||''),(e.innerText||e.value||'').trim().replace(/\\s+/g,' ').slice(0,50)].join(' | ')).join('\\n')";

    // Keşif: tüm çerçevelerdeki görünür form elemanlarını loga döker + ekran görüntüsü (ince ayar için)
    public async Task Kesif(string ad)
    {
        foreach (var f in P.Frames)
            try { Log.W($"KEŞİF[{ad}] {f.Url}\n{await f.EvaluateAsync<string>(JS)}"); } catch { }
        await Foto(ad);
    }
}
