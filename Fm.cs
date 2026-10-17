using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
namespace SslYon;

public class Fm : Form
{
    readonly Cfg c;
    readonly TextBox t = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new("Consolas", 9) };
    readonly FlowLayoutPanel p = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new(4) };
    readonly NotifyIcon ni = new() { Icon = SystemIcons.Shield, Text = "SslYon", Visible = true };
    readonly System.Windows.Forms.Timer tm = new() { Interval = 6 * 3600 * 1000 };

    public Fm(Cfg c, bool gizli = false)
    {
        this.c = c; Text = "SslYon — " + c.Alan; Width = 1000; Height = 650; Icon = SystemIcons.Shield;
        if (gizli) { WindowState = FormWindowState.Minimized; ShowInTaskbar = false; }
        Controls.Add(t); Controls.Add(p);
        Log.On += s => { if (IsHandleCreated) BeginInvoke(() => t.AppendText(s.Replace("\n", "\r\n") + "\r\n")); };

        // Site seçici: 1–5 düğmeleri seçili site için çalışır; açılınca ayar.json'dan tazelenir (Not Defteri'nde eklenen site görünsün)
        var sc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Margin = new(3, 4, 8, 3) };
        void Doldur() { sc.Items.Clear(); foreach (var s in c.Siteler) sc.Items.Add(s.Alan); sc.SelectedItem = c.Alan; }
        sc.DropDown += (_, _) => { c.Siteler = Cfg.Yukle().Siteler; c.Sec(c.Alan); Doldur(); };
        sc.SelectedIndexChanged += (_, _) =>
        {
            if (sc.SelectedItem is not string a || a == c.Alan) return;
            c.Sec(a); c.Secili = a; c.Kaydet(); Text = "SslYon — " + c.Alan; Log.W($"Site: {c.Alan} (Ad={c.Ad}, Kur={string.Join(", ", c.Kur)})");
        };
        Doldur(); p.Controls.Add(sc);

        B("1 · Prova: Natro DNS", ProvaDns);
        B("2 · Prova: cPanel", () => CpYukle(Ag.Sahte(c.Host), Mod.Prova));
        B("3 · Prova: uçtan uca (LE staging)", () => Al(true));
        B("4 · Sertifika al (ZeroSSL)", () => Al(false));
        B("5 · cPanel'e yükle", async () => { var x = Wacs.Pem(c, false); if (x != null) await CpYukle(x.Value, Mod.Onay); });
        B("Kontrol", Kontrol);
        var to = new CheckBox { Text = "TXT otomatik (onaysız)", AutoSize = true, Checked = c.TxtOto, Margin = new(8, 6, 3, 3) };
        to.CheckedChanged += (_, _) => { c.TxtOto = to.Checked; c.Kaydet(); Log.W("TXT otomatik: " + (to.Checked ? "açık" : "kapalı")); };
        p.Controls.Add(to);
        var os = new CheckBox { Text = "Oturumda başlat", AutoSize = true, Checked = Gorev.Var(), Margin = new(8, 6, 3, 3) };
        bool dur = false; // geri alırken olayı tekrar tetiklememek için
        os.CheckedChanged += (_, _) => { if (dur) return; if (!Gorev.Ayarla(os.Checked)) { dur = true; os.Checked = Gorev.Var(); dur = false; } };
        p.Controls.Add(os);
        B("Ayarlar", () => { Process.Start("notepad", $"\"{Cfg.F}\""); return Task.CompletedTask; });
        B("Loglar", () => { Directory.CreateDirectory(Log.Dir); Process.Start("explorer", Log.Dir); return Task.CompletedTask; });

        void Goster() { ShowInTaskbar = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        ni.DoubleClick += (_, _) => Goster();
        ni.BalloonTipClicked += (_, _) => Goster();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosed += (_, _) => ni.Dispose();
        tm.Tick += async (_, _) => await Kontrol(); tm.Start();
        Shown += async (_, _) => { if (gizli) Hide(); Log.W("Ayar: " + Cfg.F); await Kontrol(); };
    }

    void B(string s, Func<Task> f)
    {
        var b = new Button { Text = s, AutoSize = true };
        b.Click += async (_, _) =>
        {
            p.Enabled = false;
            try { await f(); } catch (Exception e) { Log.W("HATA: " + e.Message); }
            finally { p.Enabled = true; Log.W("—— bitti ——"); }
        };
        p.Controls.Add(b);
    }

    // Kök + wildcard için iki token; ikinci kaydın birinciyi ezmediğini görmek için ikisi de doldurulur
    async Task ProvaDns()
    {
        await using var b = await Br.Ac(Mod.Prova);
        await Tut(b, async () => { for (int i = 1; i <= 2; i++) { Log.W($"— DNS prova {i}/2 —"); await Natro.TxtEkle(b, c, "_acme-challenge." + c.Alan, "PROVA" + i + "-" + Ag.Token()); } });
    }

    async Task CpYukle((string crt, string key, string cab) x, Mod m)
    {
        await using var b = await Br.Ac(m);
        await Tut(b, async () => Log.W(await Cp.Yukle(b, c, x.crt, x.key, x.cab) ? "✓ cPanel adımı tamam" : "✗ cPanel adımında sorun var — logu incele"));
    }

    // Hata olursa tarayıcıyı incelemek için açık tut
    async Task Tut(Br b, Func<Task> f)
    {
        try { await f(); }
        catch (Exception e)
        {
            Log.W("HATA: " + e.Message); await b.Foto("hata");
            MessageBox.Show(this, "Hata: " + e.Message + "\n\nTarayıcı inceleme için açık. Kapatmak için Tamam.", "SslYon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    async Task Al(bool test)
    {
        if (!test && (c.Mail == "" || c.EabKid == "" || c.EabKeyEnc == "")) { Log.W("Ayarlarda Mail/EabKid/EabKeyDuz eksik"); return; }
        if (!test)
        {   // win-acme script yolunu yenileme kaydına yazar → exe kalıcı bir yerde olmalı
            var x0 = Environment.ProcessPath ?? "";
            if ((x0.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) || x0.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase)) &&
                MessageBox.Show(this, $"Uygulama geçici bir klasörden çalışıyor:\n{x0}\n\nwin-acme bu yolu yenileme kaydına yazar; exe taşınırsa yenileme bozulur. Önce C:\\SslYon gibi sabit bir yere kopyalayıp oradan çalıştırman önerilir.\n\nYine de devam edilsin mi?",
                    "SslYon", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            if (MessageBox.Show(this, $"{c.Alan} için ZeroSSL'den gerçek sertifika alınacak ({string.Join(", ", c.Host)}). Devam?", "SslYon", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        }
        Wacs.Ayarla(c);
        if (await Wacs.Calistir(c, test, c.TxtOto ? Mod.Oto : Mod.Onay) != 0) { Log.W("✗ wacs başarısız"); return; }
        var x = Wacs.Pem(c, test); if (x == null) return;
        Log.W("✓ Sertifika alındı → " + Wacs.PemDir(c, test));
        if (test) await CpYukle(x.Value, Mod.Prova); // staging sertifikası güvenilir değil: yalnızca doldur
        else if (MessageBox.Show(this, "cPanel'e şimdi yüklensin mi?", "SslYon", MessageBoxButtons.YesNo) == DialogResult.Yes) await CpYukle(x.Value, Mod.Onay);
    }

    async Task Kontrol()
    {
        foreach (var h in c.Siteler.SelectMany(s => s.Kur).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await Ag.Sertifika(h) is not var (x, e)) continue;
            var g = (int)(x.NotAfter - DateTime.Now).TotalDays;
            if (e != System.Net.Security.SslPolicyErrors.None)
            {   // tarih anlamsız: tarayıcı bu sertifikayı reddeder
                var n = e.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) ? $"ad uyuşmuyor (sertifika: {x.GetNameInfo(X509NameType.DnsName, false)})" : e.ToString();
                Log.W($"✗ {h}: geçerli sertifika yok — {n}");
                ni.ShowBalloonTip(10000, "SSL kurulu değil", $"{h}: {n}", ToolTipIcon.Error);
                continue;
            }
            Log.W($"{h}: veren={x.GetNameInfo(X509NameType.SimpleName, true)}, bitiş={x.NotAfter:dd.MM.yyyy} ({g} gün)");
            if (g <= c.Esik) ni.ShowBalloonTip(10000, "SSL yenileme zamanı", $"{h}: {g} gün kaldı", ToolTipIcon.Warning);
        }
    }
}
