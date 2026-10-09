using System.Diagnostics;
namespace SslYon;

// Görev Zamanlayıcı: oturum açılınca, en yüksek ayrıcalıkla, tray'de gizli başlat
public static class Gorev
{
    const string Ad = "SslYon";

    static (int k, string o) S(string a)
    {
        var p = Process.Start(new ProcessStartInfo("schtasks.exe", a) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })!;
        var o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit();
        return (p.ExitCode, o.Trim());
    }

    public static bool Var() => S($"/Query /TN \"{Ad}\"").k == 0;

    public static bool Ayarla(bool ac)
    {
        var u = $"{Environment.UserDomainName}\\{Environment.UserName}";
        var (k, o) = ac
            ? S($"/Create /F /TN \"{Ad}\" /TR \"\\\"{Environment.ProcessPath}\\\" --tray\" /SC ONLOGON /RU \"{u}\" /IT /RL HIGHEST /DELAY 0000:30")
            : S($"/Delete /F /TN \"{Ad}\"");
        Log.W(k == 0 ? $"✓ Oturumda başlatma {(ac ? "açıldı" : "kapatıldı")}" : $"✗ Görev Zamanlayıcı: {o} (yönetici olarak çalıştırılmalı)");
        if (ac && k == 0) Log.W("Görev yolu: " + Environment.ProcessPath + " — exe'yi taşırsan kutuyu kapatıp yeniden aç");
        return k == 0;
    }
}
