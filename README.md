# SslYon

Natro paylaşımlı hosting (cPanel) üzerinde **ZeroSSL** wildcard sertifikasını **win-acme** ile yarı otomatik alıp yenileyen Windows aracı.

Natro'da cPanel API/SSH ve AutoSSL kapalı olduğu için tam otomasyon mümkün değil. SslYon kalan elle işleri tarayıcı otomasyonuyla (Playwright) üstlenir:

1. win-acme DNS-01 doğrulaması ister → SslYon Natro DNS Yönetimi'ne `_acme-challenge` TXT kaydını ekler, DNS'te yayılmasını bekler, doğrulamadan sonra siler.
2. Sertifika PEM olarak yazılır → SslYon cPanel **SSL/TLS → SSL sitelerini yönetin** formunu her site için doldurur ve onayınla yükler.
3. Tray'de çalışır, bitiş tarihini izler, süre azalınca bildirim verir.

> ⚠️ **Sorumluluk reddi:** Natro veya cPanel ile bağlantısı yoktur. Araç, Natro müşteri panelini kendi hesabınızda tarayıcı otomasyonuyla kullanır. Panel arayüzü değişirse seçiciler bozulabilir. Kullanmadan önce sağlayıcınızın kullanım koşullarını kontrol edin. Tüm risk kullanıcıya aittir (bkz. [LICENSE](LICENSE)).

## Gereksinimler
- Windows 10/11, .NET 8 SDK
- [win-acme](https://www.win-acme.com/) (ör. `C:\win-acme\wacs.exe`)
- ZeroSSL hesabı → Developer → **EAB Credentials** (ücretsiz ACME, wildcard dahil)
- Natro hosting, alan adının DNS'i Natro'da

## Kurulum
```
dotnet publish -c Release -o C:\SslYon
```
1. `C:\SslYon\SslYon.exe`'yi **yönetici olarak** çalıştırın. İlk tarayıcı açılışında Playwright Chromium kendiliğinden indirilir (~150 MB).
   - Uygulamayı sabit bir klasörden çalıştırın: win-acme script yolunu yenileme kaydına yazar.
2. **Ayarlar** → `%AppData%\SslYon\ayar.json`:

| Alan | Açıklama |
|---|---|
| `Siteler[]` | Alan adı başına bir kayıt (aşağıdaki beş alan). Pencerenin solundaki listeden seçilen site için 1–5 düğmeleri çalışır |
| ↳ `Alan` | Ana alan adı (`example.com`) |
| ↳ `Host` | Sertifikadaki adlar (`example.com`, `*.example.com`) |
| ↳ `Kur` | cPanel'de sertifikanın kurulacağı siteler (Alan Adı listesindeki adlar; ör. ek alt alan siteleri) |
| ↳ `HostingNo` | Natro *Web Hosting Hesapları* listesindeki hesap kodu |
| ↳ `Ad` | PEM dosya öneki ve win-acme kayıt id'si; **her sitede farklı olmalı** |
| `Mail`, `EabKid`, `EabKeyDuz` | ZeroSSL ACME bilgileri (tüm siteler için ortak). `EabKeyDuz` ilk açılışta DPAPI ile şifrelenip silinir |
| `Wacs`, `Pem` | wacs.exe yolu, PEM klasörü |

Eski tek siteli `ayar.json` (üst düzeyde `Alan`, `Host`…) ilk açılışta kendiliğinden `Siteler[0]`'a taşınır. DNS doğrulamasında site, win-acme'nin gönderdiği kayıt adından (`_acme-challenge.<alan>`) seçilir; bu yüzden hangi site seçili olursa olsun her sitenin yenilemesi doğru alana yazılır.
| `TxtOto` | `true`: TXT kayıtları onaysız eklenir/silinir |
| `Esik` | Bitişe kaç gün kala uyarı verilsin (varsayılan 30) |

3. Tarayıcı açılınca Natro'ya bir kez giriş yapın; oturum profilde saklanır (şifre uygulamada tutulmaz).

## Kullanım
| Düğme | Ne yapar | Kalıcı değişiklik |
|---|---|---|
| 1 · Prova: Natro DNS | TXT formunu iki sahte token ile doldurur, kaydetmez | Yok |
| 2 · Prova: cPanel | Sahte sertifikayla yükleme formunu doldurur, yüklemez | Yok |
| 3 · Prova: uçtan uca | Let's Encrypt **staging** ile tam akış; cPanel yalnızca doldurulur | Geçici TXT kayıtları |
| 4 · Sertifika al (ZeroSSL) | Gerçek sertifika; ilk sefer yeni kayıt, sonrakiler `--renew --force` | TXT + sertifika |
| 5 · cPanel'e yükle | Son PEM'leri her siteye kurar (onaylı) | Kurulum |
| Kontrol | Sitelerin canlı sertifika bitiş tarihini gösterir | Yok |

- **Oturumda başlat:** Görev Zamanlayıcı'ya oturum açılışında, en yüksek ayrıcalıkla, tray'de gizli başlayan bir görev ekler.
- Kalıcı her tıklama (Değiştir, Sertifika Yükle, Sil) tek bir kapıdan geçer: prova modunda tıklanmaz, kırmızı çerçeve + ekran görüntüsü alınır.
- Loglar ve ekran görüntüleri: `%AppData%\SslYon\log\` (win-acme'nin çağırdığı script modu `script-*.txt`'ye yazar).

## Nasıl çalışır
- win-acme `--validation script --dnsscript SslYon.exe` ile çağırır; SslYon `create|delete {Identifier} {RecordName} {Token}` argümanlarıyla script modunda açılır.
- DNS sayfası Natro'nun `HostingPanelineGit_v2.asp?...zoneName=<alan>` bağlantısından açılır; açılan sayfa başka alana aitse işlem durur (güvenlik kilidi).
- TXT yayılması Google, Cloudflare ve alanın yetkili NS'lerinden kontrol edilir.
- `settings.json`'da `Script.Timeout` 3600 sn'ye çekilir (varsayılan 600 sn elle onaya yetmeyebilir).
- Edge yönetici yetkisiyle açılmadığından varsayılan tarayıcı Playwright Chromium'dur (`"Tarayici": "msedge"` yalnızca yetkisiz çalıştırmada).

## İnce ayar
Natro formu değişirse ilgili adımın `KEŞİF[...]` log çıktısındaki id/name'e göre `ayar.json`'a seçici yazın:
```json
"NatroTxtAd": "#Name", "NatroTxtDeger": "#Value", "NatroTxtKaydet": "button:has-text('Değiştir')"
```
Ad alanı tam ad istiyorsa (`_acme-challenge.example.com`) `"TxtAdBicim": "tam"`.

## Notlar
- Dizüstünde Görev Zamanlayıcı görevi pildeyken başlamayabilir: görev → Koşullar → "Yalnızca AC güç kaynağındaysa başlat" işaretini kaldırın.
- Eski staging kaydını silmek için: `wacs --cancel --id <Ad>-test --baseuri https://acme-staging-v02.api.letsencrypt.org/`

## Lisans
[MIT](LICENSE). Bağımlılıklar: Microsoft.Playwright (Apache-2.0), DnsClient (Apache-2.0), System.Management ve System.Security.Cryptography.ProtectedData (MIT). win-acme (Apache-2.0) ayrıca indirilir, repoya dahil değildir.
