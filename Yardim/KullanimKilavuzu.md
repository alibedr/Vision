# Kullanım Kılavuzu

Vision; donanım kartlarına telnet ile bağlanmak, kartlara komut göndermek, birden çok kartın çıktısını aynı anda izlemek ve kartların dosya sistemini gezmek için geliştirilmiş bir masaüstü uygulamasıdır.

> Bu belge uygulamanın yanındaki `Yardim\KullanimKilavuzu.md` dosyasından okunur. Herhangi bir metin düzenleyiciyle güncelleyebilirsiniz; değişiklik, Yardım penceresi bir sonraki açılışta görünür.

## Ekranın bölümleri

| Bölüm | Konum | Ne işe yarar |
|---|---|---|
| Kart Gezgini | Sol kenar | Grupları ve kartları listeler; kart ekleme, seçme, bağlanma ve gösterme buradan yapılır. Sekme şeridinin en solundaki kenar çubuğu düğmesi (Ctrl+B) ile gizlenip gösterilir. |
| Sekmeler | Sağ alanın üstü | **Telnet** (kart terminalleri), **Kart Dosya Gezgini** ve **Komut Kütüphanesi** arasında geçiş (Ctrl+1 / 2 / 3). Aynı şeridin en sağında **Yardım** (Kullanım Kılavuzu, Gereksinimler, Yardım klasörü, Hakkında) ve **Temalar** menüsü (hazır temalar ve özelleştirme; aşağıya bakın) bulunur. |
| Telnet sekmesi | Sağ alan | Seçilen kartların terminal pencereleri ızgara düzeninde görünür (en fazla 24 kart). |
| Terminal ayarları | Telnet sekmesinin üst çubuğu | Satır sonu, yerel eko, zaman damgası, dosyaya kaydet, satır sınırı ve "Tümünü kapat". |
| Genel komut çubuğu | Telnet sekmesinin altı | Bir komutu ekrandaki tüm kartlara aynı anda gönderir. |
| Durum çubuğu | Pencerenin en altı | Grup, kart ve bağlı kart sayıları; kısayol ipuçları ve geliştirici imzası. |

### Temalar

