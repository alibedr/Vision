# Kullanım Kılavuzu

Vision; donanım kartlarına telnet ile bağlanmak, kartlara komut göndermek, birden çok kartın çıktısını aynı anda izlemek ve kartların dosya sistemini gezmek için geliştirilmiş bir masaüstü uygulamasıdır.

> Bu belge uygulamanın yanındaki `Yardim\KullanimKilavuzu.md` dosyasından okunur. Herhangi bir metin düzenleyiciyle güncelleyebilirsiniz; değişiklik, Yardım penceresi bir sonraki açılışta görünür.

## Ekranın bölümleri

| Bölüm | Konum | Ne işe yarar |
|---|---|---|
| Menü çubuğu | Pencerenin en üstü | **Yardım** menüsü: Kullanım Kılavuzu, Gereksinimler, Yardım klasörü, Hakkında. |
| Kart Gezgini | Sol kenar | Grupları ve kartları listeler; kart ekleme, seçme, bağlanma ve gösterme buradan yapılır. Sekme şeridinin en solundaki kenar çubuğu düğmesi (Ctrl+B) ile gizlenip gösterilir. |
| Sekmeler | Sağ alanın üstü | **Telnet** (kart terminalleri), **Kart Dosya Gezgini** ve **Komut Kütüphanesi** arasında geçiş (Ctrl+1 / 2 / 3). |
| Telnet sekmesi | Sağ alan | Seçilen kartların terminal pencereleri ızgara düzeninde görünür (en fazla 12 kart). |
| Terminal ayarları | Telnet sekmesinin üst çubuğu | Satır sonu, yerel eko, zaman damgası, dosyaya kaydet, satır sınırı ve "Tümünü kapat". |
| Genel komut çubuğu | Telnet sekmesinin altı | Bir komutu ekrandaki tüm kartlara aynı anda gönderir. |
| Durum çubuğu | Pencerenin en altı | Grup, kart ve bağlı kart sayıları; kısayol ipuçları ve geliştirici imzası. |

### Düğme simgeleri

Yer kazanmak için sık kullanılan düğmeler yazı yerine simgeyle gösterilir. Fareyi bir simgenin üzerinde bekletince **tam adı ve kısayolu** ipucu olarak görünür; ekran okuyucular da tam adı okur.

| Simge | Düğme | Nerede |
|---|---|---|
| Kâğıt uçak (mavi) | **Gönder** (Enter) | Kart penceresinin komut satırı |
| Yayın dalgaları (mavi) | **Hepsine Gönder** (Enter) | Genel komut çubuğu |
| Fiş (yeşil) | **Bağlantı Kur** | Kart penceresi başlığı · Dosya Gezgini · Kart Gezgini (yazıyla birlikte) |
| Üstü çizili fiş (kırmızı) | **Bağlantıyı Kes** | aynı yerler |
| Silgi | **Temizle** (ekran ve geçmiş) | Kart penceresi başlığı |
| Kare / iki kare · ✕ | **Büyüt / Eski boyut** · **Ekrandan kaldır** | Kart penceresi başlığı |
| Sol ok · dönen ok | **Geri** (Backspace) · **Yenile** (F5) | Dosya Gezgini |
| Artılı klasör · yukarı ok · dişli | **Yeni klasör** · **Yükle** · **FTP ayarları** | Dosya Gezgini |
| Kenar çubuklu pencere | **Kart Gezgini'ni gizle / göster** (Ctrl+B) | Sekme şeridinin en solu |

Pasif (o an kullanılamayan) düğmelerin simgesi soluk gri çizilir.

Terminal ayarlarındaki **Yerel eko**, **Zaman damgası** ve **Dosyaya kaydet** açma / kapama anahtarlarıdır: mavi ve sağa kaymışsa açık, gri ise kapalıdır (tıklayarak ya da odaktayken Boşluk tuşuyla değişir).

### Renkler ve okunabilirlik

Arayüz renkleri WCAG 2.1 AA kontrast ölçütüne göre ölçülüp ayarlanmıştır: normal metin her zeminde en az **4.5:1**, pasif düğme yazıları en az **3:1**. Ölçülen 41 metin / zemin çiftinin hepsi bu sınırı geçer (önceki sürümde 11 çift geçmiyordu). Ayrıntı: Gereksinimler → G12.

## Kart ekleme

1. Kart Gezgini'nde **+ Kart Ekle** düğmesine basın (ya da **Ctrl+N**).
2. **Grup adı**, **Port** (varsayılan 23), **Başlangıç IP adresi** ve **Kart sayısı** alanlarını doldurun.
3. Kart sayısı kadar IP alanı otomatik oluşur ve başlangıç IP'sinden sırayla doldurulur (ör. 192.168.1.10, .11, .12). Alanları elle değiştirebilirsiniz; **Sıralı doldur** tüm alanları yeniden sıralar.
4. **Grubu Oluştur** ile kaydedin.

Doğrulama kuralları: grup adı boş ya da var olan bir grupla aynı olamaz; IP'ler dört bölümlü geçerli IPv4 olmalı ve listede tekrar etmemelidir. Hatalı alanlar kırmızıyla işaretlenir.

Var olan bir gruba kart eklemek için gruba sağ tıklayıp **Gruba kart ekle…** seçin.

### Kartlara ad verme

Kartın ne için kullanıldığını göstermek için isteğe bağlı bir ad verebilirsiniz: karta sağ tıklayıp **Ad ver…** (ya da **F2**). Kart her yerde `IP (Ad)` olarak görünür, ör. `18.5.7.59 (Haberlesme)` ya da `192.168.1.10 (Navigation)`.

- Ad yalnız gösterim içindir; bağlantı ve tüm komutlar her zaman **IP adresiyle** yapılır.
- Ad Kart Gezgini'nde, kart penceresinin başlığında, Kart Dosya Gezgini'nin kart seçiminde ve uyarılarda görünür; Kart Gezgini aramasında da aranır.
- Değiştirmek için yine **Adı değiştir…**; kaldırmak için alanı boş bırakın.
- Ad `Konfig\config.xml` dosyasında saklanır; bağlantı kopsa da, yeniden bağlansa da kartla birlikte kalır.

