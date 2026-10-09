# SslYon — ZeroSSL + win-acme + Natro yarı otomatik SSL

## Kurulum
1. .NET 8 SDK, Microsoft Edge ve win-acme (ör. `C:\win-acme\wacs.exe`) kurulu olmalı.
2. `dotnet build` → `bin\Debug\net8.0-windows\SslYon.exe`, ardından bir kez Chromium'u indir:
   `powershell -ExecutionPolicy Bypass -File bin\Debug\net8.0-windows\playwright.ps1 install chromium`
   (Edge yönetici yetkisiyle açılmayı reddettiği için varsayılan tarayıcı Chromium; `"Tarayici": "msedge"` yalnızca yönetici olmadan çalışır.)
3. İlk açılışta `%AppData%\SslYon\ayar.json` oluşur. **Ayarlar** düğmesiyle aç:
   - `Mail`, `EabKid`, `EabKeyDuz` (ZeroSSL'in verdiği gibi yapıştır; ilk açılışta DPAPI ile şifrelenip silinir)
   - `Wacs`, `Pem`, `Kur` (cPanel Alan Adı listesindeki adlar)

## Prova sırası
| Düğme | Ne yapar | Kalıcı değişiklik |
|---|---|---|
| 1 · Prova: Natro DNS | Giriş → Hosting → Yönet → mmuhasebe.net ▸ Web Sitesi ▸ DNS Yönetimi → Yeni Kayıt (TXT) → iki sahte token doldurur, Kaydet'i kırmızı çerçeveler | Yok |
| 2 · Prova: cPanel | cPanel'e Bağlan → ssl/install.html → alan seç, sahte CRT/KEY/CABUNDLE doldur, Yükle'yi çerçeveler | Yok |
| 3 · Prova: uçtan uca | win-acme `--test` (Let's Encrypt staging). TXT **gerçekten kaydedilir** (her tıklamada sorar), cPanel yalnızca doldurulur | Natro'da TXT kaydı |
| 4 · Sertifika al | ZeroSSL'den gerçek sertifika | TXT + sertifika |
| 5 · cPanel'e yükle | Son PEM'leri kurar (Yükle'ye basmadan sorar) | Kurulum |

Her adımda `%AppData%\SslYon\log\` altına log ve ekran görüntüsü düşer. `KEŞİF[...]` satırları sayfadaki görünür alanları (etiket, id, name) listeler.

## İnce ayar
Natro TXT formu keşiften kesinleşti: `input#Name`, `textarea#Value`, `Değiştir` düğmesi (varsayılan). Natro değiştirirse keşif çıktısına göre `ayar.json`a yaz:
```json
"NatroTxtAd": "#txtHost", "NatroTxtDeger": "#txtValue", "NatroTxtKaydet": "#btnTxtKaydet"
```
Natro ad alanına tam ad (`_acme-challenge.mmuhasebe.net`) istiyorsa `"TxtAdBicim": "tam"` yap.

## Kalıcı kullanım
- Release derle, çıktıyı `C:\SslYon\` gibi sabit bir klasöre kopyala, oradan yönetici olarak çalıştır (win-acme script yolunu yenileme kaydına yazar).
- **Oturumda başlat** kutusu: Görev Zamanlayıcı'ya `SslYon` görevi ekler (oturum açılışı, en yüksek ayrıcalık, `--tray` ile gizli). Exe taşınırsa kutuyu kapatıp aç.
- **TXT otomatik** kutusu: TXT ekle/sil onaysız olur; cPanel'e yükleme her zaman onaylıdır.
- Yenileme: bildirim gelince **4 · Sertifika al** → `--renew --force` (kayıtlı ayarlarla), ardından cPanel yükleme onayları.

## Notlar
- win-acme bu exe'yi `--dnsscript` olarak çağırır (`create|delete {Identifier} {RecordName} {Token}`); mod `SSLYON_MOD` ortam değişkeniyle geçer.
- `settings.json`'da `Script.Timeout` 3600'e çekilir (varsayılan 600 sn senin TXT girmene yetmeyebilir).
- Zamanlanmış görev kurulmaz (`--notaskscheduler`); uygulama 6 saatte bir bitiş tarihini kontrol edip 30 gün kala bildirim verir.
