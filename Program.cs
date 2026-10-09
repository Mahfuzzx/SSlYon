namespace SslYon;

static class Program
{
    [STAThread]
    static int Main(string[] a)
    {
        ApplicationConfiguration.Initialize();
        // win-acme script modu: create|delete {Identifier} {RecordName} {Token}
        if (a.Length >= 4 && a[0] is "create" or "delete")
        {
            Log.On_ek = "script-";
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.W("SCRIPT ÇÖKTÜ: " + e.ExceptionObject);
            return Script(a).GetAwaiter().GetResult();
        }
        using var mx = new Mutex(true, "SslYon_tek", out var ilk); // tek kopya
        if (!ilk) { MessageBox.Show("SslYon zaten çalışıyor (tray simgesine bak).", "SslYon"); return 0; }
        Application.Run(new Fm(Cfg.Yukle(), a.Contains("--tray")));
        return 0;
    }

    static async Task<int> Script(string[] a)
    {
        try
        {
            var c = Cfg.Yukle();
            var m = Enum.TryParse<Mod>(Environment.GetEnvironmentVariable("SSLYON_MOD"), out var x) ? x : Mod.Onay;
            Log.W($"SCRIPT {a[0]} id={a[1]} kayıt={a[2]} token={a[3]} mod={m} kullanıcı={Environment.UserName} dizin={Environment.CurrentDirectory}");
            if (a[0] == "delete")
            {
                if (m != Mod.Prova) { await using var d = await Br.Ac(m); await Natro.TxtSil(d, c, a[3]); }
                return 0; // silme başarısızsa da doğrulama bitti; sorun değil
            }
            if (m == Mod.Prova) { Log.W("Prova modunda gerçek DNS doğrulaması yapılamaz"); return 1; }
            bool ok;
            await using (var b = await Br.Ac(m)) ok = await Natro.TxtEkle(b, c, a[2], a[3]);
            return ok && await Ag.TxtBekle(a[2], a[3], c.Alan) ? 0 : 1;
        }
        catch (Exception e) { Log.W("SCRIPT HATA: " + e); return 1; }
    }
}