### Gruplar nerede saklanır?

Gruplar, kartlar ve tüm ayarlar exe'nin yanındaki **`Konfig\config.xml`** dosyasında tutulur ve uygulama her açıldığında geri gelir. Kayıt otomatiktir: grup / kart eklenince veya silinince, ayar değişince ve uygulama kapanınca dosya yazılır.

| Geri gelir | Geri gelmez |
|---|---|
| Gruplar (ad, port, açık / kapalı), kartların IP'leri | Telnet sekmesinde açık olan kart pencereleri |
| Satır sonu, yerel eko, zaman damgası, dosyaya kaydet | Bağlantılar (kartlara yeniden bağlanılır) |
| Bağlantı izleme ve FTP ayarları | Terminal çıktıları ve komut geçmişleri |

- `Konfig` klasörü exe ile birlikte taşınır: exe klasörünü başka bir yere ya da bilgisayara kopyalarsanız gruplar da gelir. Yalnız **FTP parolası** Windows kullanıcınıza bağlı şifrelendiği için başka bir kullanıcıda / bilgisayarda yeniden girilmelidir.
- Visual Studio'dan çalıştırılan uygulama (`bin\Debug`) ile `HazirExe` klasöründeki uygulama **ayrı** `Konfig` klasörleri kullanır.
- Önceki sürümler ayarları `%APPDATA%\TelnetCards` içinde tutuyordu. İlk açılışta oradaki ayarlar `Konfig` klasörüne **kopyalanır**; eski dosya yedek olarak yerinde kalır.
- Exe yazma izni olmayan bir klasördeyse (ör. `Program Files`) ayarlar `%APPDATA%\TelnetCards` içinde tutulur ve durum çubuğunda bu belirtilir.
- Dosya bozulursa uygulama çökmez: bozuk dosyayı `config.xml.bozuk` olarak yedekleyip boş listeyle açılır.

## Kart Gezgini

- **Gruba tıklamak** grubu açar / kapatır. Grubun yanında `bağlı/toplam` rozeti ve portu görünür.
- **Karta çift tıklamak** kartı Telnet sekmesinde gösterir (bağlantıya dokunmaz).
- **Kutucuklar** ile birden çok kart seçilir. Grup kutucuğu, gruptaki tüm kartları işaretler.
- **Arama kutusu** grup adına veya IP'ye göre süzer.
- Kartın yanındaki renkli nokta bağlantı durumunu, `ekranda` yazısı kartın Telnet sekmesinde açık olduğunu gösterir.

**Seçim kuralı:** İşaretli kutucuk varsa seçili kartlar onlardır. Hiç işaret yoksa ağaçta tıklanmış kart (ya da tıklanmış grubun tüm kartları) seçili sayılır. Alt kısımdaki bilgi satırı seçimi özetler: `6 kart seçili · 4 bağlı · 2 ekranda`.

### Sağ tık menüleri

| Öğe | Seçenekler |
|---|---|
| Grup | Tümünü ekranda göster · Seçilileri ekranda göster · Tümünü bağla · Tümünün bağlantısını kes · Gruba kart ekle… · Grubu sil |
| Kart | Ekranda göster · Bağlan / Bağlantıyı kes · Dosya Gezgininde aç · Kartı kaldır |

Klavye: **Enter** seçili kartı (ya da grubu) ekranda gösterir, **Delete** seçili kartı / grubu siler (onay istenir).

## Bağlantı ve görünüm birbirinden bağımsızdır

Her kartın üç ayrı durumu vardır ve biri diğerini kendiliğinden değiştirmez:

| Durum | Anlamı | Nasıl değişir |
|---|---|---|
| Yapılandırma | Kart listede var mı | Kart Ekle / Kartı kaldır / Grubu sil |
| Bağlantı | Karta bağlı mı | **Bağlantı Kur / Bağlantıyı Kes** |
| Görünüm | Kart Telnet sekmesinde görünüyor mu | **Seçilenleri Göster**, çift tık, kart penceresindeki ✕ |

### Bağlantı Kur / Bağlantıyı Kes

Kart Gezgini'nin altındaki düğme seçili kartların **bağlantı** durumuna göre değişir:

- Bağlantı kurulurken kart en fazla **10 saniye** beklenir (yavaş kartlar için); kart hızlı cevap verirse beklenmez. Birden çok kart seçildiğinde her kart ayrı ve aynı anda bağlanır; yavaş ya da kapalı bir kart diğerlerini bekletmez. Süre `Konfig\config.xml` içindeki `ConnectTimeoutMs` değeriyle değiştirilebilir.

- Seçili kartların hepsi bağlıysa düğme **Bağlantıyı Kes** olur ve hepsinin bağlantısını keser.
- En az biri bağlı değilse düğme **Bağlantı Kur** olur ve yalnız bağlı olmayanlara bağlanır.
- Bu düğme kartları ekrana eklemez ve ekrandan kaldırmaz.

### Seçilenleri Göster

Telnet sekmesinde **tam olarak** seçili kartları bırakır: seçimden çıkan kartlar ekrandan kalkar (ama bağlı kalır), eksik olanlar eklenir. Bağlantı kurmaz, kesmez.

### Bağlantı durumları ve renkler

| Renk | Durum | Açıklama |
|---|---|---|
| Yeşil | Bağlı | Kart bağlı ve sağlıklı. |
| Sarı | Bağlanıyor | Bağlantı kuruluyor. |
| Kırmızı | Bağlantı kesildi · yeniden bağlanılacak | Bağlıyken koptu; kart ulaşılabilir olunca otomatik bağlanılacak. |
| Kırmızı | Bağlantı yok | Bağlanılamadı (ör. kart kapalı, port yanlış). |
| Gri | Bağlı değil | Kullanıcı bağlanmadı ya da bağlantıyı kesti. |

