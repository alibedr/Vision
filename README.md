<img src="docs/simge.png" width="96" align="right" alt="Vision simgesi">

# Vision

Donanım kartlarına telnet ile bağlanmak, birden çok kartın çıktısını aynı anda izlemek, kartlara toplu komut göndermek ve kartların dosya sistemini gezmek için Windows masaüstü uygulaması.

Geliştiren: **Ali Bedir**

> Önceki adı: Telnet Kart Terminali. Önceki sürümün ayarları yeni adla da okunur.

## Özellikler

- Grup tabanlı kart yönetimi (Kart Gezgini), kartlara isteğe bağlı ad
- Aynı anda en fazla 24 kart terminali; bağlantı ve görünüm birbirinden bağımsız
- Bağlantı süresi, kaydırma koruması ve yeni satır göstergesi, kalıcı komut geçmişi (geçmiş penceresi, Ctrl+R ile geriye doğru arama) ve otomatik tamamlama, kart ekranında sağ tıkla renklendirme
- Genel komut çubuğu: ekrandaki tüm kartlara aynı komut
- Periyodik bağlantı izleme ve otomatik yeniden bağlanma
- Kart Dosya Gezgini: `devs` / `ll`, klasör ve dosya işlemleri, FTP üzerinden karta yükleme, salt okunur "Birlikte aç"
- Komut Kütüphanesi: exe'ye gömülü, salt okunur 113 VxWorks 6.4 kabuk komutu (açıklama, örnek kullanım, örnek çıktı), modüllerde gruplama ve komutu tek tıkla bir kartın komut satırına aktarma
- Koyu ve açık tema (sekme şeridinin sağındaki düğme ya da Ctrl+Shift+L; seçim hatırlanır)
- Türkçe arayüz ve hata mesajları, uygulama içi yardım (F1)

## Gereksinimler

| | |
|---|---|
| Çalıştırma | Windows 10 / 11, .NET Framework 4.6.1 veya üstü |
| Geliştirme | Visual Studio 2019 (16.11), C# 7.3; NuGet paketi gerekmez |

## Derleme

1. `Vision.sln` dosyasını Visual Studio 2019 ile açın.
2. **Build → Build Solution** (Ctrl+Shift+B).
3. Çıktı `bin\Debug\` (ya da `bin\Release\`) klasöründedir. `Yardim\` klasörü exe'nin yanına kopyalanır; komut kütüphanesi exe'ye gömülüdür.

## Klasör yapısı

| Konum | İçerik |
|---|---|
| `*.cs` | Kaynak kod (WinForms) |
| `Yardim\` | Kullanım Kılavuzu ve Gereksinimler (uygulama içi yardımda gösterilir) |
| `Komutlar\Komutlar.xml` | Komut Kütüphanesi içeriği (VxWorks 6.4 komutları); derlemede exe'ye gömülür, dışarıya kopyalanmaz |
| `Vision.ico` | Uygulama simgesi (`docs/simge.png`: büyük önizleme) |

Gruplar, kartlar ve ayarlar kullanıcının bilgisayarında `%APPDATA%\TelnetCards\config.xml` dosyasında tutulur (yeni sürüme geçişte korunur). Çalışırken exe'nin yanında `logs\` oluşur; depoya alınmaz.

## Ayrıntılı belgeler

- [Kullanım Kılavuzu](Yardim/KullanimKilavuzu.md)
- [Gereksinimler ve sürüm notları](Yardim/Gereksinimler.md)
