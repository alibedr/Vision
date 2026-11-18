# Vision 1.8 — Sürüm notları

**Yayın tarihi:** 18.11.2026

Bu klasörde Vision'ın yalnız **güncel sürümü** bulunur. Eski sürümler kullanılmamalıdır: her sürüm, öncekilerin hatalarını
düzeltip iyileştirmelerini içerir.

## Kurulum

1. `Vision` klasörünü bütün olarak istediğiniz yere kopyalayın.
2. `Vision.exe`'yi çalıştırın. Kurulum gerekmez.
3. İlk açılışta uygulama turu Vision'ı adım adım tanıtır (sonra **Yardım → Uygulama turu** ile yeniden açılır).

`lib` klasörü `Vision.exe`'nin yanında kalmalıdır (Sentinel sayfası için Edge WebView2 dosyaları).

**Gereksinimler:** Windows 10/11, .NET Framework 4.6.1 veya üstü. Sentinel sayfası için Microsoft Edge WebView2 Runtime
(Windows 10/11'de genellikle yüklüdür; yoksa sayfa eski görüntüleyiciyle açılır).

Önceki bir sürümü kullanıyorsanız yeni `Vision` klasörünü eskisinin yerine koymanız yeterlidir; ayarlar, kartlar, komut
geçmişi ve boot ayarı kayıtları `%APPDATA%\TelnetCards` altında tutulduğu için korunur. Hata kayıtları ve kartların olay
günlükleri ise `Vision.exe`'nin yanındaki `logs` klasöründedir; bunları saklamak isterseniz eski `logs` klasörünü yeni
`Vision` klasörüne taşıyın.

## Bu sürümde yeni

- **Seri sekmesi: kartlara COM portundan bağlantı.** TeraTerm açmadan, USB-seri çeviriciyle kartlara seri porttan
  bağlanılır. Telnet'ten bağımsızdır; kartın ağı yokken, açılışta ya da bootrom'dayken de çalışır.
  - Bilgisayardaki portlar Windows'taki adlarıyla listelenir; porta kartın adı verilebilir (ör. "Kart 3").
  - En fazla 12 port aynı anda, Telnet'teki gibi ızgarada; her pencere büyütülebilir.
  - Port başına ayarlar: baud rate, data, parity, stop, akış denetimi ve gönderme gecikmesi (varsayılan 115200 8N1).
  - TeraTerm gibi terminale tıklayıp doğrudan yazılır; seçilen metin kendiliğinden kopyalanır, Shift+Insert yapıştırır.
  - Seride yazılan komutlar Telnet'ten ayrı bir geçmişte saklanır (tek tık yazar, çift tık gönderir); çıktı dosyaya
    kaydedilebilir.
- **Sekmeye göre değişen sol menü.** Telnet ve Kart Dosya Gezgini'nde Kart Gezgini, Seri'de seri portlar, Sentinel'de kart
  listesi, Komut Kütüphanesi'nde arama ve komut listesi. Genişlik bütün sekmelerde aynı olduğu için sekme değiştirince ekran
  kaymaz; her sekmenin menüsü ayrı gizlenir ve açık / kapalı durumu ayrı hatırlanır.
- **Klasör yükleme daha dayanıklı.** Bir dosya FTP'ye gönderilemez ya da kartta kopyalanamazsa yükleme durmaz: o dosya
  atlanır, kalanlar yüklenir. Sonunda kartta her dosyanın boyutu bilgisayardakiyle karşılaştırılır. Yüklenemeyenler sonda
  listelenir: ekranda ilk 5'i, kartın olay günlüğünde hepsi nedenleriyle.
- **Yeni sekme sırası ve kısayollar:** Telnet (Ctrl+1), Kart Dosya Gezgini (Ctrl+2), Seri (Ctrl+3), Sentinel (Ctrl+4),
  Komut Kütüphanesi (Ctrl+5).
- Uygulama turu ve Kullanım Kılavuzu yeni özelliklere göre güncellendi.

## Önemli notlar

- Seri port aynı anda yalnız bir programda açılabilir: port TeraTerm'de açıksa Vision açamaz ve bunu söyler.
- Komut Kütüphanesi'nin kısayolu Ctrl+3'ten Ctrl+5'e geçti (Ctrl+3 artık Seri sekmesi).
- Boot ayarı kayıtları, FTP parolası dahil kartın tüm boot alanlarını `config.xml` içinde düz metin olarak saklar.
- Sorun bildirirken `logs\hatalar.log` dosyasını eklemeniz sorunun bulunmasını kolaylaştırır. Kartla ilgili olayların
  zaman çizelgesi için terminalde sağ tık → **Olay günlüğü…**

## Önceki sürümlerde gelenler

| Sürüm | Değişiklikler |
|---|---|
| 1.7 | Kart açılınca loglar reboot'u atana gelir: reboot'u gönderen Vision 2 sn'de bir, habersiz kopan diğerleri 30 sn bekleyip 15 sn'de bir dener. Düzeltmeler: Bağlan / Kes art arda basılınca arka planda bağlantı açık kalması; Boot ayarı kayıtları penceresi genişletilince satırların kayması. |
| 1.6 | Boot ayarı kayıtları: kartın boot ayarlarını adıyla kaydetme (kart başına en fazla 5), şu anki ayarlarla karşılaştırma, önizleme + onayla aynı ya da başka karta geri yükleme (başka kartta IP alanları atlanır), doğrulama ve yeniden başlatma önerisi; Boot ayarları penceresinden "Bu ayarları kaydet…" ve "Boot ayarı kayıtları…". Kendi gönderdiğimiz reboot'ta kırmızı uyarı yerine sarı "yeniden başlatılıyor"; kesinti ekrana ve olay günlüğüne yalnız bir kez, bağlantı gelince toplam süreyle yazılır. |
| 1.5 | Otomatik testler (her derlemede çalışır); kart başına olay günlüğü (bağlantı, reboot, boot ayarı değişiklikleri, Sentinel, dosya işlemleri, aktarımlar — süzme, arama, CSV); reboot onayında "Temizleyip yeniden başlat". |
| 1.4 | Yüksek kontrast temasından başka temaya geçişteki renk sorunu düzeltildi; reboot onay ister; kartta ve genel komut çubuğunda en fazla 5 hızlı komut; Dosya Gezgini'nde çoklu seçim (Ctrl+A, Ctrl / Shift + tık) ile toplu silme, kaydetme, taşıma / kopyalama ve sürükleme. |
| 1.3 | Klasör içindeki büyük dosyalar da parça parça yüklenir; kart içi kopyalama / taşımada ilerleme ve iptal; disk yeri denetimleri; aktarım güvenliği (aktarım sürerken komut engeli, boyut doğrulaması, bağlantı kopması ve kapanışta kartta kalanların temizlenmesi); beklenmeyen hatalarda Türkçe bildirim ve log. |
| 1.2 | Karttan büyük dosya alma (parçalı), anında iptal, anlık hıza göre kalan süre, indirmede ilerleme penceresi. |
| 1.1 | Büyük dosya yükleme: büyük dosyalar parça parça aktarılır, kopyalama süresi dosya boyutuna göre, yüklemeden önce boş alan denetimi, FAT 4 GB sınırı, yükleme ilerleme penceresi. |
| 1.0 | İlk resmî sürüm: çoklu kart telnet terminali, grup tabanlı kart yönetimi, genel komut çubuğu, bağlantı izleme ve otomatik yeniden bağlanma, Kart Dosya Gezgini, Komut Kütüphanesi, Boot ayarları penceresi, Sentinel sayfası, temalar, uygulama turu. |