## Telnet sekmesi

Açık kartlar pencere boyutuna göre dinamik bir ızgaraya dizilir (2 kart → 2×1, 4 → 2×2, 6 → 3×2 …). Kartlar çok küçülecekse alan kaydırılabilir olur.

### En fazla 12 kart

Aynı anda en fazla **12 kart** gösterilebilir (uygulama sınırı; değiştirilemez). Daha fazlası açılmak istendiğinde:

- Boş yer kadar kart açılır; açılamayanlar bir uyarı penceresinde listelenir.
- Ekrandaki kartlar kapatılmaz ve değiştirilmez. Bir kartı kapatmak (✕) yer açar.
- **Seçilenleri Göster** ile 12'den fazla kart seçilirse, zaten ekranda olan seçili kartlar korunur ve kalan yer seçim sırasıyla doldurulur.
- Sınır tüm açma yollarında geçerlidir: çift tık, Enter, sağ tık → "Tümünü ekranda göster" / "Seçilileri ekranda göster", **Seçilenleri Göster**.

- Kart başlığına **çift tıklamak** (ya da ⬜) kartı büyütür; tekrar çift tıklamak ızgaraya döndürür.
- Kart penceresindeki **✕** kartı yalnız ekrandan kaldırır; bağlantı ve veri akışı arka planda sürer. Kart yeniden gösterildiğinde, kapalıyken gelen veriler de ekrandadır.
- Üst çubuktaki **Tümünü kapat** tüm kart pencerelerini ekrandan kaldırır (bağlantılar sürer).
- **Ctrl+W** seçili kart penceresini kapatır.

### Kart penceresi

| Öğe | Açıklama |
|---|---|
| Renkli nokta ve başlık | Kartın IP'si; altında grup, port ve bağlantı durumu. |
| ⏱ 00:12:34 | Bağlantı süresi. Bağlantının kurulduğu andan başlar, kopunca sıfırlanır. |
| RX / TX | Veri alınırken (yeşil) ve gönderilirken (mavi) yanar. |
| Fiş simgesi | **Bağlantı Kur** (yeşil fiş) / **Bağlantıyı Kes** (kırmızı, üstü çizili fiş): yalnız bu kartın bağlantısını kurar / keser. |
| Silgi simgesi | **Temizle**: kartın ekranını ve geçmişini temizler. |
| Kare / ✕ | Büyüt (büyütülmüşken iki kare: eski boyut) / ekrandan kaldır. |
| Kâğıt uçak (komut satırının sağı) | **Gönder** (Enter ile aynı). |

### Komut satırı

- **Enter** komutu gönderir. Boş Enter yalnız satır sonu gönderir.
- **↑ / ↓** kartın kendi komut geçmişinde gezinir. En yeni komutun da ötesine inince, geçmişe girmeden önce yazdığınız metin geri gelir. Boş komut saklanmaz; aynı komut tekrar kullanılırsa yinelenmez, en yeniye taşınır. Her kartın geçmişi ayrıdır.
- **Otomatik tamamlama:** Daha önce yazılmış bir komutun başını yazdığınızda (ör. `mod`), geçmişteki en son eşleşen komutun eksik kısmı imlecin sağında soluk renkte görünür (`moduleShow`). **Tab**, **↓** ya da imleç sondayken **→** öneriyi kabul eder. Kabul etmek komutu **çalıştırmaz**; ardından Enter ile gönderirsiniz. Yazmaya devam edebilir ya da **Esc** ile öneriyi gizleyebilirsiniz. Enter yalnız sizin yazdığınızı gönderir. Büyük / küçük harf duyarsızdır; genel komut çubuğunda da kendi geçmişiyle çalışır.
- **Ctrl+C** (metin seçili değilken) karta kesme karakteri (ETX) gönderir.

### Bağlı olmayan kart

Ekranda gösterilen ama bağlı olmayan kart gri tonlarda görünür; komut satırı kilitlenir ("Kart bağlı değil — komut yazılamaz"). Başlığın altındaki şerit durumu açıklar.

### Bağlantı koparsa

Bağlıyken bağlantı koparsa kart anında gri tona geçer ve:

- çıktının üstünde kırmızı **BAĞLANTI KESİLDİ** kutusu çıkar (tıklayınca kapanır),
- konsola kalın kırmızı `!!! BAĞLANTI KESİLDİ (saat): neden` satırı yazılır (her kopmada yalnız bir kez),
- kartın kenarlığı ve durum şeridi kırmızıya döner.

Kart yeniden ulaşılabilir olunca bağlantı otomatik kurulur ve konsola `Bağlantı yeniden kuruldu — kesinti süresi …` yazılır. Ayrıntılar için **Bağlantı izleme** bölümüne bakın.

### Yeni satır göstergesi

Eski çıktıyı okumak için yukarı kaydırdığınızda ekran yeni veriyle aşağı kaymaz. Bunun yerine çıktının altında **↓ Yeni Satırlar (N)** göstergesi bir kez belirir ve yalnız sayacı artar. Göstergeye tıklamak ya da elle en alta kaydırmak göstergeyi kapatır ve sayacı sıfırlar. En alttayken akış otomatik izlenir.

## Genel komut çubuğu

Telnet sekmesinin altındaki **TÜM AÇIK KARTLARA** çubuğu, yazılan komutu ekranda açık olan tüm kartlara aynı anda gönderir (**Ctrl+G** ile odaklanır).

- Yalnız Telnet sekmesinde açık kartlar komutu alır; listede olup ekranda olmayan kartlar almaz.
- Bağlı olmayan açık kartlar atlanır ve sonuçta raporlanır: `⚠ 3/4 karta gönderildi · gönderilemedi: 192.168.1.12 (bağlı değil)`.
- Bir kartın hatası diğer kartlara gönderimi durdurmaz.
- Kendi komut geçmişi vardır (↑ / ↓). Bu çubuk Kart Dosya Gezgini sekmesinde görünmez.

