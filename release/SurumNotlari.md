# Vision 1.7 — Sürüm notları

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

- **Kart açılınca loglar reboot'u atana gelir.** Kartlara birden çok kişi bağlanabilir ama kartın logları kart açıldıktan
  sonra ilk bağlanan oturuma gider. Artık karta reboot'u gönderen Vision kart açılana kadar 2 saniyede bir dener; bağlantısı
  habersiz kopan diğer Vision'lar önce 30 saniye bekler, sonra 15 saniyede bir dener. Böylece kart açılınca önce reboot'u
  atan bağlanır ve loglar ona gelir. Vision kart geri gelene kadar denemeyi sürdürür; kesinti süresi olay günlüğüne yazılır.

## Düzeltmeler

- **Bağlan / Kes art arda basılınca:** Kart "bağlı değil" görünürken arka planda bir bağlantı açık kalıyor ve terminale log
  akmaya devam ediyordu. Artık Kes'e basılınca kurulmakta olan bağlantı da kapatılır; kart gerçekten bağlantısız kalır.
- **Boot ayarı kayıtları penceresi:** Pencere genişletilince karşılaştırma satırları başlıkla birlikte kaymıyor, tıklanınca
  yerine atlıyordu. Artık satırlar başlıkla birlikte hizalı kalır.

## Önemli notlar

- Boot ayarı kayıtları, FTP parolası dahil kartın tüm boot alanlarını `config.xml` içinde düz metin olarak saklar.
- Sorun bildirirken `logs\hatalar.log` dosyasını eklemeniz sorunun bulunmasını kolaylaştırır. Kartla ilgili olayların
  zaman çizelgesi için terminalde sağ tık → **Olay günlüğü…**

## Önceki sürümlerde gelenler

| Sürüm | Değişiklikler |
|---|---|
| 1.6 | Boot ayarı kayıtları: kartın boot ayarlarını adıyla kaydetme (kart başına en fazla 5), şu anki ayarlarla karşılaştırma, önizleme + onayla aynı ya da başka karta geri yükleme (başka kartta IP alanları atlanır), doğrulama ve yeniden başlatma önerisi; Boot ayarları penceresinden "Bu ayarları kaydet…" ve "Boot ayarı kayıtları…". Kendi gönderdiğimiz reboot'ta kırmızı uyarı yerine sarı "yeniden başlatılıyor"; kesinti ekrana ve olay günlüğüne yalnız bir kez, bağlantı gelince toplam süreyle yazılır. |
| 1.5 | Otomatik testler (her derlemede çalışır); kart başına olay günlüğü (bağlantı, reboot, boot ayarı değişiklikleri, Sentinel, dosya işlemleri, aktarımlar — süzme, arama, CSV); reboot onayında "Temizleyip yeniden başlat". |
| 1.4 | Yüksek kontrast temasından başka temaya geçişteki renk sorunu düzeltildi; reboot onay ister; kartta ve genel komut çubuğunda en fazla 5 hızlı komut; Dosya Gezgini'nde çoklu seçim (Ctrl+A, Ctrl / Shift + tık) ile toplu silme, kaydetme, taşıma / kopyalama ve sürükleme. |
| 1.3 | Klasör içindeki büyük dosyalar da parça parça yüklenir; kart içi kopyalama / taşımada ilerleme ve iptal; disk yeri denetimleri; aktarım güvenliği (aktarım sürerken komut engeli, boyut doğrulaması, bağlantı kopması ve kapanışta kartta kalanların temizlenmesi); beklenmeyen hatalarda Türkçe bildirim ve log. |
| 1.2 | Karttan büyük dosya alma (parçalı), anında iptal, anlık hıza göre kalan süre, indirmede ilerleme penceresi. |
| 1.1 | Büyük dosya yükleme: büyük dosyalar parça parça aktarılır, kopyalama süresi dosya boyutuna göre, yüklemeden önce boş alan denetimi, FAT 4 GB sınırı, yükleme ilerleme penceresi. |
| 1.0 | İlk resmî sürüm: çoklu kart telnet terminali, grup tabanlı kart yönetimi, genel komut çubuğu, bağlantı izleme ve otomatik yeniden bağlanma, Kart Dosya Gezgini, Komut Kütüphanesi, Boot ayarları penceresi, Sentinel sayfası, temalar, uygulama turu. |
