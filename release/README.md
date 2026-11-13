# Vision sürümleri

Her sürümün derlenmiş, doğrudan kullanılabilir hali bu klasörde (zipten çıkarılmış olarak) tutulur (v1.1 ile başladı):

```
release/
  v1.1/Vision/
    Vision.exe
    lib/          (Edge WebView2 DLL'leri)
  v1.2/Vision/
  …
```

**Kullanım:** İlgili sürümün `Vision` klasörünü bütün olarak istediğiniz yere kopyalayıp `Vision.exe`'yi çalıştırın.
`lib` klasörü (Sentinel sayfası için Edge WebView2 DLL'leri) `Vision.exe`'nin yanında kalmalıdır.

**Gereksinimler:** Windows, .NET Framework 4.6.1 veya üstü. Sentinel sayfası için Microsoft Edge WebView2 Runtime
(Windows 10/11'de genellikle yüklüdür; yoksa sayfa eski görüntüleyiciyle açılır).

Her klasör, o sürümün `main`'e birleştirilen kodundan GitHub Actions'ta derlenen paketin içeriğidir; aynı paket GitHub
Releases'ta `vX.Y` etiketiyle `Vision.zip` olarak da yayınlanır.

| Sürüm | Değişiklikler |
|---|---|
| v1.3 | Klasör içindeki büyük dosyalar da parça parça yüklenir; kart içi kopyalama / diskler arası taşımada boyuta göre bekleme, ilerleme penceresi ve iptal; karttan almadan önce bilgisayarda yer denetimi; işlemlerden sonra disk boş alanı güncellenir; Birlikte aç'ta ilerleme penceresi; parça 64 MB / kartta ara bellek 8 MB; aktarım sürerken o karta komut gönderilmez, boyut doğrulaması, bağlantı kopması ve kapanışta kartta kalanların temizlenmesi, FTP artıklarının bulunması; beklenmeyen hatalarda Türkçe bildirim ve log, log dosyası 5 MB'ta döndürülür; Yüksek kontrast temasından başka temaya geçişte yanlış kalan renkler düzeltildi, yardım sayfası ve menüler temanın renklerinde. |
| v1.2 | Karttan büyük dosya alma (parçalı), anında iptal, anlık hıza göre kalan süre, indirmede ilerleme penceresi. |
| v1.1 | Büyük dosya yükleme: 1 GB üstü dosyalar parça parça aktarılır, kartta kopyalama süresi dosya boyutuna göre, yüklemeden önce boş alan denetimi, FAT 4 GB sınırı, yükleme ilerleme penceresi. |