## Terminal ayarları

Üç kutucuk ve satır sonu seçimi tüm kartlar için ortaktır, anında geçerli olur ve kaydedilir. Hiçbiri karta gönderilen komutu ya da bağlantıyı etkilemez; yalnız ekranda ne gördüğünüzü ve neyin kaydedildiğini değiştirir.

### Satır sonu (Enter)

Enter'a basınca komutun sonuna eklenip karta gönderilen görünmez karakter:

| Seçenek | Gönderilen | Ne zaman |
|---|---|---|
| **CR+LF (varsayılan)** | Satır başı + Yeni satır (`\r\n`) | Telnet standardı; çoğu cihaz bununla çalışır. |
| CR | Yalnız Satır başı (`\r`) | Her Enter'da iki istem (`->`) geliyorsa. |
| LF | Yalnız Yeni satır (`\n`) | Linux/Unix tarzı cihazlar; komut gidip çalışmıyorsa. |
| Yok | Hiçbir şey | Yazılan metin olduğu gibi gider. |

### Satır sınırı (varsayılan: 1.000)

Her kartın terminalinde tutulan en fazla satır sayısı (100 – 100.000). Sınıra ulaşıldığında çıktı gelmeye devam eder: her yeni satırda **en eski satır silinir**, en yeni satırlar hep görünür. Terminal hiçbir zaman tümden temizlenmez.

- Sınır her karta ayrı uygulanır; Telnet sekmesinde görünmeyen kartların arka plandaki geçmişi de sınırlıdır (bellek şişmez).
- Değiştirdiğinizde açık ve kapalı tüm kartlara hemen uygulanır.
- Eski çıktıyı okumak için yukarı kaydırdıysanız, en eski satırlar silinse de okuduğunuz yer korunur; **↓ Yeni Satırlar** göstergesi eskisi gibi çalışır.
- **Dosyaya kaydet** açıksa log dosyasına tüm akış yazılır; satır sınırı yalnız ekranı etkiler.

### Yerel eko (varsayılan: açık)

Yazdığınız komutu, kart cevap vermeden önce kartın ekranına yazar; komut istemin yanına düşer (`-> ls "/ata0a"`). Kart da komutu geri ekolarsa ikinci kopya otomatik ayıklanır. Kapalıyken ekranda yalnız kartın gönderdikleri görünür. Komutlar ekranda iki kez görünüyorsa kapatın.

### Zaman damgası (varsayılan: kapalı)

Ekrana düşen her yeni satırın başına `[14:32:05.123]` biçiminde saat ekler. Saat, satırın bilgisayara ulaştığı andır (kartın saati değil). Yalnız açıldıktan sonra gelen satırlara eklenir; dosyaya kayıt açıksa log'a da yazılır.

### Dosyaya kaydet (varsayılan: kapalı)

Her kartın ekran akışını exe'nin yanındaki `logs` klasöründe ayrı bir dosyaya yazar: `GrupAdı_IP_tarih_saat.log` (UTF-8, her satır anında diske yazılır). Ekranda görünenin aynısını içerir (eko satırları, `***` bilgi satırları, `!!!` uyarıları, açıksa zaman damgaları). Dosya Gezgini'nin arka planda çalıştırdığı `devs` / `ls` komutları terminale düşmediği için bu dosyaya da yazılmaz.

## Kart Dosya Gezgini

**Kart Dosya Gezgini** sekmesi (**Ctrl+2**), kartın cihazlarını, klasörlerini ve dosyalarını `devs` ve `ls` komutlarını elle yazmadan gezmenizi sağlar. Karta sağ tıklayıp **Dosya Gezgininde aç** ile de açılır.

1. Üstten **kartı seçin**. Kart bağlıysa `devs` çalışır ve cihazlar listelenir. Yalnız `devs` çıktısındaki **name** sütunu kullanılır (`drv refs name` ve `drv name` biçimleri desteklenir).
   - Listede yalnız **içine girilebilen** cihazlar görünür: her cihaz `ll "cihaz"` ile denenir ve dönüş değeri **0** olanlar gösterilir. `/null`, `/tyCo/0` gibi girilemeyen cihazlar gizlenir; adları alttaki durum satırında yazar (ör. *içine girilemeyen 2 cihaz gizlendi (/null, /tyCo/0)*).
   - Denetim yalnız ilk açılışta yapılır; Cihazlar'a geri dönünce tekrarlanmaz. Kartta bir cihaz sonradan kullanılabilir olduysa Cihazlar'dayken **Yenile** (F5) ile yeniden denetleyin.
2. Bir cihaza ya da klasöre **çift tıklayın** (veya Enter): `ll "/yol"` çalışır ve içerik **tür, boyut ve tarihle** listelenir. Kartın imajında `ll` yoksa otomatik olarak `ls` kullanılır (bu durumda boyut / tarih görünmez).
3. Üstteki **yol çubuğundan** (breadcrumb) üst klasörlere doğrudan dönebilirsiniz.
4. **Geri** (sol ok simgesi, Backspace) önceki klasöre, **Yenile** (dönen ok simgesi, F5) mevcut klasörü yeniden yükler.

**Dönüş değeri:** Listeleme komutunun dönüş değeri (`value = 0 = 0x0` satırı) metin çıktısından ayrı okunur. **0** başarılıdır ve içerik gösterilir. **0 dışındaki** değerlerde kırmızı hata şeridi çıkar ve görüntülenen liste **değişmez**. Erişilebilir ama boş klasör "Bu klasör boş" olarak gösterilir.

**İkonlar:** cihaz, klasör (sarı), dosya, `?` (türü düz `ls` çıktısından anlaşılamayan öğe; açılmaya çalışılınca klasör ya da dosya olarak kesinleşir).

**Ham cevap ▸** (sağ alt) kartın son komuta verdiği cevabı olduğu gibi gösterir; ayrıştırma sorunlarını incelemek için kullanın.

