# Vision 1.10.1 — Sürüm notları

**Yayın tarihi:** 20.11.2026

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

1.10'un hata düzeltmesi; yeni özellik yok. Kart Dosya Gezgini'nde:

- **Aynı dosya / klasör aynı anda birden çok karta atılabiliyor.** Önceden aynı adlı dosya ya da klasör iki-üç karta aynı
  anda yüklenince FTP'de birbirine karışıyor (biri ötekinin üzerine yazılıyor ya da ilk biten kart diğerlerinin dosyasını
  siliyordu), yüklemeler hata veriyordu. Artık her yükleme FTP'de kendine özel geçici bir adla gider; her kart kendi
  kopyasını alır. Kartta oluşan dosya ve klasör adları değişmedi.
- **Aktarım pencerelerinde hangi kart olduğu görünüyor.** Karta yükleme, karttan alma ve kart içinde kopyalama / taşıma
  pencerelerinin başlığında ve ilk satırında kartın IP'si ve adı yazar (ör. *Karta yükleme — 10.0.0.5 (Kart1)*).

## Önemli notlar

- Vision'ı ağ klasöründen doğrudan çalıştırmayın; `Vision` klasörünü kendi bilgisayarınızda yerel bir klasöre kopyalayıp
  oradan açın.
- Seri port aynı anda yalnız bir programda açılabilir: port TeraTerm'de açıksa Vision açamaz ve bunu söyler.
- "Dosyaya kaydet" kayıtları artık `logs\TelnetLoglari` ve `logs\SeriKanalLoglari` altına gider; eski kayıtlar `logs`
  içinde kalır.
- Boot ayarı kayıtları, FTP parolası dahil kartın tüm boot alanlarını `config.xml` içinde düz metin olarak saklar.
- Sorun bildirirken `logs\hatalar.log` dosyasını eklemeniz sorunun bulunmasını kolaylaştırır (Yardım → Log klasörünü aç).
  Kartla ilgili olayların zaman çizelgesi için terminalde sağ tık → **Olay günlüğü…**

## Önceki sürümlerde gelenler

| Sürüm | Değişiklikler |
|---|---|
| 1.10 | Seri sekmesi TeraTerm'e yaklaştı: BIOS / boot manager tam ekran görünür (Ekran görünümü), Telnet'teki gibi alt komut satırı, F1–F12 / Home / End / PgUp / PgDn karta gider, Delete = DEL, Backspace = BS, Ctrl+C / Ctrl+V, yukarıdayken ekran kaymaz, UTF-8, çok satırlı yapıştırmada onay; ekran kayıtları `logs\TelnetLoglari` ve `logs\SeriKanalLoglari` altında; Tamam / Vazgeç pencerelerinde Enter ve Esc. |
| 1.9 | Beta geri bildirimi: bütün kartlara bspBootChange tek pencerede (kart listesi + seçilen kartın boot ekranı), boot ayarı sürerken kilitler, Çık (Ctrl+D) her zaman görünür, Temizle onayı, komut satırında tekerlekle geçmiş, ızgarada grup ve IP sırası, Yardım'da log klasörleri, ad pencerelerinde Enter, Ctrl+Shift+L kaldırıldı. |
| 1.8 | Seri sekmesi: COM portundan karta bağlantı (en fazla 12 port ızgarada, port başına ayarlar ve ad, doğrudan yazma, ayrı seri komut geçmişi). Sekmeye göre değişen ve her sekmede ayrı hatırlanan sol menü; yeni sekme sırası (Ctrl+3 Seri, Ctrl+4 Sentinel, Ctrl+5 Komut Kütüphanesi). Klasör yüklemede gidemeyen dosyalar atlanır, boyutlar doğrulanır, yüklenemeyenler olay günlüğünde tam listelenir. |
| 1.7 | Kart açılınca loglar reboot'u atana gelir: reboot'u gönderen Vision 2 sn'de bir, habersiz kopan diğerleri 30 sn bekleyip 15 sn'de bir dener. Düzeltmeler: Bağlan / Kes art arda basılınca arka planda bağlantı açık kalması; Boot ayarı kayıtları penceresi genişletilince satırların kayması. |
| 1.6 | Boot ayarı kayıtları: kartın boot ayarlarını adıyla kaydetme (kart başına en fazla 5), şu anki ayarlarla karşılaştırma, önizleme + onayla aynı ya da başka karta geri yükleme (başka kartta IP alanları atlanır), doğrulama ve yeniden başlatma önerisi; Boot ayarları penceresinden "Bu ayarları kaydet…" ve "Boot ayarı kayıtları…". Kendi gönderdiğimiz reboot'ta kırmızı uyarı yerine sarı "yeniden başlatılıyor"; kesinti ekrana ve olay günlüğüne yalnız bir kez, bağlantı gelince toplam süreyle yazılır. |
| 1.5 | Otomatik testler (her derlemede çalışır); kart başına olay günlüğü (bağlantı, reboot, boot ayarı değişiklikleri, Sentinel, dosya işlemleri, aktarımlar — süzme, arama, CSV); reboot onayında "Temizleyip yeniden başlat". |
| 1.4 | Yüksek kontrast temasından başka temaya geçişteki renk sorunu düzeltildi; reboot onay ister; kartta ve genel komut çubuğunda en fazla 5 hızlı komut; Dosya Gezgini'nde çoklu seçim (Ctrl+A, Ctrl / Shift + tık) ile toplu silme, kaydetme, taşıma / kopyalama ve sürükleme. |
| 1.3 | Klasör içindeki büyük dosyalar da parça parça yüklenir; kart içi kopyalama / taşımada ilerleme ve iptal; disk yeri denetimleri; aktarım güvenliği (aktarım sürerken komut engeli, boyut doğrulaması, bağlantı kopması ve kapanışta kartta kalanların temizlenmesi); beklenmeyen hatalarda Türkçe bildirim ve log. |
| 1.2 | Karttan büyük dosya alma (parçalı), anında iptal, anlık hıza göre kalan süre, indirmede ilerleme penceresi. |
| 1.1 | Büyük dosya yükleme: büyük dosyalar parça parça aktarılır, kopyalama süresi dosya boyutuna göre, yüklemeden önce boş alan denetimi, FAT 4 GB sınırı, yükleme ilerleme penceresi. |
| 1.0 | İlk resmî sürüm: çoklu kart telnet terminali, grup tabanlı kart yönetimi, genel komut çubuğu, bağlantı izleme ve otomatik yeniden bağlanma, Kart Dosya Gezgini, Komut Kütüphanesi, Boot ayarları penceresi, Sentinel sayfası, temalar, uygulama turu. |