Sekme şeridinin en sağındaki **Temalar** düğmesi (Yardım'ın yanında, palet simgesi) hazır temaları listeler. Seçili tema işaretlidir; her temanın yanında küçük bir renk örneği görünür.

| Tema | Görünüm |
|---|---|
| Koyu | Vision'ın varsayılan koyu teması |
| Açık | Aydınlık ortamlar için açık tema |
| Solarized | Göz yormayan, düşük parlaklıkta mavi-yeşil tonlar |
| Monokai | Kod düzenleyicilerden tanıdık sıcak koyu tema |
| Yüksek kontrast | En yüksek okunabilirlik: siyah zemin, parlak yazı |
| Klasik terminal | Siyah zemin üzerinde yeşil fosforlu terminal görünümü |

- Geçiş anında olur: açık tüm pencereler (kart terminalleri, Dosya Gezgini, Komut Kütüphanesi, başlık ve kaydırma çubukları) yeni temaya geçer. Bağlantılar kesilmez, terminal içerikleri silinmez.
- **Ctrl+Shift+L** listedeki sonraki temaya geçer.
- Seçim ayarlarla birlikte kaydedilir; uygulama bir sonraki açılışta aynı temayla açılır. Her kullanıcının seçimi kendi bilgisayarındadır.
- Yardım sayfası da açıldığında o anki temanın renkleriyle gösterilir.

### Temayı özelleştirme

**Temalar → Temayı özelleştir…** düzenleyiciyi açar:

1. Üstteki listeden düzenlemek istediğiniz temayı seçin (seçtiğiniz tema hemen uygulanır).
2. **Renkler** listesinde değiştirmek istediğiniz satıra tıklayın ve renk seçiciden yeni rengi seçin. Her temanın 10 rengi düzenlenebilir: arka plan, paneller, kenarlıklar, yazı, vurgu, başarılı / bağlı, uyarı, hata, terminal zemini, terminal yazısı. İkincil yazı, seçim, şerit ve düğme tonları gibi diğer renkler bunlardan otomatik türetilir.
3. Değişiklik **anında** tüm uygulamada ve penceredeki önizlemede görünür. Değiştirilmiş renklerin yanında bir nokta ve vurgu renginde `#RRGGBB` değeri görünür.
4. **Kaydet** seçili temayı ve değişiklikleri kalıcı yapar. **İptal** (ya da pencereyi kapatmak) pencere açılmadan önceki tema ve renklere döner.

- **Varsayılana dön**, görüntülenen temanın tüm renklerini Vision'ın hazır değerlerine döndürür; Kaydet ile kalıcı olur.
- Yazı ile zemin arasındaki kontrast düşükse (4.5:1'in altı) pencere uyarır; bu durumda yazılar zor okunabilir.
- Özelleştirilmiş temalar Temalar menüsünde ve düzenleyicide **(özelleştirildi)** olarak işaretlenir. Birden çok temayı ayrı ayrı özelleştirebilirsiniz; her temanın değişiklikleri kendisine aittir.
- Birincil düğmelerin yazısı beyazdır; seçtiğiniz vurgu rengi çok açıksa düğme zemini okunur kalacak şekilde otomatik koyulaştırılır.

### Düğme simgeleri

Yer kazanmak için sık kullanılan düğmeler yazı yerine simgeyle gösterilir. Fareyi bir simgenin üzerinde bekletince **tam adı ve kısayolu** ipucu olarak görünür; ekran okuyucular da tam adı okur.

| Simge | Düğme | Nerede |
|---|---|---|
| Kâğıt uçak (mavi) | **Gönder** (Enter) | Kart penceresinin komut satırı |
| Yayın dalgaları (mavi) | **Hepsine Gönder** (Enter) | Genel komut çubuğu |
| Fiş (yeşil) | **Bağlantı Kur** | Kart penceresi başlığı · Dosya Gezgini · Kart Gezgini (yazıyla birlikte) |
| Üstü çizili fiş (kırmızı) | **Bağlantıyı Kes** | aynı yerler |
| Silgi | **Temizle** (ekran ve geçmiş) | Kart penceresi başlığı · Genel komut çubuğu (tüm kartlar) |
| Kare / iki kare · ✕ | **Büyüt / Eski boyut** · **Ekrandan kaldır** | Kart penceresi başlığı |
| Sol ok · dönen ok | **Geri** (Backspace) · **Yenile** (F5) | Dosya Gezgini |
| Artılı klasör · yukarı ok · dişli | **Yeni klasör** · **Yükle** · **FTP ayarları** | Dosya Gezgini |
| Kenar çubuklu pencere | **Kart Gezgini'ni gizle / göster** (Ctrl+B) | Sekme şeridinin en solu |
| Disket | **Dosyaya kaydet** (terminalin anlık görüntüsü) | Kart penceresi başlığı · Genel komut çubuğu |
| Şimşek | **Hızlı komut** (atanmış komutu gönderir) | Kart komut satırı · Genel komut çubuğu |
| Geri dönen ok + saat | **Komut geçmişi** (Ctrl+R): yazılmış tüm komutlar ve geriye doğru arama | Kart komut satırının içi (sağda) · Genel komut çubuğu |
| Güç + ⇄ | **reboot / reboot 2** (sol: çalıştır, sağ ⇄: seçimi değiştir) | Kart komut satırı · Genel komut çubuğu |
| − %100 + | **Yazı** (tüm terminallerin ortak yakınlaştırması) | Telnet ayar çubuğu |
| Çöp kutusu | **FTP kopyalarını sil** | Dosya Gezgini |

Pasif (o an kullanılamayan) düğmelerin simgesi soluk gri çizilir.

Terminal ayarlarındaki **Yerel eko**, **Zaman damgası** ve **Dönüş değerlerini ayır** açma / kapama anahtarlarıdır: mavi ve sağa kaymışsa açık, gri ise kapalıdır (tıklayarak ya da odaktayken Boşluk tuşuyla değişir).

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
- Ad `config.xml` dosyasında saklanır; bağlantı kopsa da, yeniden bağlansa da kartla birlikte kalır.

### Gruplar nerede saklanır?

Gruplar, kartlar ve tüm ayarlar bilgisayarınızda, **`%APPDATA%\TelnetCards\config.xml`** dosyasında tutulur ve uygulama her açıldığında geri gelir. Kayıt otomatiktir: grup / kart eklenince veya silinince, ayar değişince ve uygulama kapanınca dosya yazılır.

| Geri gelir | Geri gelmez |
|---|---|
| Gruplar (ad, port, açık / kapalı), kartların IP'leri | Telnet sekmesinde açık olan kart pencereleri |
| Satır sonu, yerel eko, zaman damgası, dosyaya kaydet | Bağlantılar (kartlara yeniden bağlanılır) |
| Bağlantı izleme ve FTP ayarları | Terminal çıktıları |
| Komut geçmişi (ayrı dosya: `KomutGecmisi.txt`) | Kart ekranındaki renklendirmeler |

- Dosya exe'den bağımsızdır: yeni bir Vision sürümünü indirip başka bir klasörden çalıştırdığınızda gruplarınız, kartlarınız ve ayarlarınız **aynen gelir**; her sürümde yeniden girmeniz gerekmez.
- Visual Studio'dan çalıştırılan uygulama (`bin\Debug`) ile `HazirExe` klasöründeki uygulama **aynı** ayar dosyasını kullanır.
- Bir önceki sürüm ayarları exe'nin yanındaki `Konfig\config.xml` içinde tutuyordu. `%APPDATA%\TelnetCards\config.xml` henüz yoksa, ilk açılışta exe'nin yanındaki `Konfig\config.xml` oraya **kopyalanır**; eski dosya yedek olarak yerinde kalır.
- Ayarları başka bir bilgisayara taşımak için `config.xml` dosyasını kopyalayın. **FTP parolası** Windows kullanıcınıza bağlı şifrelendiği için başka bir kullanıcıda / bilgisayarda yeniden girilmelidir.
- `%APPDATA%\TelnetCards` klasörüne yazılamıyorsa durum çubuğunda uyarı gösterilir.
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

- Bağlantı kurulurken kart en fazla **40 saniye** beklenir (yavaş kartlar için); kart hızlı cevap verirse beklenmez. Birden çok kart seçildiğinde her kart ayrı ve aynı anda bağlanır; yavaş ya da kapalı bir kart diğerlerini bekletmez. Süre `%APPDATA%\TelnetCards\config.xml` içindeki `ConnectTimeoutMs` değeriyle değiştirilebilir (eski sürümlerin 5000 / 10000 varsayılanları açılışta 40000'e yükseltilir).

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

### En fazla 24 kart

Aynı anda en fazla **24 kart** gösterilebilir (uygulama sınırı; değiştirilemez). Daha fazlası açılmak istendiğinde:

- Boş yer kadar kart açılır; açılamayanlar bir uyarı penceresinde listelenir.
- Ekrandaki kartlar kapatılmaz ve değiştirilmez. Bir kartı kapatmak (✕) yer açar.
- **Seçilenleri Göster** ile 24'ten fazla kart seçilirse, zaten ekranda olan seçili kartlar korunur ve kalan yer seçim sırasıyla doldurulur.
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
| Disket simgesi | **Dosyaya kaydet**: terminalde o an görünen içeriği bu kart için ayrı bir dosyaya kaydeder (aşağıya bakın). |
| Kâğıt uçak (komut satırının sağı) | **Gönder** (Enter ile aynı). |
| Saat simgesi (komut alanının içinde, sağda) | **Komut geçmişi** (Ctrl+R): daha önce yazılan tüm komutlar; içinde arama (aşağıya bakın). |
| Terminalde sağ tık | **Kopyala** ve **Renklendir**: seçilen kelimeyi ekranda renkli gösterir (aşağıya bakın). |
| Şimşek düğmesi (komut satırı) | **Hızlı komut**: bu karta atanmış komutu tek tıkla YALNIZ bu karta gönderir (aşağıya bakın). |
| reboot ⇄ (komut satırı) | Sol bölme seçili komutu (`reboot` ya da `reboot 2`) YALNIZ bu karta gönderir; sağdaki ⇄ yalnız seçimi değiştirir. |
| **Dönüş** çubuğu (komut satırının üstü) | Komutların dönüş değerleri (`value = …`); tıklayınca liste açılır (aşağıya bakın). |

### Hızlı komut

Her kartın komut satırının sağında bir **hızlı komut** düğmesi (şimşek) vardır.

- İlk tıklamada (komut atanmamışsa) komut sorulur; sonra düğmede komutun kendisi yazar (ör. `i`).
- Tıklayınca komut **yalnız o karta** gönderilir; diğer kartlar etkilenmez. Her kartın hızlı komutu ayrıdır.
- **Sağ tık**: hızlı komutu düzenle / kaldır. Komut kartla birlikte `config.xml`'e kaydedilir.
- Kart bağlı değilken düğme pasiftir. Çift tıklama komutu iki kez göndermez.
- Kart penceresi daraldığında düğme yalnız simge olarak görünür (ipucunda komut yazar); çok dar kartlarda gizlenir.

### reboot / reboot 2

Hızlı komutun yanındaki düğme iki bölmelidir:

- **Sol bölme** (güç simgesi + seçili komut): seçili komutu — `reboot` ya da `reboot 2` — **yalnız bu karta** gönderir.
- **Sağ bölme (⇄)**: seçimi `reboot` ↔ `reboot 2` arasında değiştirir. **Seçimi değiştirmek hiçbir komut göndermez**; çalıştırmak için her zaman sol bölmeye tıklamak gerekir.
- Seçili komut düğmenin üzerinde her zaman yazar ve kartla birlikte kaydedilir.
- Yanlışlıkla art arda tıklamaya karşı: gönderdikten sonra düğme **4 saniye** pasif kalır; bu sürede ikinci kez gönderilmez.

### Komut dönüş değerleri

`sp görev` gibi bir komut çalıştırınca kabuk bir dönüş değeri basar (`task spawned: id = 0x1f2a, name = t1`, `value = 522 = 0x20a`). Canlı loglar akarken bu satırlar terminalin ortasına düşüp okunmayı zorlaştırıyordu. **Dönüş değerlerini ayır** açıkken (varsayılan):

- Bu satırlar terminalden ayrılır; terminalde loglar ve komutun normal çıktısı kesintisiz akar.
- Kart penceresinin altındaki **Dönüş** çubuğu son komutu ve dönüş değerini gösterir: `Dönüş (12): sp görev → value = 522 = 0x20a · 14:02:11`.
- Çubuğa tıklayınca bu kartın komut listesi açılır: her komut, gönderildiği saat ve kendi dönüş satırlarıyla birlikte. Hızlı komut, genel komut ya da reboot ile gönderilenler kaynaklarıyla işaretlenir.
- Silgi simgesi yalnız bu listeyi temizler (terminale dokunmaz). Her kartın listesi ayrıdır.

**Nasıl ayırıyor?** Yalnız siz bir komut gönderdikten sonra ve kabuk yeni istemi (`->`) basana kadar gelen, kabuğun dönüş biçimine **tam uyan** satırlar ayrılır (`value = …`, `task spawned: …`, `new symbol "…" added to … symbol table`). Diğer her satır — loglar, tablolar, `undefined symbol` gibi hata mesajları — terminalde kalır; hiçbir satır kaybolmaz. Komutlar kabukta sırayla çalıştığından her dönüş doğru komutla eşleşir.

**Dönüş değerlerini ayır** kapatılırsa dönüş değerleri eskisi gibi terminalde görünür.

### Komut satırı

- **Enter** komutu gönderir. Boş Enter yalnız satır sonu gönderir.
- **↑ / ↓** komut geçmişinde gezinir. En yeni komutun da ötesine inince, geçmişe girmeden önce yazdığınız metin geri gelir. Boş komut saklanmaz; aynı komut tekrar kullanılırsa yinelenmez, en yeniye taşınır.
- **Geçmiş kalıcıdır ve ortaktır:** Yazdığınız her komut `%APPDATA%\TelnetCards\KomutGecmisi.txt` dosyasına hemen kaydedilir; uygulamayı kapatıp açtığınızda geçmiş ve otomatik tamamlama önerileri geri gelir. Tüm kartlar ve genel komut çubuğu aynı geçmişi kullanır: bir kartta yazdığınız komut diğer kartlarda da ↑ ile ve otomatik tamamlamada bulunur. En fazla 5.000 komut tutulur; dolunca en uzun süredir kullanılmayan silinir.
- **Otomatik tamamlama:** Daha önce yazılmış bir komutun başını yazdığınızda (ör. `mod`), geçmişteki en son eşleşen komutun eksik kısmı imlecin sağında soluk renkte görünür (`moduleShow`). **Tab**, **↓** ya da imleç sondayken **→** öneriyi kabul eder. Kabul etmek komutu **çalıştırmaz**; ardından Enter ile gönderirsiniz. Yazmaya devam edebilir ya da **Esc** ile öneriyi gizleyebilirsiniz. Enter yalnız sizin yazdığınızı gönderir. Büyük / küçük harf duyarsızdır; genel komut çubuğunda da çalışır.
- **Ctrl+R** ya da komut alanının içindeki **saat simgesi** komut geçmişi penceresini açar (aşağıya bakın).
- **Ctrl+C** (metin seçili değilken) karta kesme karakteri (ETX) gönderir.

### Komut geçmişi penceresi ve geriye doğru arama (Ctrl+R)

Komut alanının sağındaki **saat simgesine** tıklayın ya da komut satırındayken **Ctrl+R**'ye basın. Pencerede daha önce yazılmış **tüm** komutlar en yeniden eskiye listelenir.

- Üstteki arama kutusuna yazdıkça yalnız o metni **içeren** komutlar kalır (komutun başında olması gerekmez; büyük / küçük harf duyarsız). Eşleşen kısım sarı zeminle gösterilir ve en yeni eşleşme seçili gelir.
- Pencereyi Ctrl+R ile açarken komut satırında yazılı bir metin varsa arama o metinle başlar.
- **↑ / ↓** (ya da Page Up / Down) seçimi taşır; **Ctrl+R** bir sonraki (daha eski) eşleşmeye, **Ctrl+Shift+R** bir öncekine geçer.
- **Enter**, **çift tık** ya da **Komut satırına al**: seçilen komut komut satırına yazılır, **gönderilmez**; düzenleyip Enter ile gönderirsiniz.
- **Sil** (listede **Delete**, arama kutusunda **Shift+Delete**) seçili komutu geçmişten çıkarır. **Tümünü sil…** onay sorup tüm geçmişi siler.
- **Esc** ya da **Kapat** komut satırına dokunmadan kapatır.
- Kart bağlı değilken kartın geçmiş düğmesi pasiftir (komut satırı yazılamaz).

### Kart ekranında renklendirme

Akan loglar arasında bir kelimeyi (ör. `error`) kolayca görmek için:

1. Terminalde kelimeyi seçin ya da doğrudan **kelimenin üzerine sağ tıklayın** (seçim yoksa tıklanan kelime kendiliğinden seçilir).
2. **Renklendir: "error"** menüsünden bir renk seçin (Sarı, Turuncu, Kırmızı, Pembe, Mor, Mavi, Camgöbeği, Yeşil) ya da **Diğer renk…** ile istediğiniz rengi belirleyin.

- Ekranda o metnin geçtiği **her yer** seçilen renkli zeminle gösterilir; sonradan gelen satırlarda da kendiliğinden boyanır. Eşleşme büyük / küçük harf duyarsızdır (`error`, `Error`, `ERROR`).
- Aynı metni başka renge boyamak rengini değiştirir. Birden çok metin farklı renklerle boyanabilir (en fazla 30).
- Kaldırmak için metnin üzerinde sağ tık → **Renklendirmeyi kaldır**, ya da **Renklendirmeler** alt menüsünden tek tek / **Tüm renklendirmeleri kaldır**.
- Renklendirmeler **o karta** aittir ve yalnız ekrandaki görünümü değiştirir (karta bir şey gönderilmez, kayıt dosyalarına renk yazılmaz). Kart penceresi kapatılıp yeniden açılsa da korunur; uygulama kapanınca silinir.
- Seçim tek satırda olmalıdır (en fazla 200 karakter); baştaki ve sondaki boşluklar yok sayılır.

### Boot ayarları penceresi (bspBootChange / bootChange)

Kart terminalinden (komut satırı, hızlı komut ya da genel komut çubuğu) `bspBootChange` veya `bootChange` gönderdiğinizde **Boot ayarları** penceresi açılır. Kart boot ayarlarını tek tek sorar; kart çalışırken gelen loglar bu soruların arasına karıştığı için terminalde hangi alanda olduğunuz kayabilir. Pencere kartın sorduğu alanları loglardan ayırarak bir listede gösterir:

- Üstte **şu an sorulan alan** ve kartın o alandaki değeri yazar; listede o satır vurgulanır (▶).
- **Yeni değer** kutusuna yazıp **Enter** (ya da **Gönder**): değer karta gider, kart bir sonraki alana geçer. Kutu boşken Enter ya da **Değiştirmeden geç**: alan aynen kalır.
- **Değeri düzenle** (F2): kartın değerini kutuya kopyalar; küçük bir değişiklik yapıp Enter'a basın.
- **Temizle (.)**: alanı boşaltır. **◀ Önceki alan**: bir önceki alana döner (karta `-` gönderilir).
- Listede bir alana **çift tıklayın**: kart o alana gelene kadar alanlar değiştirilmeden geçilir (geri gitmek için `-` gönderilir).
- **Kalanları geçip bitir**: kalan alanların hepsi değiştirilmeden geçilir; girdiğiniz değerler kaydedilir. **Çık (Ctrl+D)**: komuttan hemen çıkar (kartın imajına göre değişiklikler kaydedilmeyebilir).
- Değiştirdiğiniz alanlar listede `eski → yeni` olarak görünür. Komut bitince kaç alanın değiştiği yazar; yeni boot ayarları genellikle kart yeniden başlatılınca geçerli olur.
- **Araya giren kart logları** (alt şerit, tıklayınca açılır): bu sırada gelen loglar kaybolmaz, burada toplanır. Kartın tüm çıktısı terminalde de görünmeye devam eder.
- Pencere kart bitirmeden kapatılırsa sorulur: **Kalanları geçip bitir**, **Yalnız pencereyi kapat** (terminalden devam edersiniz) ya da **Vazgeç**.
- Pencere açıkken o kartta Dosya Gezgini gibi arka plan komutları gönderilmez (gönderilseler alan değeri olarak yazılırdı); pencere bitince yeniden çalışır.
- Genel komut çubuğundan gönderildiyse bağlı her kart için ayrı pencere açılır.

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

Akış izlenirken ekran yalnız **dikeyde** en alta iner; **yatay konum değişmez**. Kart penceresinden uzun log satırları ekranı sağa ya da sola kaydırmaz. Uzun bir satırın devamını görmek için alttaki yatay kaydırma çubuğunu kullanın; ekran, yeni satırlar gelse de bıraktığınız konumda kalır. Kart ekranının dikey ve yatay kaydırma çubukları her zaman görünür; son satır yatay çubuğun altında kalmaz.

## Genel komut çubuğu

Telnet sekmesinin altındaki **TÜM AÇIK KARTLARA** çubuğu, yazılan komutu ekranda açık olan tüm kartlara aynı anda gönderir (**Ctrl+G** ile odaklanır).

- Yalnız Telnet sekmesinde açık kartlar komutu alır; listede olup ekranda olmayan kartlar almaz.
- Her kart komutu **tam bir kez** alır.
- Bağlı olmayan açık kartlar atlanır ve sonuçta raporlanır: `⚠ 3/4 karta gönderildi · gönderilemedi: 192.168.1.12 (bağlı değil)`.
- Bir kartın hatası diğer kartlara gönderimi durdurmaz.
- Komut geçmişi kartlarla ortaktır ve kalıcıdır (↑ / ↓, otomatik tamamlama, saat simgesi ya da Ctrl+R ile geçmiş penceresi). Bu çubuk Kart Dosya Gezgini sekmesinde görünmez.

Gönder düğmesinin yanında, kart penceresindekilerin genel karşılıkları vardır:

| Düğme | İşlev |
|---|---|
| **Hızlı komut** (şimşek) | Genel hızlı komutu ekrandaki tüm bağlı kartlara birer kez gönderir. İlk tıklamada komut sorulur; sağ tık: düzenle / kaldır. Kaydedilir. |
| **reboot ⇄** | Sol: seçili `reboot` / `reboot 2` komutunu ekrandaki tüm bağlı kartlara birer kez gönderir. Sağ ⇄: yalnız seçimi değiştirir (göndermez). Gönderdikten sonra 4 sn pasif. |
| **Dosyaya kaydet** (disket) | Ekrandaki **her** kartın terminal içeriğini o anda alır ve **her kartı kendi dosyasına** kaydeder (aşağıya bakın). |
| **Tümünü temizle** (silgi) | Ekrandaki **her** kartın terminal ekranını ve geçmişini bir kerede temizler (kart başlığındaki silgiyle aynı iş; karta komut gönderilmez). |

Sonuç her zaman çubuğun sağında yazar; kaydetmede ayrıntı (hangi kart hangi dosyaya) ipucundadır.

## Terminal ayarları

Anahtarlar, satır sonu, satır sınırı ve yazı boyutu tüm kartlar için ortaktır, anında geçerli olur ve kaydedilir. Hiçbiri karta gönderilen komutu ya da bağlantıyı etkilemez; yalnız ekranda ne gördüğünüzü ve neyin kaydedildiğini değiştirir.

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
- **Dosyaya kaydet** terminalde o an tutulan satırları (en çok bu sınır kadar) kaydeder.

### Yerel eko (varsayılan: açık)

Yazdığınız komutu, kart cevap vermeden önce kartın ekranına yazar; komut istemin yanına düşer (`-> ls "/ata0a"`). Kart da komutu geri ekolarsa ikinci kopya otomatik ayıklanır. Kapalıyken ekranda yalnız kartın gönderdikleri görünür. Komutlar ekranda iki kez görünüyorsa kapatın.

### Zaman damgası (varsayılan: kapalı)

Ekrana düşen her yeni satırın başına `[14:32:05.123]` biçiminde saat ekler. Saat, satırın bilgisayara ulaştığı andır (kartın saati değil). Yalnız açıldıktan sonra gelen satırlara eklenir; anlık kayıtta da görünür.

### Dönüş değerlerini ayır (varsayılan: açık)

Komut dönüş değerlerini canlı loglardan ayırır ve kartın **Dönüş** panelinde gösterir (yukarıda "Komut dönüş değerleri"). Kapalıyken dönüş değerleri eskisi gibi terminalde görünür.

### Yazı (ortak yakınlaştırma, varsayılan: %100)

Ayar çubuğundaki **− %100 +** tüm kart terminallerinin yazı boyutunu **birlikte** değiştirir (%60 – %250). Yüzdeye tıklamak varsayılana (%100) döndürür.

- Yeni açılan kartlar da aynı boyutla açılır; tercih kaydedilir.
- Bir kartın terminalinde **Ctrl + fare tekerleği** artık yalnız o kartı değil, **tüm kartları** birlikte büyütür / küçültür.
- Klavye (Telnet sekmesinde): **Ctrl + +** büyüt, **Ctrl + −** küçült, **Ctrl + 0** %100.
- Kaydırma konumu, seçim ve renkli uyarı satırları korunur; en alttaysanız akışı izlemeye devam edersiniz.

### Dosyaya kaydet (anlık görüntü)

Önceki sürümlerdeki sürekli kayıt anahtarı yerine artık **tıklanan bir kayıt düğmesi** vardır. Sürekli kayıt (açık kaldıkça her satırı yazma) yoktur; eski ayar dosyasında açık kalmışsa açılışta kapatılır.

- **Kart başlığındaki disket**: o kartın terminalinde **tıklandığı anda** görünen içeriğin tamamını (satır sınırında tutulan tüm satırlar) bir dosyaya yazar.
- **Genel komut çubuğundaki disket**: ekrandaki **her kartın** içeriğini aynı anda alır ve **her kartı kendi dosyasına** kaydeder (çıktılar tek dosyada birleştirilmez). Ekranda olmayan kartlar kaydedilmez. Bir kartın kaydı başarısız olsa da diğerleri kaydedilir; sonuç kart kart raporlanır. Boş terminali olan kart "içerik yok" olarak raporlanır.
- Dosya: exe'nin yanındaki `logs` klasörü, ad: `GrupAdı_IP[_KartAdı]_anlik_yyyyMMdd_HHmmss_fff.log` (UTF-8). Her tıklama **yeni dosya** oluşturur; var olan dosyanın üzerine yazılmaz.
- Kaydetme arka planda yapılır: canlı çıktı, komut gönderme ve kaydırma kesilmez. Kartın üstünde yeşil (başarılı) ya da kırmızı (hata) bildirim çıkar; bildirime tıklamak dosyanın klasörünü açar.
- Dönüş değerleri ayrı paneldedir; kayıt **terminalde görüneni** içerir.

## Kart Dosya Gezgini

**Kart Dosya Gezgini** sekmesi (**Ctrl+2**), kartın cihazlarını, klasörlerini ve dosyalarını `devs` ve `ls` komutlarını elle yazmadan gezmenizi sağlar. Karta sağ tıklayıp **Dosya Gezgininde aç** ile de açılır.

1. Üstten **kartı seçin**. Kart bağlıysa `devs` çalışır ve cihazlar **hemen** listelenir. Yalnız `devs` çıktısındaki **name** sütunu kullanılır (`drv refs name` ve `drv name` biçimleri desteklenir).
   - `devs`in listelediği **tüm cihazlar** görünür; cihazlar önceden tek tek denenmez, bu yüzden liste beklemeden gelir.
   - İçine girilip girilemeyeceği, cihaza **girmek istediğinizde** anlaşılır: `ll "cihaz"` dönüş değeri **0** ise içerik açılır. 0 dışındaysa (ör. `/null`, `/tyCo/0`) sarı uyarı şeridinde *"/tyCo/0" cihazının içine girilemiyor (dönüş değeri -1)* yazar, liste değişmez ve cihaz soluk görünür; türü **Cihaz · girilemiyor (-1)** olur.
   - İşaret, Cihazlar'dayken **Yenile** (F5) ya da kart değişince sıfırlanır; cihaz sonradan açılırsa işaret kendiliğinden kalkar.
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
- **`EFI` korumalıdır:** Adı `EFI` olan dosya ya da klasör (büyük / küçük harf fark etmez) kartın çalışması (açılışı) için gereklidir; **silinemez, taşınamaz, yeniden adlandırılamaz**. İçindeki öğeler de korunur. İçinde `EFI` bulunan bir klasör de içindekilerle birlikte silinemez ve taşınamaz (Vision silmeden / taşımadan önce klasörün içini tarar). Menüde bu işlemler pasif görünür; Delete ya da F2'ye basılırsa karta hiçbir komut gönderilmez, uyarı şeritte gösterilir. Kopyalamak serbesttir.
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
| Girilmiş | Kartta `copy "dosya", "<kartın FTP yolu>/Vision/_tkt_<rastgele>_dosya"` ile FTP'deki Vision klasörüne geçici bir kopya alınır, FTP'den bilgisayara indirilir, geçici kopya **her zaman** silinir. | Her tür dosya, bayt bayt aynı (ikili dosyalar dahil). |
| Girilmemiş | Sorulur: **FTP ayarları…** ya da **Metin olarak aç** (içerik `copy "dosya"` ile telnet üzerinden okunur). | Yalnız metin dosyaları (.txt, .xml, .cfg…). İkili dosyalar bu yolla bozulur. |

- Kopyalar `%TEMP%\TelnetCards\SaltOkunur\<oturum>\<kart IP>\<n>\<özgün dosya adı>` içinde tutulur; dosya adı değişmez (uygulama uzantıya göre seçilir). Aynı dosyayı tekrar açmak yeni bir kopya oluşturur, öncekini değiştirmez.
- Kopyalar uygulama kapanırken silinir; o sırada bir düzenleyicide açık olanlar bir sonraki açılışta temizlenir.
- 50 MB'tan büyük dosyalar (metin olarak açmada 1 MB) için önce onay istenir.
- Karttaki dosya ve klasör listesi bu işlemde **değişmez**.

### Bilgisayardan karta yükleme

Yükleme, kartlara dosya atarken elle izlediğiniz yolun aynısını otomatik yapar:

1. Dosya (ya da klasör) **kartın erişebildiği FTP sunucusuna**, FTP klasörünün içindeki **`Vision`** çalışma klasörüne gönderilir — sunucu, kullanıcı ve parola **o kartın kendisinden** alınır (aşağıya bakın). `Vision` klasörü yoksa ilk aktarımda otomatik oluşturulur.
2. Kartta `copy "FTP-konumu/Vision/dosya", "hedef/dosya"` çalıştırılır. Klasörlerde `xcopy` **kullanılmaz** (FTP üzerinden `xcopy` kartın telnet bağlantısını düşürebiliyordu): kartta önce klasör ve alt klasörler `mkdir` ile açılır, sonra her dosya ayrı `copy` ile kopyalanır. İlerleme "dosya 3/12" gibi gösterilir; bir dosya kopyalanamazsa diğerleri devam eder ve sonunda hangilerinin kopyalanamadığı yazılır. Klasörün içindeki öğelerden birinin adında Türkçe / özel karakter varsa yükleme başlamadan bildirilir.
3. Dönüş değeri kontrol edilir.

Yüklemek için: Windows Gezgini'nden dosyaları listeye **sürükleyip bırakın** (bir klasörün üzerine bırakırsanız o klasöre; boş bir klasöre de bırakılabilir) ya da **Yükle** (yukarı ok simgesi) düğmesini kullanın. Hedefte aynı adlı öğe varsa üzerine yazmadan önce sorulur.

- Hata olursa hangi aşamada olduğu, nedeni, **FTP sunucusunun cevabı** ve adresi birlikte yazılır. Örnek: *imaj.bin: FTP'ye gönderilemedi (FTP klasörüne yazılamadı: kullanıcının bu klasörde yazma izni yok ya da klasör yok — sunucu: 550 Permission denied · ftp://192.168.1.100:21/upload/imaj.bin)*.
- Adında Türkçe ya da özel karakter (ç, ş, ğ, ü, ö, ı, İ, çift tırnak) olan dosyalar FTP'ye hiç gönderilmeden açıklamayla reddedilir: kartın kabuğu bu adları doğru yazamaz. Dosyayı İngilizce karakterlerle yeniden adlandırın. Boşluk, `#` ve `%` sorun değildir.
- Pasif kipte FTP veri bağlantısı kurulamazsa gönderim bir kez etkin kiple denenir ve bu bilgi sonuçta yazar.

### FTP kopyalarını sil

Yüklenen her dosya önce FTP sunucusundaki **`Vision`** klasörüne kopyalanır. Kopyalama başarılı olunca FTP kopyası silinir ("kopyalama sonrası hemen sil" açıksa — varsayılan); aktarım **başarısız olursa** kopya FTP'de kalır. Bu kopyalar her zaman `Vision` klasöründe durduğu için FTP sunucusunda zaten bulunan dosya ve klasörlerle **karışmaz**; isterseniz FTP'de `Vision` klasörünü tümüyle silebilirsiniz (bir sonraki aktarımda yeniden oluşturulur).

- Bir karta yükleme **başlar başlamaz** Dosya Gezgini'ndeki **FTP kopyalarını sil** düğmesi (çöp kutusu) etkinleşir; yükleme başarılı da olsa başarısız da olsa (yarım dosya kalmış olabilir) etkin kalır.
- Tıklayınca onay istenir ve **yalnız bu kartın** FTP'de bıraktığı kopyalar silinir; karttaki dosyalara dokunulmaz. Silinince düğme pasifleşir. Zaten silinmiş kopyalar sorun çıkarmaz.
- Düğmenin ipucu silinecek kopyaları listeler. Her kartın listesi ayrıdır.

### FTP ayarları (kart başına otomatik)

FTP sunucusu, kullanıcı ve parola **elle girilmez**. Her kart için, o kartın kendisinden `version` komutuyla alınır. Komutun cevabındaki `Boot line:` (`BootLine:`) satırının altında kartın boot satırı bulunur:

```
Boot line:
fei(0,0)host:/vxWorks e=192.168.1.10:ffffff00 h=192.168.1.100 u=ftpuser pw=parola f=0x8
```

| Boot satırındaki değer | Kullanım |
|---|---|
| `h=` | FTP sunucusu (IP) |
| `u=` | FTP kullanıcı adı |
| `pw=` | FTP parolası |
| `gei(0,0)host:` (satır başı) | Kartın aygıt adı: parantez kapandıktan sonra `:` işaretine kadar olan kelime (`:` hariç). Kartın FTP yolu `/host.` + bu ad olur: `gei(0,0)host:` → `/host.host`, `gei(1,0)ali:` → `/host.ali`. |

- Bilgi, kart bağlıyken ilk FTP işleminde (yükleme, "Birlikte aç", bağlantı sınaması) otomatik alınır ve **yalnız o kart için, yalnız bellekte** tutulur. Başka kartın bilgisi asla kullanılmaz. Bağlantı kopunca / kesilince silinir; yeniden bağlanınca tekrar alınır.
- Parola ekranda, ayar dosyasında ve log'larda gösterilmez; Dosya Gezgini'nin **Ham cevap** panelinde `••••••` olarak görünür. Eski sürümlerde elle girilmiş parola ayar dosyasından silinir.
- Yalnız `Boot line:` işaretinden sonraki boot satırı okunur; `version` çıktısının diğer satırları (sürüm, `value = …`) karışmaz. Değer `=` işaretinden sonraki boşluğa kadar olan kısımdır (değerin içindeki `:` / `=` bölünmez). `version` etkileşimli değildir; karta tek komut gider ve hiçbir boot parametresi değiştirilmez.
- Bilgi alınamazsa nedeni yazılır: komut kartın imajında yoksa turuncu uyarı; `h` / `u` / `pw` eksikse hangisinin eksik olduğu (parola boşsa kart dosyalara FTP yerine RSH ile erişiyor olabilir); adres geçersizse adres.

**FTP** (dişli simgesi) penceresi:

| Bölüm | Açıklama |
|---|---|
| Bu kartın FTP bilgisi | Karttan alınan sunucu, kullanıcı, kartın FTP yolu (`/host.…`) ve aygıtı (parola gösterilmez). **Karttan yeniden al** ile tazelenir. |
| FTP klasörü | Dosyaların gönderileceği klasör, FTP oturumunun açıldığı klasöre göre (ör. `upload`; boş = kök). Tüm kartlar için ortak. |
| Kartın aynı klasörü gördüğü yol | **Karttan otomatik dolar ve kaydedilmez:** `/host.` + kartın aygıt adı + FTP klasörü. Aygıt adı kartın `version` çıktısındaki boot satırından alınır (parantezden sonra `:` işaretine kadar): `motetsec(0,0)mkozdem:vxWorks` → `/host.mkozdem`; FTP klasörü `upload` ise `/host.mkozdem/upload`. Her kart kendi yolunu kullanır. Gerekirse elle değiştirebilirsiniz; değişiklik **yalnız o kart ve o bağlantı** için geçerlidir (`host:` gibi `:` ile biten bir aygıt yazılırsa eski biçim `host:upload` kullanılır). **Karttan yeniden al**, bağlantının kopması ya da uygulamanın yeniden açılması yolu karttan gelen değere döndürür. Eşleme satırı (FTP'deki `…/Vision` ↔ kartın gördüğü `…/Vision`) sonucu gösterir. |
| Port, Pasif kip | Tüm kartlar için ortak. |
| Kopyalama sonrası hemen sil | Açıksa kartta kopyalama başarılı olunca FTP kopyası hemen silinir (varsayılan: **açık**; önceki sürümden gelen ayarlarda da ilk açılışta bir kez açılır, sonra seçiminiz korunur). |

**Bağlantıyı sına** yalnız bağlanmayı değil, yüklemenin gerçekten çalışacağını **aşama aşama** sınar:

1. FTP oturumu, `Vision` çalışma klasörünün oluşturulması (varsa dokunulmaz) ve listelenmesi
2. `Vision` klasörüne yazma (küçük bir deneme dosyası)
3. **Kartın** aynı dosyayı görmesi (kart, `…/Vision` klasörünü listeler)
4. Deneme dosyasının silinmesi

Bağlantı başarılı olsa da yazma izni yoksa ya da kartın gördüğü yol FTP klasörüyle aynı yer değilse hangi aşamada ve neden başarısız olduğu yazılır.

> Dosya Gezgini ayrı bağlantı açmaz; kartın mevcut bağlantısını kullanır. Kart bağlı değilse bu belirtilir ve **Bağlantı Kur** düğmesi gösterilir. Gezginin komutları ve cevapları terminale yazılmaz; terminal komutlarıyla sırayla çalışır, cevaplar birbirine karışmaz.

## Komut Kütüphanesi

**Komut Kütüphanesi** sekmesi (**Ctrl+3**), VxWorks 6.4 kart komutlarının açıklamalarını, yazılışlarını ve örnek çıktılarını tek yerde toplar. Kütüphane **Vision'a gömülüdür ve salt okunurdur**: uygulamadan komut ya da modül eklenemez, düzenlenemez, silinemez, taşınamaz; exe'nin yanında ayrı bir komut dosyası da yoktur. Yeni komutlar ve düzeltmeler Vision'ın sonraki sürümleriyle gelir.

### Komutlar

Vision, VxWorks 6.4 kabuğunun (C yorumlayıcısı, `->` istemi) sık kullanılan **113 komutuyla** birlikte gelir. Her komutta ne işe yaradığı, yazılışı / örnek kullanımı ve varsa kartın verdiği örnek çıktı vardır. Komutlar 12 modülde toplanmıştır:

| Modül | Komutlar |
|---|---|
| Kabuk kullanımı | `help`, `h`, `printf`, değişken tanımlama, `lkup`, `lkAddr`, `cmd`, `logout`, `dbgHelp`, `ioHelp`, `netHelp`, `spyHelp`, `rtpHelp`, `timexHelp`, `<`, `>` |
| Sistem bilgisi | `version`, `sysModel`, `sysClkRateGet`, `sysClkRateSet`, `tickGet`, `spy`, `spyStop` |
| Görevler (task) | `i`, `ti`, `sp`, `taskSpawn`, `td`, `ts`, `tr`, `tt`, `taskPrioritySet`, `checkStack`, `period`, `repeat`, `taskShow`, `w` |
| Bellek | `memShow`, `d`, `m`, `l`, `memPartShow`, `adrSpaceShow`, `vmContextShow` |
| Modüller ve semboller | `moduleShow`, `ld`, `unld`, `printErrno`, `unldByModuleId` |
| Boot / BSP ayarları | `bspBootChange`, `bootChange`, `reboot`, `reboot 2`, `bootParamsShow`, `FPGAGiveBoardReset` |
| Dosya sistemi | `devs`, `ls`, `ll`, `cd`, `pwd`, `copy`, `xcopy`, `mv`, `rm`, `mkdir`, `rmdir`, `xdelete`, `iosFdShow`, `llr`, `rename`, `cp`, `dosFsShow` |
| Ağ | `ifShow`, `inetstatShow`, `ping`, `routeShow`, `routeAdd`, `arpShow`, `hostShow`, `hostAdd`, `tcpstatShow`, `netStackSysPoolShow`, `ifconfig`, `netstat`, `netstat "-a"`, `sockShow`, `ipcom_socketclose`, `sysvar list`, `syslog list` |
| Senkronizasyon nesneleri | `semShow`, `msgQShow`, `show` |
| Hata ayıklama | `b`, `bd`, `c`, `s`, `edrShow`, `debugAgentStart` |
| Süreçler (RTP) | `rtpShow`, `rtpi`, `rtpSp`, `rtpSpawn` |
| Zamanlayıcılar ve watchdog | `timer_create`, `timer_connect`, `timer_settime`, `timer_gettime`, `timer_cancel`, `timer_delete`, `wdCreate`, `wdStart`, `wdCancel`, `wdDelete`, `wdShow` |

- Örnek çıktılar temsilîdir: adresler, görev kimlikleri ve satırlar kartın BSP'sine ve imajına göre değişir. Bir komut kartın imajında yoksa kart `undefined symbol` der; `lkup "ad"` ile kartta olup olmadığına bakabilirsiniz.
- `FPGAGiveBoardReset` ve `debugAgentStart` standart VxWorks komutu değildir (karta / BSP'ye özel); açıklamaları sonraki sürümlerde netleştirilecektir. `sysvar list` ve `syslog list` IPCOM komutlarıdır ve `cmd` ile geçilen komut yorumlayıcısında yazılır.

### Karta aktar

Bir komutu kütüphaneden alıp doğrudan bir kartta denemek için:

1. Komutu seçin; **YAZILIŞ / ÖRNEK KULLANIM** başlığının sağındaki **Karta aktar**'a tıklayın (ya da soldaki listede komuta sağ tık → **Karta aktar**).
2. Açılan listeden kartı seçin. Kartlar gruplarıyla ve bağlantı durumlarını gösteren renkli noktayla listelenir; en altta **Ekrandaki tüm kartlar** seçeneği komutu genel komut çubuğuna yazar.
3. Vision Telnet sekmesine geçer, kartı açar (açıksa öne getirir) ve komutu kartın komut satırına **yazar — göndermez**. Argümanları (ör. dosya yolu, görev adı) düzenleyip **Enter** ile gönderin.

- Yazılış birden çok satırsa (birden çok örnek), aktarmadan önce yazılış kutusunda istediğiniz satırı **seçin**; seçmezseniz ilk satır aktarılır. Satır sonundaki ` // açıklama` kısmı aktarılmaz.
- Kart bağlı değilse önce bağlanılır; bağlantı kurulunca komut yazılır. Bağlanılamazsa kartta uyarı çıkar ve komut yazılmaz.

### Ekran

| Bölüm | Ne gösterir |
|---|---|
| Üst çubuk | Toplam komut ve modül sayısı. |
| Sol liste | Arama kutusu ve modül ağacı. Her modül başlığında komut sayısı yazar; başlığın solundaki ok ile modül daraltılır / genişletilir. Alttaki iki simge tüm modülleri genişletir / daraltır. |
| Komut sayfası | Komutun adı, bulunduğu modül (tıklayınca modülün sayfasına gider), açıklama, yazılış (**Karta aktar** ile bir kartın komut satırına, **Kopyala** ile panoya) ve örnek çıktı. |
| Modül sayfası | Bir modül başlığı seçilince açılır: modülün açıklaması, komut sayısı ve komutlarının listesi (ad, yazılış, özet). Listeden bir komuta tıklamak o komutu açar. |

### Arama ve gezinme

- **Arama:** Sol üstteki kutu (Ctrl+F) ad, açıklama, yazılış, örnek çıktı **ve modül adında** arar; Türkçe büyük / küçük harf duyarsızdır. Sonuçlar modüllerine göre gruplanır; adı aramayla başlayan komutlar önce gelir. Modül adını yazmak (ör. `ağ`) o modülün tüm komutlarını getirir. Esc aramayı temizler.
- **Klavye:** ↑ / ↓ listede gezinir; modül başlığında ← / → daraltır / genişletir, Enter açıp kapatır; komut üzerinde ← modül başlığına çıkar.
- **Sağ tık:** komutta **Yazılışı kopyala** ve **Karta aktar**; modül başlığında **Tümünü genişlet / daralt**.

### Kütüphaneyi güncelleme (geliştirici için)

Komutlar projedeki `Komutlar\Komutlar.xml` dosyasındadır ve derlemede exe'ye gömülür. Komut eklemek ya da düzeltmek için bu dosya düzenlenip yeni sürüm derlenir. Biçim:

```
<KomutKutuphanesi Surum="2">
  <Modul Ad="Görevler (task)">
    <Aciklama>Görevleri listeleme, durdurma ve başlatma.</Aciklama>
    <Komut Ad="i">
      <Aciklama>Sistemdeki görevleri özetler.</Aciklama>
      <Yazilis>i
i "tShell0"</Yazilis>
      <OrnekCikti>  NAME   ENTRY   TID ...</OrnekCikti>
    </Komut>
  </Modul>
</KomutKutuphanesi>
```

Yazılışta her satır ayrı bir örnektir; satır sonundaki ` // açıklama` kısmı karta aktarılmaz.

## Bağlantı izleme ve otomatik yeniden bağlanma

Uygulama, bağlı kartların ulaşılabilirliğini komut beklemeden sürekli denetler. Denetim **TCP keepalive** ile mevcut bağlantı üzerinden yapılır: işletim sistemi boştaki bağlantıya küçük yoklama paketleri gönderir; bunları kartın kabuğu değil TCP yığını cevaplar. Terminale hiçbir şey yazılmaz, komutlarla çakışmaz, ek bağlantı açılmaz.

- **Algılama:** Varsayılan ayarlarla kart kapandığında ya da kablosu çekildiğinde kopma yaklaşık **6–9 saniyede** algılanır. Tek bir kayıp yoklama kopma sayılmaz; 3 ardışık cevapsız yoklama gerekir. Birkaç saniyelik geçici kesintiler uyarı üretmez.
- **Yeniden bağlanma:** Kopan kart, ulaşılabilir olunca aynı oturum üzerinden kendiliğinden yeniden bağlanır. Denemeler 3, 6, 12, 15, 15… saniye aralıklarla yapılır; başarısız denemeler sessizdir (tekrar uyarı yazılmaz).
- **Durdurma:** Kopmuş kartta **Bağlantıyı Kes** derseniz otomatik yeniden bağlanma durur.
- **Kapsam:** Hiç bağlanmadığınız kartlara izleme amacıyla bağlantı açılmaz.

### İzleme ayarları (isteğe bağlı)

Arayüzde ayar yoktur; değerler `%APPDATA%\TelnetCards\config.xml` dosyasındaki `<Monitor>` bölümünden değiştirilebilir (uygulama kapalıyken düzenleyin):

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
| F1 | Kullanım Kılavuzu (ayrıca sekme şeridinin sağındaki **Yardım** düğmesi) |
| Ctrl+Shift+L | Sonraki tema |
| Enter / F2 / Ctrl+D / Esc | Boot ayarları penceresinde: gönder (boşsa değiştirmeden geç) / kartın değerini düzenle / çık / kapat |
| Ctrl+S / Esc | Komut Kütüphanesi'nde kaydet / iptal |
| Ctrl+F | Komut Kütüphanesi'nde ara |
| Enter | Komut gönder · Kart Gezgini'nde seçili kartı göster · Dosya Gezgini'nde aç |
| ↑ / ↓ | Komut geçmişi |
| Ctrl+R | Komut satırında: komut geçmişi penceresi ve geriye doğru arama · pencerede: sonraki eşleşme |
| Ctrl+C | Karta kesme (ETX) gönder (metin seçili değilken) |
| Delete | Kart Gezgini'nde seçili kartı / grubu sil |
| Backspace / F5 | Dosya Gezgini'nde Geri / Yenile |
| F2 / Delete | Dosya Gezgini'nde yeniden adlandır / sil |
| Ctrl+Shift+N | Dosya Gezgini'nde yeni klasör |
| Ctrl + sürükle | Dosya Gezgini'nde taşımak yerine kopyala |
| Ctrl+Enter / Ctrl+Shift+Enter | Dosya Gezgini'nde varsayılan uygulamayla / "Birlikte aç" ile aç (salt okunur kopya) |
| Ctrl + + / Ctrl + − / Ctrl + 0 | Tüm kart terminallerinde yazıyı büyüt / küçült / %100 (Telnet sekmesi) |
| Ctrl + fare tekerleği | Terminal üzerinde: tüm kartların yazısını birlikte büyüt / küçült |
| Esc | Açık pencereyi (Kart Ekle, Yardım) kapat |

## Dosyalar ve klasörler

| Konum | İçerik |
|---|---|
| `%APPDATA%\TelnetCards\config.xml` | Gruplar, kartlar (adları, hızlı komutları, reboot seçimleri), terminal, yakınlaştırma, izleme ve ortak FTP ayarları (otomatik kaydedilir; FTP parolası dosyada tutulmaz). |
| `%APPDATA%\TelnetCards\KomutGecmisi.txt` | Komut geçmişi: yazılan tüm komutlar, satır başına bir komut, eskiden yeniye (en fazla 5.000; tüm kartlar ve genel çubuk ortak; otomatik kaydedilir). Silinirse geçmiş boş başlar. |
| `logs\GrupAdı_IP_anlik_tarih_saat.log` | "Dosyaya kaydet" ile alınan anlık terminal görüntüleri (her tıklama ayrı dosya). |
| `logs\hatalar.log` | Bağlantı hatalarının teknik ayrıntıları (hata kodu, özgün İngilizce mesaj). Her zaman açıktır. |
| `Yardim\` | Bu kılavuz ve gereksinimler belgesi. |
| `%TEMP%\TelnetCards\SaltOkunur\` | "Birlikte aç" ile açılan salt okunur kopyalar (kapanışta silinir). |

`config.xml` ve `%TEMP%` dışındaki klasörler exe'nin yanındadır (Visual Studio'dan çalıştırırken `bin\Debug\`).

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
| Yükleme: "FTP'ye gönderilemedi … 550" | FTP kullanıcısının o klasöre **yazma** izni yok ya da klasör yok. FTP sunucusunda (ör. FileZilla Server) kullanıcının klasör izinlerine **Write** ekleyin. **Bağlantıyı sına** bunu 2. aşamada gösterir. |
| Yükleme: "FTP'ye gönderildi ama kartta kopyalanamadı" | Kart dosyayı aynı yerde göremiyor. **Bağlantıyı sına**'nın 3. aşamasına bakın. "Kartın aynı klasörü gördüğü yol" elle değiştirildiyse **Karttan yeniden al** ile karttan gelen yola döndürün; kartta `devs` ile FTP aygıtının adını kontrol edin. |
| "Kartın FTP bilgisi alınamadı" | Ayrıntı uyarı şeridinde. Kartta `version` yazıp `Boot line:` altındaki satırda `h=`, `u=`, `pw=` değerlerinin olduğunu kontrol edin. |
| Sürükle-bırak hiç kabul edilmiyor | Vision yönetici olarak, Windows Gezgini normal çalışıyorsa Windows iki program arasında sürüklemeye izin vermez. İkisini de aynı yetkiyle çalıştırın ya da **Yükle** düğmesini kullanın. |

## Belgeleri güncelleme

Bu kılavuz ve gereksinimler belgesi Markdown biçimindedir. Proje içinde `Yardim\` klasöründedir; derlemede exe'nin yanına kopyalanır ve exe'nin içine de yedek olarak gömülür. Yardım penceresindeki **Yardım klasörünü aç** düğmesiyle dosyalara ulaşabilirsiniz. Yeni özellik eklendikçe ilgili bölüm güncellenmelidir.