Kart Dosya Gezgini'nde gezilen kart, soldaki Kart Gezgini'nde mavi şerit ve **gezginde** etiketiyle vurgulanır (grubu kapalıysa açılır). Kart değiştirildiğinde vurgu da değişir; başka sekmeye geçince kalkar. Vurgu kartın seçimini, işaretlerini ya da bağlantısını değiştirmez.

### Dosya ve klasör işlemleri

Bir klasörün içindeyken araç çubuğu, sağ tık menüsü ve klavye kısayollarıyla:

| İşlem | Nasıl | Kartta çalışan komut |
|---|---|---|
| Dosyayı görüntüle | Dosyaya çift tık / Enter / sağ tık → Görüntüle | `copy "dosya"` (içeriği ekrana basar) |
| Varsayılan uygulamayla aç (salt okunur) | Sağ tık / Ctrl+Enter | aşağıya bakın |
| Birlikte aç… (salt okunur) | Sağ tık / Ctrl+Shift+Enter | aşağıya bakın |
| Yeni klasör | Artılı klasör simgesi / Ctrl+Shift+N | `mkdir "yol"` |
| Yeniden adlandır | Sağ tık → Yeniden adlandır / F2 | `mv "eski", "yeni"` |
| Taşı | Öğeyi bir klasörün üzerine sürükleyip bırakın · sağ tık → Taşı… | `mv "kaynak", "hedef"` |
| Kopyala | Sürüklerken **Ctrl** basılı tutun · sağ tık → Kopyala… | dosya: `copy`, klasör: `xcopy` |
| Sil | Sağ tık → Sil / Delete | dosya: `rm` · boş klasör: `rmdir` · içindekilerle: `xdelete` |

- Öğeleri yol çubuğundaki (breadcrumb) bir bölümün üzerine bırakarak üst klasörlere de taşıyabilirsiniz.
- Silme her zaman onay ister. Klasörlerde **Yalnız boşsa sil** (`rmdir`, klasör doluysa hiçbir şey silmez) ve **İçindekilerle birlikte sil** (`xdelete`) seçenekleri vardır.
- Her işlemden sonra dönüş değeri kontrol edilir: **0** başarılıdır ve liste yenilenir; **0 dışı** değerde kırmızı hata şeridi çıkar.
- Görüntüleyicide satır kaydırma ve **Bilgisayara kaydet…** vardır. 1 MB'tan büyük dosyalar ve metin olmayan (ikili) dosyalar için önce onay istenir. Telnet bağlantısı ASCII karakterleri taşıdığı için Türkçe karakterler `?` olarak görünebilir.

> **Eksik komut uyarısı:** Kartın imajında (sembol tablosunda) olmayan bir komut gerektiren işlemde kart `undefined symbol: …` cevabı verir ve komutu çalıştırmaz. Uygulama bunu turuncu bir uyarı olarak gösterir: *Bu işlem için gereken "xdelete" komutu kartın imajında (sembol tablosunda) yok. Kartta hiçbir değişiklik yapılmadı.*

### Birlikte aç (salt okunur)

Kart dosyasını bilgisayarınızdaki bir uygulamayla (Notepad++, XML düzenleyici, onaltılık düzenleyici vb.) açar. Dosyaya sağ tıklayın:

- **Varsayılan uygulamayla aç (salt okunur)** — Ctrl+Enter: dosya uzantısının Windows'taki varsayılan uygulaması. Uzantının ilişkili uygulaması yoksa Windows'un uygulama seçme penceresi açılır.
- **Birlikte aç… (salt okunur)** — Ctrl+Shift+Enter: Windows'un "Birlikte aç" penceresinden uygulamayı siz seçersiniz.

> **Açılan dosya SALT OKUNURDUR.** Uygulama karttaki dosyanın bilgisayarınızdaki bir **kopyasını** açar. Kopyada yaptığınız değişiklikler **karta yansımaz**. Bu size üç yerde belirtilir: ilk açılışta bir açıklama penceresi (**Bir daha gösterme** seçilebilir), her açılışta Dosya Gezgini'ndeki mavi bilgi şeridi (**SALT OKUNUR KOPYA açıldı …** ve kopyanın yolu) ve kopyanın kendisi: dosya Windows'ta **Salt okunur** olarak işaretlidir ve `…\SaltOkunur\…` klasöründedir; çoğu düzenleyici başlıkta "salt okunur" yazar ve kaydetmenize izin vermez.
>
> Kartta bir dosyayı değiştirmek için: düzenlediğiniz içeriği bilgisayarınıza **farklı kaydedin**, sonra **Yükle** ile karta gönderin.

**Dosya nasıl alınır?**

| FTP ayarları | Yöntem | Hangi dosyalar |
|---|---|---|
| Girilmiş | Kartta `copy "dosya", "<kartın FTP yolu>/_tkt_<rastgele>_dosya"` ile FTP konumuna geçici bir kopya alınır, FTP'den bilgisayara indirilir, geçici kopya **her zaman** silinir. | Her tür dosya, bayt bayt aynı (ikili dosyalar dahil). |
| Girilmemiş | Sorulur: **FTP ayarları…** ya da **Metin olarak aç** (içerik `copy "dosya"` ile telnet üzerinden okunur). | Yalnız metin dosyaları (.txt, .xml, .cfg…). İkili dosyalar bu yolla bozulur. |

- Kopyalar `%TEMP%\TelnetCards\SaltOkunur\<oturum>\<kart IP>\<n>\<özgün dosya adı>` içinde tutulur; dosya adı değişmez (uygulama uzantıya göre seçilir). Aynı dosyayı tekrar açmak yeni bir kopya oluşturur, öncekini değiştirmez.
- Kopyalar uygulama kapanırken silinir; o sırada bir düzenleyicide açık olanlar bir sonraki açılışta temizlenir.
- 50 MB'tan büyük dosyalar (metin olarak açmada 1 MB) için önce onay istenir.
- Karttaki dosya ve klasör listesi bu işlemde **değişmez**.

### Bilgisayardan karta yükleme

Yükleme, kartlara dosya atarken elle izlediğiniz yolun aynısını otomatik yapar:

1. Dosya (ya da klasör) **kartın erişebildiği FTP sunucusuna** gönderilir.
2. Kartta `copy "FTP-konumu/dosya", "hedef/dosya"` (klasörlerde `xcopy`) çalıştırılır.
3. Dönüş değeri kontrol edilir; isteğe bağlı olarak FTP'deki kopya silinir.

Yüklemek için: Windows Gezgini'nden dosyaları listeye **sürükleyip bırakın** (bir klasörün üzerine bırakırsanız o klasöre yüklenir) ya da **Yükle** (yukarı ok simgesi) düğmesini kullanın. Hedefte aynı adlı öğe varsa üzerine yazmadan önce sorulur.

### FTP ayarları

İlk yüklemeden (ve "Birlikte aç"ta ikili dosyalar için) önce **FTP ayarları** (dişli simgesi) düğmesinden bir kez girin:

| Alan | Açıklama | Örnek |
|---|---|---|
| FTP sunucusu / Port | Kartın bağlı olduğu FTP sunucusu | `192.168.1.100` / `21` |
| Kullanıcı adı / Parola | FTP girişi. Parola Windows kullanıcı hesabınıza bağlı olarak şifrelenip saklanır. | `kart` |
| FTP klasörü | Dosyaların gönderileceği FTP klasörü | `/upload` |
| Kartın aynı klasörü gördüğü yol | Kartta `copy` / `xcopy` komutunun kaynağı | `host:/upload` |
| Pasif kip | Çoğu ağda açık kalmalıdır | açık |
| FTP'deki kopyayı sil | Kartta kopyalama başarılı olunca FTP'den temizler | isteğe bağlı |

**Bağlantıyı sına** düğmesi ayarları kaydetmeden önce FTP sunucusuna bağlanıp klasörü okumayı dener.

> Dosya Gezgini ayrı bağlantı açmaz; kartın mevcut bağlantısını kullanır. Kart bağlı değilse bu belirtilir ve **Bağlantı Kur** düğmesi gösterilir. Gezginin komutları ve cevapları terminale yazılmaz; terminal komutlarıyla sırayla çalışır, cevaplar birbirine karışmaz.

## Komut Kütüphanesi

**Komut Kütüphanesi** sekmesi (**Ctrl+3**), kart komutlarının açıklamalarını, yazılışlarını ve örnek çıktılarını tek yerde toplar. Komutlar **modüllerde** gruplanır: ör. **Task komutları** modülünde `i`, `rtpi`, `tt`, `tr`, `ts`, `td`, `sp`, `taskSpawn`; **Memory ve network** modülünde `adrSpaceShow`, `netstat`, `checkStack`. İçerik exe'nin yanındaki **`Komutlar\Komutlar.xml`** dosyasında tutulur.

### Ekran

| Bölüm | Ne gösterir |
|---|---|
| Üst çubuk | Toplam komut / modül sayısı; **Yeni modül** (Ctrl+Shift+M), **Yeni komut** (Ctrl+Shift+N) ve klasör simgesi (Komutlar klasörünü açar). |
| Sol liste | Arama kutusu ve modül ağacı. Her modül başlığında komut sayısı yazar; başlığın solundaki ok ile modül daraltılır / genişletilir. Modüle atanmamış komutlar en altta **Modülsüz** grubunda durur. Alttaki iki simge tüm modülleri genişletir / daraltır. |
| Komut sayfası | Komutun adı, bulunduğu modül (tıklayınca modülün sayfasına gider), son değişiklik zamanı, açıklama, yazılış (**Kopyala** ile panoya) ve örnek çıktı. |
| Modül sayfası | Bir modül başlığı seçilince açılır: modülün adı, açıklaması, komut sayısı ve komutlarının listesi (ad, yazılış, özet). Listeden bir komuta tıklamak o komutu açar. **Komut ekle**, **Düzenle**, **Sil** düğmeleri buradadır. |

### Modüller

- **Oluşturma:** **Yeni modül** → ad ve isteğe bağlı açıklama → **Oluştur**. Modül adı benzersizdir (büyük / küçük harf duyarsız); "Modülsüz" adı ayrılmıştır.
- **Düzenleme:** Modül sayfasında **Düzenle** ya da başlıkta F2 / sağ tık → **Modülü düzenle…**. Ad değişirse içindeki komutlar yeni adla gelir.
- **Silme:** Modül sayfasında **Sil** ya da başlıkta Delete. Modülde komut varsa iki seçenek sunulur: **Modülü sil, komutları koru** (komutlar Modülsüz grubuna geçer) ya da **Komutlarla birlikte sil**.
- Daraltılmış modüller oturum boyunca öyle kalır; aramada tüm eşleşen modüller açık gösterilir.

### Komutları modüle yerleştirme

| Yol | Nasıl |
|---|---|
| Yeni komut eklerken | Bir modül (ya da o modüldeki bir komut) seçiliyken **Yeni komut** komutu o modüle ekler. Modül sayfasındaki **Komut ekle** de aynıdır. Düzenleme ekranındaki **MODÜL** listesinden değiştirilebilir. |
| Sürükle-bırak | Soldaki listede komutu sürükleyip hedef modülün başlığının (ya da o modüldeki herhangi bir komutun) üzerine bırakın. Hedef modül mavi çerçeveyle vurgulanır. |
| Sağ tık | Komuta sağ tık → **Modüle taşı** → modül adı. **Modülsüz (modülden çıkar)** komutu modülden çıkarır; **Yeni modül…** yeni bir modül oluşturup komutu oraya taşır. |
| Düzenleme | Komutu **Düzenle** → **MODÜL** listesinden seçin → **Kaydet**. |

Bir komut tek bir modülde bulunur; komut adları kütüphane genelinde benzersizdir. Aynı adla ikinci bir komut eklenmek istenirse hangi modülde olduğu söylenir.

### Komut alanları

| Alan | Açıklama | Zorunlu |
|---|---|---|
| Komut adı | Ör. `taskSpawn`. Kütüphanede benzersizdir (büyük / küçük harf duyarsız). | Evet |
| Modül | Komutun bulunduğu modül ya da Modülsüz. | Hayır |
| Açıklama | Komutun ne yaptığı. İlk satırı listede özet olarak görünür. | Hayır |
| Yazılış / örnek kullanım | Ör. `ll "/ata0a"`. **Kopyala** ile panoya alınıp terminale yapıştırılabilir. | Hayır |
| Örnek çıktı | Kartın bu komuta verdiği örnek cevap. | Hayır |

- **Ekleme:** **Yeni komut** (Ctrl+Shift+N) → alanları doldurun → **Kaydet** (Ctrl+S).
- **Düzenleme:** Komutu seçip **Düzenle** (ya da listede çift tık / Enter / F2) → değiştirin → **Kaydet**. **İptal** (Esc) değişiklikleri geri alır.
- **Silme:** **Sil** (ya da listede Delete); onay istenir.
- **Arama:** Sol üstteki kutu (Ctrl+F) ad, açıklama, yazılış, örnek çıktı **ve modül adında** arar; Türkçe büyük / küçük harf duyarsızdır. Sonuçlar modüllerine göre gruplanır; adı aramayla başlayan komutlar önce gelir. Modül adını yazmak (ör. `task`) o modülün tüm komutlarını getirir. Esc aramayı temizler.
- **Klavye:** ↑ / ↓ listede gezinir; modül başlığında ← / → daraltır / genişletir, Enter açıp kapatır; komut üzerinde ← modül başlığına çıkar.
- Kaydedilmemiş değişiklik varken başka komuta / modüle, sekmeye geçmek ya da uygulamayı kapatmak istenirse **Kaydet / Kaydetme / İptal** sorulur.
- Her değişiklik dosyaya hemen yazılır; bir önceki sürüm `Komutlar.xml.yedek` olarak saklanır.
- Dosyayı bir metin düzenleyiciyle de düzenleyebilirsiniz (klasör simgesi). Sekmeye dönüldüğünde değişiklik algılanır ve kütüphane yeniden yüklenir.
- Yeniden derleme, `bin` klasöründeki `Komutlar.xml` dosyasının **üzerine yazmaz**; projedeki dosya yalnız hedefte dosya yoksa kopyalanır. Eklediğiniz komutlar ve modüller böylece korunur.
- Önceki sürümün (modülsüz) `Komutlar.xml` dosyası olduğu gibi açılır; tüm komutlar Modülsüz grubunda görünür, sonra istediğiniz modüllere taşıyabilirsiniz. İlk kayıtta dosya yeni biçime (Sürüm 2) geçer.

Elle düzenleme biçimi:

```
<KomutKutuphanesi Surum="2">
  <Modul Ad="Task komutları" Degistirilme="2026-11-05 13:10">
    <Aciklama>Görevleri listeleme, izleme ve başlatma.</Aciklama>
    <Komut Ad="i">
      <Aciklama>Sistemdeki görevleri özetler.</Aciklama>
      <Yazilis>i</Yazilis>
    </Komut>
    <Komut Ad="taskSpawn">
      <Aciklama>Yeni bir görev başlatır.</Aciklama>
    </Komut>
  </Modul>
  <Komut Ad="devs">
    <Aciklama>Karttaki cihazları listeler (modülsüz komut).</Aciklama>
    <Yazilis>devs</Yazilis>
  </Komut>
</KomutKutuphanesi>
```

## Bağlantı izleme ve otomatik yeniden bağlanma

Uygulama, bağlı kartların ulaşılabilirliğini komut beklemeden sürekli denetler. Denetim **TCP keepalive** ile mevcut bağlantı üzerinden yapılır: işletim sistemi boştaki bağlantıya küçük yoklama paketleri gönderir; bunları kartın kabuğu değil TCP yığını cevaplar. Terminale hiçbir şey yazılmaz, komutlarla çakışmaz, ek bağlantı açılmaz.

- **Algılama:** Varsayılan ayarlarla kart kapandığında ya da kablosu çekildiğinde kopma yaklaşık **6–9 saniyede** algılanır. Tek bir kayıp yoklama kopma sayılmaz; 3 ardışık cevapsız yoklama gerekir. Birkaç saniyelik geçici kesintiler uyarı üretmez.
- **Yeniden bağlanma:** Kopan kart, ulaşılabilir olunca aynı oturum üzerinden kendiliğinden yeniden bağlanır. Denemeler 3, 6, 12, 15, 15… saniye aralıklarla yapılır; başarısız denemeler sessizdir (tekrar uyarı yazılmaz).
- **Durdurma:** Kopmuş kartta **Bağlantıyı Kes** derseniz otomatik yeniden bağlanma durur.
- **Kapsam:** Hiç bağlanmadığınız kartlara izleme amacıyla bağlantı açılmaz.

### İzleme ayarları (isteğe bağlı)

Arayüzde ayar yoktur; değerler `Konfig\config.xml` dosyasındaki `<Monitor>` bölümünden değiştirilebilir (uygulama kapalıyken düzenleyin):

| Ayar | Varsayılan | Anlamı |
|---|---|---|
| `KeepAliveIdleMs` | 3000 | Son trafikten sonra ilk yoklamaya kadar geçen süre (ms). |
| `KeepAliveIntervalMs` | 2000 | Yoklamalar arası süre = yoklama başına zaman aşımı (ms). |
| `KeepAliveProbeCount` | 3 | Kopma için ardışık cevapsız yoklama sayısı. |
| `RetransmitTimeoutSec` | 10 | Gönderilen veri onaylanmazsa bağlantının düşürüleceği süre (sn). |
| `AutoReconnect` | true | Kopan kart otomatik yeniden bağlansın. |
| `ReconnectMinMs` / `ReconnectMaxMs` | 3000 / 15000 | Yeniden bağlanma denemeleri arası en kısa / en uzun süre. |
| `ReconnectTimeoutMs` | 3000 | Her yeniden bağlanma denemesinin zaman aşımı. |

> Ardışık yoklama sayısının ayarlanması Windows 10 sürüm 1703 ve sonrasında çalışır. Daha eski Windows'ta sistem bu sayıyı 10'da sabit tutar; algılama yaklaşık 20 saniyeye çıkar.

## Klavye kısayolları

| Kısayol | İşlev |
|---|---|
| Ctrl+N | Kart ekle |
| Ctrl+G | Genel komut çubuğuna odaklan (Telnet sekmesine geçer) |
| Ctrl+1 / 2 / 3 | Telnet / Kart Dosya Gezgini / Komut Kütüphanesi sekmesi |
| Ctrl+B | Kart Gezgini kenar çubuğunu gizle / göster |
| Tab / ↓ / → | Komut satırında otomatik tamamlama önerisini kabul et (çalıştırmaz) |
| Esc | Komut satırında öneriyi gizle |
| F2 | Kart Gezgini'nde kartın adını değiştir |
| Ctrl+W | Seçili kart penceresini kapat |
| F1 | Yardım (ayrıca menü çubuğundaki **Yardım** menüsü) |
| Ctrl+S / Esc | Komut Kütüphanesi'nde kaydet / iptal |
| Ctrl+F | Komut Kütüphanesi'nde ara |
| Enter | Komut gönder · Kart Gezgini'nde seçili kartı göster · Dosya Gezgini'nde aç |
| ↑ / ↓ | Komut geçmişi |
| Ctrl+C | Karta kesme (ETX) gönder (metin seçili değilken) |
| Delete | Kart Gezgini'nde seçili kartı / grubu sil |
| Backspace / F5 | Dosya Gezgini'nde Geri / Yenile |
| F2 / Delete | Dosya Gezgini'nde yeniden adlandır / sil |
| Ctrl+Shift+N | Dosya Gezgini'nde yeni klasör · Komut Kütüphanesi'nde yeni komut |
| Ctrl+Shift+M | Komut Kütüphanesi'nde yeni modül |
| Ctrl + sürükle | Dosya Gezgini'nde taşımak yerine kopyala |
| Ctrl+Enter / Ctrl+Shift+Enter | Dosya Gezgini'nde varsayılan uygulamayla / "Birlikte aç" ile aç (salt okunur kopya) |
| Esc | Açık pencereyi (Kart Ekle, Yardım) kapat |

## Dosyalar ve klasörler

| Konum | İçerik |
|---|---|
| `Konfig\config.xml` | Gruplar, kartlar, terminal, izleme ve FTP ayarları (otomatik kaydedilir; FTP parolası şifreli). |
| `Komutlar\Komutlar.xml` | Komut Kütüphanesi: modüller ve komutlar (önceki sürüm: `Komutlar.xml.yedek`). |
| `logs\GrupAdı_IP_tarih_saat.log` | "Dosyaya kaydet" açıkken kartların ekran akışı. |
| `logs\hatalar.log` | Bağlantı hatalarının teknik ayrıntıları (hata kodu, özgün İngilizce mesaj). Her zaman açıktır. |
| `Yardim\` | Bu kılavuz ve gereksinimler belgesi. |
| `%TEMP%\TelnetCards\SaltOkunur\` | "Birlikte aç" ile açılan salt okunur kopyalar (kapanışta silinir). |

Tüm bu klasörler exe'nin yanındadır (Visual Studio'dan çalıştırırken `bin\Debug\`). `Konfig` yazılamazsa ayarlar `%APPDATA%\TelnetCards` içinde tutulur.

## Sorun giderme

| Belirti | Olası neden ve çözüm |
|---|---|
| Her Enter'da iki istem (`->`) geliyor | Kart CR ve LF'yi ayrı Enter sayıyor. Satır sonunu **CR** yapın. |
| Komut gidiyor ama çalışmıyor | Satır sonunu **LF** ya da **CR** deneyin. |
| Komutlar ekranda iki kez görünüyor | Kart harfleri tek tek ekoluyor. **Yerel eko**'yu kapatın. |
| "Bağlantı reddedildi" | Kart bu portta dinlemiyor; grubun portunu kontrol edin. |
| "Karta ulaşılamıyor" / "zaman aşımı" | IP adresini, kabloyu ve kartın açık olduğunu kontrol edin. |
| Dosya Gezgini'nde cihaz listesi boş ya da yanlış | **Ham cevap ▸** panelinde kartın `devs` çıktısını inceleyin. |
| Listeleme sıfır olmayan değer döndürüyor | Yol yanlış, erişim yok ya da öğe bir klasör değil (ör. `/tyCo/0` gibi seri port cihazları). |
| Turuncu "komut kartın imajında yok" uyarısı | Kartın imajı bu komutu içermiyor; işlem o kartta yapılamaz. Kartta değişiklik olmaz. |
| Yükleme: "FTP'ye gönderilemedi" | FTP ayarlarını **Bağlantıyı sına** ile kontrol edin (adres, kullanıcı, parola, klasör izni). |
| Yükleme: "kartta kopyalanamadı" | "Kartın aynı klasörü gördüğü yol" ayarını kontrol edin; kartta elle `copy` ile deneyin. |

## Belgeleri güncelleme

Bu kılavuz ve gereksinimler belgesi Markdown biçimindedir. Proje içinde `Yardim\` klasöründedir; derlemede exe'nin yanına kopyalanır ve exe'nin içine de yedek olarak gömülür. Yardım penceresindeki **Yardım klasörünü aç** düğmesiyle dosyalara ulaşabilirsiniz. Yeni özellik eklendikçe ilgili bölüm güncellenmelidir.
