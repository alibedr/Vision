# Gereksinimler

Bu belge uygulamanın çalışması için gerekenleri (sistem gereksinimleri) ve uygulamadan istenen özellikleri (işlevsel gereksinimler) bir arada tutar. Her yeni istek bu belgeye eklenmeli ve durumu güncellenmelidir.

> Durum gösterimi: **Karşılandı** — uygulandı ve test edildi · **Kısmen** — uygulandı, gerçek donanımda doğrulanması gerekiyor · **Açık** — henüz uygulanmadı.

## Sistem gereksinimleri

### Çalıştırma

| Gereksinim | Açıklama |
|---|---|
| İşletim sistemi | Windows 7 SP1 ve üzeri (Windows 10 / 11 önerilir). |
| .NET Framework | 4.6.1 veya üzeri (Windows 10 / 11 ile birlikte gelir). İnternet bağlantısı gerekmez. |
| Ağ | Bilgisayardan kartlara TCP erişimi (varsayılan telnet portu 23; grup başına ayarlanabilir). |
| Kart tarafı | Telnet sunucusu ve komut kabuğu. Dosya Gezgini için `devs` ve `ls` komutları ile `value = N = 0x…` biçiminde dönüş değeri basan (VxWorks benzeri) bir kabuk. |
| Bağlantı izleme | Kartın TCP yığını keepalive yoklamalarını cevaplamalıdır (standart TCP davranışı). Yoklama sayısının 3'e ayarlanması Windows 10 sürüm 1703 ve sonrası gerektirir; daha eskilerde algılama ~20 sn sürer. |
| Disk | Exe'nin bulunduğu klasörde yazma izni (`Komutlar`, `logs`); ayarlar için `%APPDATA%\TelnetCards` klasörüne yazma izni. |
| Dosya işlemleri | Kartın imajında `ll`, `copy`, `mkdir`, `rmdir`, `rm`, `xdelete`, `mv`, `xcopy` (eksik olanlar için uyarı verilir). |
| Karta yükleme | Bilgisayardan ve karttan erişilebilen bir FTP sunucusu (kartın `host:` gibi bir yolla gördüğü klasör). |

### Geliştirme

| Gereksinim | Açıklama |
|---|---|
| IDE | Visual Studio 2019 (16.11) veya üzeri; ".NET masaüstü geliştirme" iş yükü. |
| Dil | C# 7.3 (VS 2019 / .NET Framework projelerinin varsayılanı). |
| Hedef | .NET Framework 4.6.1, Windows Forms, klasik (SDK olmayan) `.csproj`. Tek NuGet paketi: `Microsoft.Web.WebView2` (`packages.config`; Sentinel sayfası). |
| Bağımlılık | Yok. NuGet paketi kullanılmaz; kapalı ağda derlenebilir. |
| Açma | `Vision.sln` → F5. |

## İşlevsel gereksinimler

### G1 — Temel telnet arayüzü

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G1.1 | Kartlara telnet ile bağlanmak | Karşılandı | Asenkron TCP istemcisi; telnet seçenek pazarlığı (IAC) otomatik reddedilir, kart düz metin kipinde konuşur. |
| G1.2 | Kartlara komut yazmak, gelenleri ekrana basmak | Karşılandı | Kart başına terminal; çıktı 40 ms'de bir toplu basılır, yoğun akışta arayüz donmaz. |
| G1.3 | Gerçek zamanlı kullanım için hızlı olması | Karşılandı | C#; TCP_NODELAY açık (kısa komutlar beklemeden gider), okuma ayrı iş parçacığında. |
| G1.4 | Kapalı ağda VS 2019 ve .NET 4.6.1 ile derlenebilmesi | Karşılandı | C# 7.3, klasik proje, harici bağımlılık yok. |

### G2 — Arayüz modernizasyonu ve çoklu kart yönetimi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G2.1 | Mevcut bağlantı, veri akışı ve komut gönderme korunmalı | Karşılandı | Haberleşme katmanı yalnız eklemelerle genişletildi; mevcut davranış değişmedi. |
| G2.2 | Modern, profesyonel, koyu tema | Karşılandı | Tutarlı renk / yazı tipi / boşluk; koyu başlık çubuğu ve kaydırma çubukları. |
| G2.3 | Sekme yerine "Kart Ekle" penceresi: grup adı, port, IP, kart sayısı | Karşılandı | Kart sayısı kadar IP alanı dinamik oluşur; başlangıç IP'sinden sıralı doldurulur. |
| G2.4 | Girdi doğrulama | Karşılandı | Boş / geçersiz / tekrarlanan IP ve var olan grup adı engellenir; hatalı alan işaretlenir. |
| G2.5 | Solda grup → kart ağacı (açılır / kapanır) | Karşılandı | Gruba tıklama aç/kapa; arama kutusu; durum noktaları ve rozetler. |
| G2.6 | Karta çift tıklayınca kartı açmak | Karşılandı | Çift tık kartı Telnet sekmesinde gösterir. |
| G2.7 | Grubun tümünü / seçili kartları açmak | Karşılandı | Sağ tık menüsü ve kutucuklarla seçim. |
| G2.8 | Birden çok kartı aynı anda dinamik ızgarada göstermek | Karşılandı | Kart sayısı ve pencere oranına göre sütun sayısı; büyütme; kaydırma. |
| G2.9 | Kart görünümünü kapatınca yapılandırma silinmemeli | Karşılandı | ✕ yalnız görünümü kaldırır. |

### G3 — Bağlantı ve görünüm yönetiminin ayrılması

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G3.1 | "Bağlantı Kur" / "Bağlantıyı Kes" düğmesi; seçili kartların bağlantı durumunu yansıtmalı | Karşılandı | Hepsi bağlıysa "Bağlantıyı Kes", değilse "Bağlantı Kur" (yalnız bağlı olmayanlara bağlanır). |
| G3.2 | "Seçilenleri Göster": tam olarak seçilenler ekranda kalmalı | Karşılandı | Seçimden çıkanlar ekrandan kalkar, bağlı kalır. |
| G3.3 | Yapılandırma, bağlantı ve görünüm durumları bağımsız | Karşılandı | Görünüm açılınca otomatik bağlanma kaldırıldı; hiçbir görünüm işlemi bağlantıya dokunmaz. |
| G3.4 | Ekranda olup bağlı olmayan kart bunu açıkça göstermeli | Karşılandı | Durum şeridi ve gri görünüm. |

### G4 — Terminal geliştirmeleri

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G4.1 | Kart başına bağlantı süresi (SS:DD:ss) | Karşılandı | Gerçek bağlantı anından başlar; kopunca sıfırlanır, yeniden bağlanınca baştan başlar. |
| G4.2 | Yukarı kaydırınca konum korunmalı; "↓ Yeni Satırlar (N)" göstergesi | Karşılandı | Gösterge bir kez çıkar, yalnız sayaç artar; tıklayınca ya da elle en alta inince kapanır. |
| G4.3 | Enter'da çift satır sorunu (kök neden) | Karşılandı | Neden: yerel ekonun kendi satır sonu + kartın Enter ekosu ve durumsuz satır sonu işleme. Kartın ekosu ayıklanır, satır sonu varyantları tek satıra indirgenir. |
| G4.4 | Kart başına komut geçmişi (↑ / ↓) | Karşılandı | Ardışık tekrar saklanmaz; en yeninin ötesinde yazılan taslak geri gelir. |
| G4.5 | Tüm açık kartlara komut gönderen genel çubuk | Karşılandı | Yalnız ekrandaki kartlara; bir kartın hatası diğerlerini durdurmaz; sonuç raporlanır. |
| G4.6 | Genel komut çubuğu yalnız Telnet sekmesinde olmalı | Karşılandı | Çubuk Telnet sekmesinin içinde; Dosya Gezgini'nde görünmez. |
| G4.7 | Satır sonu seçenekleri anlaşılır / Türkçe; varsayılan belirtilmeli | Karşılandı | "CR+LF (varsayılan)"; açılan listede Türkçe açıklama ve ne zaman seçileceği. |

### G5 — Bağlantı durumunun görsel yansıması

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G5.1 | Bağlı kartın görünümü değişmemeli | Karşılandı | Renkler aynen korunur. |
| G5.2 | Bağlı olmayan kart gri tonda görünmeli | Karşılandı | Çıktı, başlık ve komut satırı gri tonlara geçer. |
| G5.3 | Bağlı olmayan kartın konsoluna yazılamamalı | Karşılandı | Komut satırı salt okunur; Enter / ↑ / ↓ / Ctrl+C çalışmaz. |
| G5.4 | Bağlantı aniden koparsa anında gri + kırmızı uyarı | Karşılandı | Kırmızı uyarı kutusu, kalın kırmızı konsol satırı, kırmızı kenarlık ve şerit. |

### G6 — Hata mesajları ve Dosya Gezgini

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G6.1 | Bağlantı hata mesajları Türkçe ("Bağlantı Kesildi") | Karşılandı | Hata kodları Türkçe mesajlara eşlenir; teknik ayrıntı `logs\hatalar.log` dosyasında kalır. |
| G6.2 | Kart Dosya Gezgini sekmesi; kart seçimi | Karşılandı | Kartın mevcut bağlantısını kullanır; ayrı bağlantı açmaz. |
| G6.3 | `devs` ile cihaz listesi (sabit kodlama yok) | Kısmen | `drv refs name` ve `drv name` biçimlerinden yalnız **name** sütunu alınır. Gerçek kart çıktısıyla doğrulanmalı. |
| G6.4 | Çift tıkla `ls "/yol"`; dönüş değeri 0 = başarı, 0 dışı = hata (liste değişmez); boş klasör hata değil | Kısmen | Dönüş değeri `value = N = 0x…` satırından metinden ayrı okunur. Gerçek kart çıktısıyla doğrulanmalı. |
| G6.5 | Breadcrumb, Geri, Yenile, ikonlar; yükleniyor / boş / hata durumları | Karşılandı | Ayrıca "Ham cevap" tanılama paneli. |
| G6.6 | Gezinme terminali ve diğer kartları etkilememeli | Karşılandı | Yakalanan cevaplar terminale yazılmaz; komutlar sırayla çalışır, karışan cevap terminale iade edilir. |

### G7 — Periyodik bağlantı izleme

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G7.1 | Komut çalıştırmadan kopmayı algılamak (kart kapanması / kablo çekilmesi) | Karşılandı | Mevcut sokette TCP keepalive; varsayılanlarla ~6–9 sn. |
| G7.2 | Hafif ve terminali etkilemeyen sağlık kontrolü; ek bağlantı yok | Karşılandı | Yoklamaları kartın TCP yığını cevaplar; kabuğa ulaşmaz. |
| G7.3 | Yanlış alarm koruması (ardışık hata eşiği, zaman aşımı) | Karşılandı | 3 ardışık cevapsız yoklama; geçici kesintiler uyarı üretmez. |
| G7.4 | Kart geri gelince otomatik "Bağlı" | Karşılandı | Aynı oturum üzerinden artan aralıklarla sessiz yeniden bağlanma. |
| G7.5 | Tekrarlanan uyarı yok; kartlar bağımsız; kaynaklar temizlenir | Karşılandı | Uyarı kopma başına bir kez; tek zamanlayıcı; kart silinince / uygulama kapanınca izleme durur. |
| G7.6 | Süreler sonradan mimari değişmeden ayarlanabilmeli | Karşılandı | `config.xml` → `<Monitor>`; arayüz ayarı eklenmedi. |

### G8 — Belgeler ve kimlik

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G8.1 | Geliştirici imzası (Ali Bedir) | Karşılandı | Pencere başlığı, durum çubuğu, Yardım → Hakkında ve exe "Özellikler → Ayrıntılar". |
| G8.2 | Yardım bölümü: kullanım kılavuzu ve gereksinimler | Karşılandı | F1; belgeler projede `Yardim\` klasöründe, exe'ye gömülü (G32). |
| G8.3 | Uygulama adı | Açık | Şimdilik "Vision". Ad `AppInfo.cs` içinden tek yerden değiştirilir. |

### G10 — Ayarların konumu, menü ve Komut Kütüphanesi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G10.1 | Ayarlar exe'nin yanındaki `Konfig` klasöründe | Değiştirildi (G18.3) | Ayarlar yeniden `%APPDATA%\TelnetCards\config.xml` içinde tutulur; eski `Konfig\config.xml` ilk açılışta oraya kopyalanır. |
| G10.2 | Üst menü çubuğunda "Yardım" (F1 korunur) | Karşılandı | Kullanım Kılavuzu, Gereksinimler, Yardım klasörünü aç, Hakkında. |
| G10.3 | Komut açıklamaları için yeni sekme | Karşılandı | "Komut Kütüphanesi" sekmesi: ad, açıklama, yazılış / örnek, örnek çıktı. |
| G10.4 | Komut ekleme, çıkarma ve sonradan düzenleme | Karşılandı | Ekle / Düzenle / Sil; benzersiz ad doğrulaması; kaydedilmemiş değişiklik uyarısı; arama. |
| G10.5 | Komut dosyası exe ile birlikte bir klasörde gelmeli | Karşılandı | `Komutlar\Komutlar.xml`; derlemede yalnız yoksa kopyalanır (eklenen komutların üzerine yazılmaz); `.yedek` sürüm. |

### G11 — Geri bildirime göre iyileştirmeler

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G11.1 | Bağlantı zaman aşımı yaklaşık iki katına çıkmalı; bir kart diğerlerini engellememeli | Karşılandı | 5 sn → 10 sn (`ConnectTimeoutMs`); hızlı kart beklenmez; kartlar aynı anda, birbirinden bağımsız bağlanır. Yavaş kart (~7 sn) testte bağlandı. |
| G11.2 | Kullanıcı ayarlı terminal satır sınırı (varsayılan 1000) | Karşılandı | Telnet sekmesinde "Satır sınırı"; en eski satırlar silinir; kaydırma konumu ve yeni satır göstergesi korunur; görünmeyen kartların geçmişi de sınırlı; değişiklik hemen uygulanır. |
| G11.3 | Komut geçmişinden otomatik tamamlama | Karşılandı | Mevcut kart geçmişi; soluk öneri; Tab / ↓ / → ile kabul (çalıştırmaz); Esc; tekrarlanan komut yinelenmez. Genel komut çubuğunda da var. |
| G11.4 | En fazla 12 görünür kart (sabit) | Karşılandı | Tüm açma yollarında; kalan yer kadar açılır, gerisi uyarıyla bildirilir; açık kartlar kapatılmaz. |
| G11.5 | Dosya Gezgini'ndeki kart Kart Gezgini'nde vurgulanmalı | Karşılandı | Ayrı vurgu (seçim ve bağlantı değişmez); kart değişince güncellenir. |
| G11.6 | Çalışma Alanı sekmesinin adı "Telnet" | Karşılandı | Kullanıcıya görünen tüm metinler ve belgeler; iç adlar aynı. |
| G11.7 | Kart Gezgini daraltılabilir olmalı | Karşılandı | Kenar çubuğu simgeli düğme (G12.2) ve Ctrl+B; durum korunur; tercih kaydedilir. |
| G11.8 | Kartlara isteğe bağlı görünen ad: "IP (Ad)" | Karşılandı | Ad ver / değiştir / kaldır (F2); her yerde IP ile birlikte; bağlantı her zaman IP ile; `config.xml`'de saklanır. |
| G11.9 | Yardım'da "Bu sayfada" vurgusu tıklama ve kaydırmayla eşzamanlı | Kısmen | Tıklanan bölüm korunur; kaydırmada etkin bölüm hesaplanır (sayfa başı / sonu ve yakın başlıklar dahil). Hesap test edildi; Windows'taki görünümü doğrulanmalı. |

### G12 — Birlikte aç, simgeli düğmeler ve kontrast

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G12.1 | Dosya Gezgini'nde "Birlikte aç"; yalnız okuma, kullanıcıya salt okunur olduğu belirtilmeli | Karşılandı | Sağ tık: "Varsayılan uygulamayla aç (salt okunur)" (Ctrl+Enter) ve "Birlikte aç… (salt okunur)" (Ctrl+Shift+Enter, Windows uygulama seçme penceresi). Kopya `%TEMP%\TelnetCards\SaltOkunur\…` altında, özgün adıyla, Windows "Salt okunur" özniteliğiyle. Kullanıcıya: ilk açılışta açıklama penceresi (bir daha gösterme seçeneği), her açılışta bilgi şeridi ("SALT OKUNUR KOPYA açıldı … değişiklikler karta yansımaz"), menü adlarında "(salt okunur)". FTP ayarlıysa kartta geçici adla FTP konumuna `copy` + FTP'den indirme + geçici kopyanın silinmesi (ikili dosyalar bozulmaz); değilse onayla metin olarak. Kapanışta / sonraki açılışta temizlik. Kart dosyası değişmez (testte doğrulandı). |
| G12.2 | Kart Gezgini'ni gizleme düğmesi daha sade ve modern | Karşılandı | ◀ / ▶ karakteri yerine çizilmiş "kenar çubuğu" simgesi (açıkken sol bölme dolu, kapalıyken boş); yalnız üzerine gelince hafif zemin; ipucu "Kart Gezgini'ni gizle / göster (Ctrl+B)". |
| G12.3 | Hepsine Gönder, Gönder, Bağlantı Kur / Kes, Temizle gibi düğmelerde yazı yerine simge | Karşılandı | Kodla çizilen, yazı tipinden bağımsız simgeler (her ölçekte keskin): Gönder (kâğıt uçak), Hepsine Gönder (yayın), Bağlantı Kur / Kes (fiş / üstü çizili fiş; yeşil / kırmızı), Temizle (silgi), Büyüt / Eski boyut / Kaldır, Dosya Gezgini'nde Geri, Yenile, Yeni klasör, Yükle, FTP ayarları. Kart penceresi başlığındaki düğmeler 28 px, Gönder 36 px (önceden 70–130 px yazılı düğmeler). Her simgenin ipucu ve ekran okuyucu adı tam işlevi söyler. Kart Gezgini'nin alt düğmesi geniş olduğu için simge + yazı. |
| G12.4 | Arayüz için kontrast testi; okunması zor yazıların düzeltilmesi | Karşılandı | 41 metin / zemin çifti WCAG 2.1 formülüyle ölçüldü (normal metin ≥ 4.5:1, pasif düğme ≥ 3:1). Önce 11 çift geçmiyordu, şimdi 41/41 geçiyor. Aşağıdaki tabloya bakın. |

**Kontrast ölçümü (önemli değişiklikler)**

| Metin / zemin (önceden geçmeyen 11 çift) | Önce | Sonra | Değişiklik |
|---|---|---|---|
| İpucu metni (süre, RX/TX, yol) / kart yüzeyi | 2.91 | 4.62 | `TextFaint` 96,104,117 → 128,137,151 |
| İpucu metni / pencere (boş ekran yazıları) | 3.30 | 5.26 | aynı |
| İpucu metni / kenar çubuğu (sayaçlar) | 3.13 | 4.98 | aynı |
| İpucu metni / çıktı alanı | 3.39 | 5.39 | aynı |
| Pasif düğme yazısı / yükseltilmiş yüzey (gerek ≥ 3) | 2.60 | 4.13 | aynı |
| İkincil metin / seçili satır | 3.41 | 4.52 | `TextMuted` 139,148,162 → 163,171,184 |
| Beyaz yazı / mavi düğme (Gönder, Bağlantı Kur…) | 3.55 | 5.17 | düğme zemini için ayrı `AccentFill` 37,99,235 |
| Beyaz yazı / mavi düğme (üzerine gelince) | 2.87 | 6.42 | `AccentFillHover` 29,84,212 (açılmak yerine koyulaşır) |
| Hata metni / tehlike düğmesi | 4.10 | 5.28 | `Danger` 232,86,86 → 248,113,113 |
| Hata metni / kırmızı şerit (Bağlantı yok) | 4.17 | 5.37 | aynı |
| Gri (bağlı değil) terminal metni | 4.02 | 5.19 | 118,124,134 → 137,143,153 |

Ana metin (≈ 14:1), terminal metni (≈ 13:1) ve başarı / uyarı renkleri zaten yeterliydi; değiştirilmedi. İkincil metin, bağlantı durumu noktası ve Yardım sayfası renkleri de aynı palete alınarak ayrıca güçlendirildi; kenarlık rengi bileşen sınırlarının seçilmesi için 48,54,65 → 58,65,78 yapıldı. Yeni simgeli düğmelerin tüm durumları da ölçüldü (ör. kırmızı "Bağlantıyı Kes" 5.4, üzerine gelince 4.9; yeşil fiş üzerine gelince 5.6).

### G13 — Komut Kütüphanesi modülleri

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G13.1 | Komut Kütüphanesi'nde modül oluşturabilme | Karşılandı | **Yeni modül** (Ctrl+Shift+M): ad + isteğe bağlı açıklama; benzersiz ad doğrulaması; düzenleme (ad değişince komutlar da taşınır) ve silme (komutları koruyarak ya da birlikte). |
| G13.2 | İstenen komutu istenen modülün altına atabilme | Karşılandı | Yeni komutta seçili modül; düzenlemede **MODÜL** listesi; soldaki listede sürükle-bırak; sağ tık → **Modüle taşı** (ya da yeni modüle). Modülsüz komutlar ayrı grupta. |
| G13.3 | Modüllere göre gösterim | Karşılandı | Sol tarafta daraltılabilir modül ağacı (komut sayılarıyla); modül sayfası (açıklama + komut listesi); aramada modül adı da aranır, sonuçlar modüllere göre gruplanır. |
| G13.4 | Var olan komut dosyasıyla uyum | Karşılandı | Sürüm 1 (modülsüz) dosya aynen okunur; dosyada komutlar `<Modul>` öğesinin içinde (elle okunur / düzenlenir). Elle düzenlemede aynı adlı modüller birleştirilir, çift komutlar atlanır. |
| G13.5 | Görünümün daha profesyonel olması | Karşılandı | Simgeli ve tutarlı düğmeler (birincil / ikincil / tehlike), arama kutusunda simge ve temizleme düğmesi, modül çipleri, sayı rozetleri, üzerine gelince vurgu, modül sayfası ve özet istatistikler. Windows'taki görünümü doğrulanmalı. |

### G14 — Tüm arayüzün profesyonel görünümü

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G14.1 | Tek tip düğmeler | Karşılandı | Uygulamadaki tüm düğmeler aynı çizimi kullanır: yuvarlatılmış köşe, birincil (mavi) / ikincil (kenarlıklı) / hayalet / tehlike (kırmızı) türleri, üzerine gelince ve basınca renk, klavye odak halkası, pasif görünüm. Yazılı düğmelere uygun simgeler eklendi (Kart Ekle, Seçilenleri Göster, Tümünü kapat, Yardım klasörü, yol çubuğunda ev simgesi). |
| G14.2 | Koyu temaya uymayan Windows kontrollerinin değiştirilmesi | Karşılandı | Açık renkli Windows ipuçları → koyu ipucu balonu; onay kutuları → açma / kapama anahtarı; açılır listeler → koyu liste (ok simgeli, odakta mavi kenarlık); sayı kutularının açık renkli okları → koyu oklar; metin kutularının siyah kenarlığı → tema rengi, odakta mavi; Windows MessageBox'ları → uygulamanın onay penceresi. |
| G14.3 | Pencereler (diyaloglar) | Karşılandı | Tüm küçük pencerelerde düğmeler ayrı renkli alt bantta; onay penceresinde çizilmiş bilgi / uyarı simgesi ve içeriğe göre yükseklik (uzun açıklamalar kesilmez); varsayılan düğme Enter ile çalışır. |
| G14.4 | Sembol karakterleri | Karşılandı | ⚠ ℹ ✓ ✕ ● ⏱ ⇶ gibi yazı tipine bağlı semboller (bazı Windows sürümlerinde kutu görünebilir) çizilmiş simgelerle gösterilir: bilgi şeritleri, kart durumu, bağlantı süresi, genel komut çubuğu, durum çubuğu, kopma uyarısı. |
| G14.5 | Sekmeler ve kimlik | Karşılandı | Sekmelerde simge (Telnet, Kart Dosya Gezgini, Komut Kütüphanesi) ve yuvarlatılmış etkin çizgi; uygulama simgesi (exe, görev çubuğu, pencere başlığı); Telnet araç çubuğunda gruplar arası ayraçlar; Kart Gezgini aramasında simge ve odak kenarlığı. |
| G14.6 | Doğrulama | Kısmen | Derleme ve tüm mantık testleri geçti. Çizim Windows'ta görsel olarak doğrulanmalı (geliştirme ortamında Windows ekranı çalıştırılamadı). |

### G15 — Uygulamanın adı ve simgesi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G15.1 | Uygulamanın adı "Vision" | Karşılandı | Pencere başlığı, imza, Hakkında, exe adı (`Vision.exe`), dosya özellikleri, proje / çözüm (`Vision.sln`) ve belgeler. Önceki sürümün ayarları (`config.xml`), `%APPDATA%\TelnetCards` klasörü ve kayıtlı FTP parolası çalışmaya devam eder (bu iç adlar bilerek değiştirilmedi; testte doğrulandı). |
| G15.2 | Uygulamayla ilgili bir uygulama simgesi | Karşılandı | Donanım kartı + terminal istemi: koyu lacivert zemin üzerinde yeşil devre kartı, altında altın kenar konnektörü, kartın üzerinde beyaz `>_` istemi; büyük boyutlarda devre yolları ve montaj delikleri. 16–256 px arası 9 boyut; küçük boyutlarda sadeleştirilmiş çizim. Exe'ye gömülü; görev çubuğu ve pencere başlıklarında görünür. Kaynak: `Vision.ico`. |

### G16 — Dosya Gezgini'nde yalnız kullanılabilir cihazlar

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G16.1 | Cihazlar listesinde yalnız içine girilebilen cihazlar gösterilmeli | Değiştirildi (G27.2) | `devs`ten sonra her cihaz listeleme komutuyla (`ll`, yoksa `ls`) denenir; dönüş değeri 0 olanlar gösterilir, diğerleri (ör. `/null`, `/tyCo/0`) gizlenir. Gizlenenler durum satırında adlarıyla belirtilir. Sonuç önbelleğe alınır (Cihazlar'a dönüşte kart yeniden denenmez); **Yenile** (F5) ve kart değişimi yeniden denetler. Denetim komutları terminale yazılmaz. Hiçbir cihaz girilemiyorsa bu açıkça yazılır; kartta listeleme komutu hiç yoksa süzme yapılmaz ve tümü gösterilir. |

### G17 — Kart yönetimi iyileştirmeleri

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G17.1 | FTP bilgisi karttan otomatik alınmalı (`bspBootChange`: h, u, pw), kart başına ayrı | Karşılandı | İlk FTP işleminde kartta `bspBootChange` çalıştırılır; tablo / kısa / boot satırı / etkileşimli biçimler ayrıştırılır (etkileşimlide yalnız Enter, değer değişmez). Bilgi yalnız o kartın oturumunda, bellekte tutulur; bağlantı kesilince silinir. Eksik / geçersiz değerde alan adıyla açıklama. Parola arayüzde, ayar dosyasında, ham cevapta (`••••••`) ve hata mesajlarında gösterilmez. |
| G17.2 | Bağlantı sınaması otomatik bilgiyle çalışmalı; sınama yüklemenin kanıtı sayılmamalı | Karşılandı | Sınama 4 aşamalı: oturum + listeleme, yazma (deneme dosyası), kartın aynı dosyayı görmesi, silme. |
| G17.3 | Sürükle-bırak yükleme hatasının asıl nedeni düzeltilmeli | Karşılandı | Bulunan nedenler: elle girilen FTP hesabı kartın boot hesabından farklıydı (kart dosyayı göremiyordu) → kartın kendi hesabı ve otomatik yol eşlemesi (`aygıt:klasör`); adreste `#`, `%` kaçışlanmıyordu; boş klasör katmanı bırakmayı reddediyordu; bırakma sırasında pencere açılıyordu (OLE bırakması içinde) → bırakmadan sonra işleniyor; yazma izni yokluğu genel hata veriyordu → sunucu cevabı ve nedenle ayrıntılı tanı; pasif kipte veri bağlantısı kurulamazsa etkin kiple bir kez yeniden deneme; Türkçe karakterli dosya adları baştan açıklamayla reddediliyor. |
| G17.4 | "FTP kopyalarını sil" düğmesi karta kopyalama başlayınca etkin olmalı; otomatik silme yapılmamalı | Karşılandı | Kart başına kopya listesi; FTP'ye gönderim başlarken etkinleşir, başarı / hatada etkin kalır, silince ya da bağlantı sıfırlanınca pasifleşir. Silme yalnız tıklama + onayla. |
| G17.5 | Kart başına ayarlanabilir hızlı komut düğmesi | Karşılandı | Komut satırında şimşek düğmesi; yalnız o karta gönderir; sağ tık düzenle / kaldır; kartla kaydedilir; boş komut ve bağlı olmayan kart durumu ele alınır; çift tıklama koruması. |
| G17.6 | Genel hızlı komut düğmesi | Karşılandı | Ekrandaki bağlı kartlara birer kez (tekrarlar ayıklanır); bir kartın hatası diğerlerini durdurmaz; sonuç raporu. |
| G17.7 | reboot / reboot 2 seçmeli düğme (kart ve genel) | Karşılandı | İki bölmeli düğme: sol çalıştırır, sağ ⇄ yalnız seçimi değiştirir (göndermez). Seçim düğmede yazar ve kaydedilir. Gönderimden sonra 4 sn kilit. |
| G17.8 | Komut dönüş değerleri canlı loglardan ayrılmalı | Karşılandı (tercih edilen çözüm) | Yalnız komut sürerken (gönderim ile `->` istemi arası) kabuğun dönüş biçimine tam uyan satırlar ayrılır; diğer satırlar terminalde kalır. Her dönüş kendi komutuyla eşleşir (FIFO); kart başına ayrı **Dönüş** paneli. Ayar: "Dönüş değerlerini ayır". Dosya Gezgini ham cevap paneline dokunulmadı. |
| G17.9 | Ortak terminal yakınlaştırması | Karşılandı | Ayar çubuğunda − %100 +; %60–%250; yüzdeye tıklama varsayılan; yeni kartlar aynı boyutla; Ctrl+tekerlek ve Ctrl + + / − / 0 ortak değeri değiştirir; tercih kaydedilir. |
| G17.10 | Sürekli kayıt anahtarı yerine anlık kayıt düğmeleri (kart ve genel) | Karşılandı | Kart başlığında ve genel çubukta disket; terminalde o an tutulan içerik; her tıklama yeni dosya (`Grup_IP[_Ad]_anlik_tarih_saat.log`); genelde her kart ayrı dosya, ekranda olmayanlar hariç, kart kart rapor; arka planda yazılır; günlük durumu değişmez. |

### G18 — Telnet, bağlantı, yapılandırma ve FTP düzeltmeleri

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G18.1 | Genel komut çubuğunda tüm kartların ekranını bir kerede temizleyen silgi düğmesi | Karşılandı | Genel çubukta disketin yanında silgi; ekrandaki her kartta kart başlığındaki silgiyle aynı işi yapar (ekran + geçmiş); karta komut gönderilmez; sonuç çubukta yazar. Ekranda kart yokken pasif. |
| G18.2 | Bağlantı zaman aşımı 4 katına çıkmalı | Karşılandı | `ConnectTimeoutMs` 10 sn → 40 sn. Eski sürümlerin dosyaya yazılmış varsayılanları (5000 / 10000) açılışta 40000'e yükseltilir; elle girilmiş başka değerlere dokunulmaz. Hızlı kart yine beklenmez. |
| G18.3 | Ayarlar yeni sürüme geçişte korunmalı (`%APPDATA%`, ilk sürümdeki gibi) | Karşılandı | `%APPDATA%\TelnetCards\config.xml`; exe'nin konumundan bağımsız. Dosya yoksa exe yanındaki `Konfig\config.xml` (ya da `%APPDATA%\Kartograf`) kopyalanır; yazılamazsa durum çubuğunda uyarı. |
| G18.4 | Telnet sekmesinde en fazla 24 kart | Karşılandı | Sınır 12 → 24; tüm açma yollarında geçerli; kartlar sığmazsa ızgara kaydırılır. |
| G18.5 | `bspBootChange` ile FTP bilgisinin yanlış alınması düzeltilmeli | Karşılandı | Neden: `ftp password (pw) (blank = use rsh) :` satırı anahtardan sonra ikinci bir parantez içerdiği için tanınmıyor, parola hiç okunmuyor ve ham cevapta gizlenmiyordu. Artık değer alanın `:` işaretinden sonraki tüm kısımdır (`h` → FTP IP, `u` → kullanıcı, `pw` → parola; değerdeki `:` / `=` bölünmez). Enter, `value = …` (ya da `value= …`) dönüşü görülene kadar gönderilir; alan olmayan satırlarda (`sysMvraöSet: … success`) fazladan Enter gitmez. Parola açıklamalı satırda da `••••••` ile gizlenir. |
| G18.6 | FTP bilgisi `bspBootChange` yerine `version` komutundan alınmalı | Karşılandı | Kartta `version` çalıştırılır; cevaptaki `Boot line:` / `BootLine:` satırından sonraki boot satırında `h=` → FTP IP, `u=` → kullanıcı, `pw=` → parola, satır başındaki `…host:` → kartın aygıtı. Yalnız boot satırı okunur; etkileşimli Enter döngüsü kaldırıldı (komut tek seferde biter, boot parametreleri değişmez). İşaret ya da değerler yoksa açıklamalı hata. Parola ham cevapta `••••••`. |
| G18.7 | Kartın aynı klasörü gördüğü yol varsayılan olarak `/host.` + kartın aygıt adı olmalı | Karşılandı | Aygıt adı `version` boot satırının başından alınır: parantez kapandıktan sonra `:` işaretine kadar (`:` hariç). `gei(0,0)host:` → `/host.host`, `gei(1,0)ali:` → `/host.ali`; FTP klasörü varsa `/host.host/upload`. FTP penceresinde "Kartın FTP yolu" olarak gösterilir; alan elle doldurulursa o kullanılır. |
| G18.8 | "Kopyalama başarılı olunca FTP'deki kopyayı hemen sil" varsayılan açık | Karşılandı | Yeni ayarlarda açık; eski ayar dosyalarında ilk açılışta bir kez açılır (`DeleteAfterCopyDefaultApplied`), sonra kullanıcının seçimi korunur. |
| G18.9 | Klasör sürükle-bırak yüklemede FTP'den karta kopyalarken telnet bağlantısı kopmamalı | Karşılandı | Neden: klasörler kartta FTP konumundan tek bir özyinelemeli `xcopy` ile kopyalanıyordu; ağ (FTP) aygıtı üzerinde `xcopy` kartın kabuğunu / telnet oturumunu düşürüyordu (tek dosya `copy` ile sorunsuzdu). Çözüm: klasör yapısı bilgisayarda bilindiği için kartta hedef ve alt klasörler `mkdir`, her dosya ayrı `copy` ile kopyalanır; dosya başına ilerleme; tek dosyanın hatası diğerlerini durdurmaz; bağlantı koparsa durur ve kaç dosyanın kopyalandığını yazar. İç öğe adları yüklemeden önce denetlenir. |
| G18.10 | Koyu temanın yanında açık tema ve aralarında geçiş düğmesi | Karşılandı | Tüm renkler iki paletli tema üzerinden (koyu / açık); dağınık sabit koyu renkler tema renklerine bağlandı. Menü çubuğunun sağında güneş / ay simgeli düğme ve Ctrl+Shift+L. Geçiş canlıdır: açık pencerelerdeki denetimler yeni temaya çevrilir, başlık ve kaydırma çubukları güncellenir, kart terminalleri ve durum şeritleri yeniden boyanır; bağlantılar ve içerikler korunur. Seçim `UiTheme` olarak kaydedilir ve açılışta ilk pencereden önce uygulanır. Açık temada kontrast ölçüldü (ana metin ≈ 15:1, ikincil ≥ 6:1, durum renkleri ≥ 4.5:1). Yardım sayfası temaya göre ayrı renkler kullanır. |

**Sınırlamalar:** Dönüş ayrımı VxWorks kabuğunun `->` istemine ve dönüş satırı biçimine dayanır; bir log aynı satırın ortasına karışırsa o satır ayrılamaz ve terminalde kalır. İstem görmeyen kabukta açık komut 120 sn sonra kapanır. Anlık kayıt, terminalde tutulan satırları (satır sınırı) içerir; daha eski çıktı kaydedilemez. Yönetici olarak çalışan Vision'a normal yetkili Windows Gezgini'nden sürükleme Windows tarafından engellenir.

### G19 — Kalıcı komut geçmişi, geçmiş penceresi ve kart ekranında renklendirme

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G19.1 | Kullanılan komutlar uygulama kapanıp açılınca kaybolmamalı; yeni komutlar listeye eklenmeli | Karşılandı | Tüm komutlar `%APPDATA%\TelnetCards\KomutGecmisi.txt` dosyasında (UTF-8, satır başına bir komut) tutulur; her yeni komutta hemen kaydedilir, ilk kullanımda okunur. Kartlar ve genel komut çubuğu tek ortak listeyi kullanır (↑ / ↓ konumu her satırın kendisinindir). Aynı komut yinelenmez, en yeniye taşınır; en fazla 5.000 komut. Okunamayan dosya `.bozuk` olarak yedeklenir; yazma hatası uygulamayı durdurmaz (`hatalar.log`). |
| G19.2 | Komut satırında önceki komutların hepsini liste halinde gösteren düğme | Karşılandı | Kart komut alanının ve genel komut alanının içinde saat simgeli **Komut geçmişi** düğmesi; pencere tüm komutları en yeniden eskiye listeler. Seçilen komut komut satırına yazılır, gönderilmez. Listeden tek komut ya da onayla tümü silinebilir. Bağlı olmayan kartta pasif. |
| G19.3 | Geçmiş listesinde geriye doğru arama (reverse search) | Karşılandı | Pencerenin arama kutusu yazıldıkça süzer (metni içeren komutlar, büyük / küçük harf duyarsız, en yeni eşleşme seçili, eşleşen kısım vurgulu). Komut satırında **Ctrl+R** pencereyi yazılı metinle açar; pencerede Ctrl+R / Ctrl+Shift+R sonraki / önceki eşleşme. |
| G19.4 | Kart ekranında seçilen kelime sağ tıkla istenen renge boyanmalı | Karşılandı | Terminalde sağ tık → **Renklendir** → 8 hazır renk ya da **Diğer renk…**; seçim yoksa tıklanan kelime seçilir. Metnin ekrandaki tüm geçişleri ve sonradan gelen satırlardaki eşleşmeler renkli zeminle (okunur yazı rengiyle) gösterilir; paketlere bölünmüş kelimeler de bulunur. Büyük / küçük harf duyarsız; kart başına en fazla 30 kural; tek tek ya da tümü kaldırılabilir. Kurallar kartın oturumunda tutulur (pencere kapatılıp açılınca korunur), tema / bağlantı durumu / yakınlaştırma değişince yeniden boyanır. Sağ tık menüsünde ayrıca **Kopyala**. |

**Sınırlamalar:** Komut geçmişi aynı anda açık iki Vision penceresi arasında anlık paylaşılmaz; dosyaya en son yazan pencerenin listesi kalır. Renklendirmeler uygulama kapanınca silinir ve anlık kayıt dosyalarına yansımaz (dosyalar düz metindir).

### G20 — Hazır VxWorks 6.4 komut kütüphanesi ve karta aktarma

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G20.1 | Komut kütüphanesi VxWorks 6.4 telnet (kabuk) komutlarıyla hazır gelmeli: ne işe yaradığı, varsa kullanım örneği ve örnek çıktı | Karşılandı | `Komutlar\Komutlar.xml` 12 modülde 113 komutla gelir (kabuk, sistem bilgisi, görevler, süreçler (RTP), bellek, modüller, boot / BSP, dosya sistemi, ağ (IPNet / IPCOM dahil), zamanlayıcılar ve watchdog, senkronizasyon nesneleri, hata ayıklama). Kullanıcının verdiği 78 komutluk listenin tamamı kapsanır; karta özel `FPGAGiveBoardReset` ve `debugAgentStart` adlarına göre açıklanmış, doğrulanmak üzere işaretlenmiştir. Her komutta Türkçe açıklama, yazılış / örnekler ve temsilî örnek çıktı. `moduleShow`, `bspBootChange` ve PowerPC kartları için `bootChange` istenen açıklamalarla. Dosya exe'ye de gömülüdür. |
| G20.2 | Kullanıcı komut eklemeye ve komutları modüllere taşımaya devam edebilmeli | Karşılandı | Hazır komutlar sıradan kütüphane kayıtlarıdır (ekle / düzenle / sil / taşı aynen çalışır). Kütüphane tamamen boşsa ilk açılışta hazır komutlarla dolar; **Hazır komutlar** düğmesi yalnız eksik komut ve modülleri ekler, mevcut ve düzenlenmiş kayıtlara dokunmaz. Derleme mevcut `Komutlar.xml`'in üzerine yazmaz. |
| G20.3 | Kütüphanedeki komut doğrudan istenen karta aktarılıp kullanılabilmeli | Karşılandı | Komut sayfasında **Karta aktar** ve listede sağ tık → **Karta aktar**: kartlar gruplarıyla ve durum noktasıyla listelenir; seçilen kart Telnet sekmesinde açılır ve komut komut satırına yazılır (gönderilmez, argümanlar düzenlenip Enter ile gönderilir). Yazılıştan seçili satır ya da ilk satır aktarılır, ` // açıklama` atılır. Bağlı olmayan kartta önce bağlanılır, bağlanınca yazılır; bağlanılamazsa uyarı. **Ekrandaki tüm kartlar** seçeneği genel komut çubuğuna yazar. |

**Sınırlamalar:** Örnek çıktılar temsilîdir (BSP'ye ve imaja göre değişir); gerçek kartlarda doğrulanıp gerekirse kütüphaneden düzeltilmelidir. Bazı komutlar (ör. `spy`, `ping`, `bspBootChange`) yalnız kartın imajında ilgili bileşen varsa çalışır.

### G21 — Gömülü komut kütüphanesi, sekme şeridinde Yardım / tema, EFI koruması

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G21.1 | Komut kütüphanesi uygulamaya gömülü olmalı; dışarıdan eklenip çıkarılamamalı, düzenlenememeli | Karşılandı | Kütüphane yalnız exe'ye gömülü `Komutlar.xml` kaynağından okunur (`CommandLibrary.Embedded`); diske dosya yazılmaz / okunmaz, paket ve derleme çıktısında `Komutlar` klasörü yoktur. Ekleme, düzenleme, silme, modüle taşıma, sürükle-bırak, Hazır komutlar ve klasör düğmeleri ile ilgili kısayollar kapalıdır; kayıt katmanı da salt okunur kütüphanede yazmayı reddeder. Arama, okuma, kopyalama ve Karta aktar çalışır. Yeni komutlar sonraki sürümlerde gelir. G20.2'nin yerini alır. |
| G21.2 | Yardım ve tema düğmesi sekmelerin hizasında sağda olmalı; üstteki ayrı menü çubuğu kalkmalı | Karşılandı | Menü çubuğu kaldırıldı. Sekme şeridinin sağında **Yardım** (açılır menü: Kullanım Kılavuzu, Gereksinimler, Yardım klasörü, Hakkında) ve onun sağında **Açık / Koyu tema** düğmesi. F1 ve Ctrl+Shift+L kısayolları formda işlenir. |
| G21.3 | Kart Dosya Gezgini'nde EFI adlı dosya silinmemeli | Karşılandı | Adı `EFI` olan dosya / klasör (büyük / küçük harf duyarsız) ve içindeki öğeler silinemez, taşınamaz, yeniden adlandırılamaz; karta komut gönderilmeden şeritte uyarı gösterilir, menüde bu işlemler pasiftir. İçinde EFI bulunan klasörlerin içindekilerle silinmesi, taşınması ve yeniden adlandırılması da engellenir (işlemden önce klasör ağacı en fazla 300 klasöre kadar taranır; daha büyükse güvenli tarafta kalınıp reddedilir). Kopyalama serbesttir. |

### G22 — FTP'de Vision çalışma klasörü

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G22.1 | Bilgisayardan karta aktarımda başarısız kalan kopyalar FTP'deki mevcut dosya ve klasörlerle karışmamalı; aktarım FTP içinde Vision adlı bir klasör üzerinden yapılmalı | Karşılandı | Kartın etkin aktarım ayarlarında (`TransferSettings.ForCard`) FTP klasörü ve kartın gördüğü yol `…/Vision` olur: dosyalar / klasörler FTP'de `Vision` klasörüne gönderilir, kart oradan `copy` ile hedefe kopyalar. Klasör yoksa her aktarımdan önce oluşturulur (varsa dokunulmaz); oluşturulamazsa aktarım açık hatayla durur. "Birlikte aç" geçici kopyaları (`_tkt_…`), bağlantı sınaması dosyası ve "FTP kopyalarını sil" de aynı klasörü kullanır. FTP ayarları penceresindeki eşleme metni `…/Vision` yollarını gösterir. |

**Sınırlama:** Önceki sürümlerin FTP klasörünün kendisinde bıraktığı kopyalar taşınmaz; gerekirse elle silinmelidir.

### G23 — Kartın FTP yolu her zaman karttan, kaydedilmeden

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G23.1 | "Kartın aynı klasörü gördüğü yol" kartın FTP yolundan (version boot satırı) otomatik dolmalı; kart değiştikçe ayrıca kayıt gerekmemeli | Karşılandı | Değer ayar dosyasına yazılmaz ve okunmaz (`CardPathPrefix` XmlIgnore; eski sürümlerin kaydettiği tek, ortak değer — ör. her kartta `/host.host` — artık uygulanmaz). Yol her kart için kartın oturumundaki FTP bilgisinden hesaplanır: `/host.` + aygıt adı + FTP klasörü (`motetsec(0,0)mkozdem:` → `/host.mkozdem`). Pencerede kutu bu değerle dolar, FTP klasörü değişince birlikte güncellenir. |
| G23.2 | "Karttan yeniden al" alanı güncellemeli | Karşılandı | Yeni bilgi alınınca kutu karttan gelen yola döner (elle yazılmış değer silinir). Bağlantı kopunca / yeniden bağlanınca da bilgi ve yol karttan yeniden alınır. |
| G23.3 | Kullanıcı alanı yine değiştirebilmeli | Karşılandı | Elle yazılan yol yalnız o kartın bu bağlantıdaki FTP bilgisiyle birlikte bellekte tutulur (`CardFtpCredentials.PathOverride`); başka kartları etkilemez, hiçbir yere kaydedilmez. Bağlı kartlar arasında geçişte her kart kendi bilgisini ve yolunu korur. Eşleme satırı elle değiştirildiğini belirtir; Bağlantıyı sına penceredeki (kaydedilmemiş) değerle çalışır. G18.7'nin yerini alır. |

### G24 — Akış izlenirken yatay kayma olmamalı

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G24.1 | Küçük kart ekranında uzun log satırları gelirken ekran satır sonu ile satır başı arasında yatayda gidip gelmemeli; hangi satırın sonuna bakılacağına kullanıcı karar vermeli | Karşılandı | Neden: akış izlenirken her eklemede imleç metnin sonuna taşınıp `ScrollToCaret` ile görünür yapılıyordu; satır kutudan genişse kutu imleç için sağa, satır sonu gelince (imleç yeni satırın başında) sola kayıyordu. Yazı küçültülünce satırlar sığdığı için görülmüyordu. Çözüm: en alta inme (`ScrollToEnd`) yalnız dikey konumu değiştirir; yatay konum eklemeden önceki değere (`EM_GETSCROLLPOS` / `EM_SETSCROLLPOS`) geri alınır. Satır sınırı değişiminde, komut gönderirken ve "Yeni Satırlar" göstergesinde de aynı davranış. |
| G24.2 | Enter'a basınca / yeni satır gelince son satır yatay kaydırma çubuğunun altında kalmamalı | Karşılandı | Neden: uzun satır gelince yatay çubuk beliriyor, görünür alan kısalıyor ama kutu yeniden kaydırılmıyordu; `ScrollToCaret` da çubuğun kapladığı yeri hesaba katmıyordu. Çözüm: en alta inerken ayrıca `WM_VSCROLL / SB_BOTTOM` gönderilir; ekleme sonrası çizim açıldığında (kaydırma ölçüleri güncel) bir kez daha en alta inilir; akış izlenirken görünür alan değişirse (`ClientSizeChanged`: yatay çubuk belirdi, pencere küçüldü) yeniden en alta inilir. Akışı izleme durumu yalnız kullanıcının kaydırmasıyla değişir. |
| G24.3 | Bağlantı kesilip kart griye dönerken (ya da yazı boyutu / renklendirme değişirken) ekran baştan sona kaymamalı | Karşılandı | Neden: tüm metin yeniden renklenirken uyarı satırları ve renklendirilen kelimeler tek tek seçiliyor, her seçim ekranı o satıra kaydırıyordu (ekran "akıyor" gibi görünüyordu; bağlantı kod tarafında kapalıdır, yeni veri gelmez). Çözüm: `RecolorKeepingView` — yeniden renklendirme çizim kapalıyken yapılır, seçim ve kaydırma konumu geri alınır; gri / normal görünüm, yakınlaştırma, renklendirme değişimi ve ilk açılışta kullanılır. Ayrıca kaydırma çubukları kalıcı (`ForcedBoth`): uzun satır gelince yatay çubuk belirip görünür alanı değiştirmez; G24.2'deki "alan değişince en alta in" olayı yalnız kart penceresinin boyutu değişince çalışır (kaydırma ↔ çubuk döngüsü olmaz). |

### G25 — Hazır temalar ve tema özelleştirme

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G25.1 | 4–6 hazır tema; mevcut koyu ve açık tema bunların içinde olmalı | Karşılandı | `ThemeCatalog`: Koyu, Açık, Solarized, Monokai, VS Code Dark Modern, Yüksek kontrast, Matrix (eski adı "Klasik terminal"; kimliği `klasik-terminal` korunduğu için kayıtlı seçim bozulmaz) — G28.7. Her tema 10 temel renkle tanımlanır; arayüzün ~60 rengi bunlardan türetilir (`Theme.BuildPalette`: her türetilen renk iki temel rengin arası bir ton, oran koyu / açık hazır paletten ölçülür). Koyu ve Açık varsayılan renkleriyle önceki sürümlerle birebir aynıdır. Tema değişiminde denetim renkleri değerden değere çevrildiği için türetilen paletteki tüm değerler benzersiz tutulur. |
| G25.2 | Temalar Yardım'ın yanında açılır menüden seçilebilmeli | Karşılandı | Sekme şeridinin sağında **Temalar** (palet simgesi): hazır temalar renk örneğiyle, seçili olan işaretli, özelleştirilmişler "(özelleştirildi)"; altta **Temayı özelleştir…**. Eski Açık / Koyu düğmesinin yerini alır; Ctrl+Shift+L sonraki temaya geçer. |
| G25.3 | Temanın istenen kısmının rengi değiştirilebilmeli; anlık önizleme, kaydetme, varsayılana dönme | Karşılandı | `ThemeEditorDialog`: tema seçimi, 10 renk satırı (tıklayınca renk seçici; özel renkler kutusunda temanın renkleri hazır), her değişiklik tüm uygulamaya anında uygulanır, ayrıca pencerede kart / terminal / düğme / durum önizlemesi ve kontrast (WCAG 4.5:1) uyarısı. Kaydet: yalnız varsayılandan farklı renkler `config.xml`'e (`ThemeCustomizations`) yazılır, seçili tema `UiThemeId`'de. Varsayılana dön: görüntülenen temanın hazır renkleri. İptal / kapat: açılıştaki tema ve renkler geri gelir. Birincil düğmede beyaz yazı okunur kalsın diye vurgu dolgusu gerekirse otomatik koyulaştırılır. Eski sürüm ayarı (`UiTheme`) ilk açılışta Koyu / Açık'a çevrilir. |

### G26 — Uzun çıktıda kaydırma (hata düzeltmesi)

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G26.1 | Çok satırlı kart ekranında (ör. 36.000 satır) kullanıcı yukarı kaydırınca ekran en alta ya da en üste atlamamalı; aradaki satırlar okunabilmeli | Karşılandı | Neden: RichEdit'in kaydırma konumu ve kaydırma çubuğu değerleri 16 bittir; belge 65.535 pikseli geçince orantılı sıkıştırılır. "En altta mı" kontrolü bu yüzden birkaç satır yukarıyı da en alt sayıyor (ekran aşağı çekiliyordu), geri yüklenen konum ise gerçek pikselden çok küçük kalıyordu (ekran en üste atlıyor, en üstten aşağı inilemiyordu). Çözüm: dikey konum ilk görünen satır numarasıyla kaydedilip geri yüklenir (`EM_GETFIRSTVISIBLELINE` / `EM_LINESCROLL`), satır sınırı kırpmasında silinen satır sayısı kadar düzeltilir; "en altta" son görünen satırla ölçülür; yatay konum ayrıca ve dikeye dokunmadan geri alınır. |

### G27 — Boot ayarları penceresi ve hızlı açılan Dosya Gezgini

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G27.1 | `bspBootChange` / `bootChange` gönderilince adımlar araya log girmeden ayrı bir ekranda izlenip düzenlenebilmeli | Karşılandı | Kart terminali, hızlı komut ya da genel komut çubuğu bu komutu gönderirken `BootChangeDialog` açılır (kart başına bir pencere). Kartın ham çıktısı terminal akışı değişmeden bir kopyadan (`CardSession.OpenTap`) okunur; `BootChangeParser` alan istemlerini ("alan adı      : değer ") loglardan ayırır: satır sonu gelmemiş son satır, veri 120 ms kesilince (kart girişi bekliyor) istem sayılır; istemin aynı satırına eklenen eko / log ayrılır; logla birleşmiş istemde değer ilk sözcüktür. Alan tanıma: bilinen VxWorks alan adı, parantez içi kısa anahtar ("(h)") ya da hizalama boşluğu; böylece "görev: mesaj" biçimli loglar alan sanılmaz. Gönderilenler kartın kendi girişine gider (satır sonu terminal ayarındaki gibi; komut geçmişine ve dönüş değerlerine yazılmaz): değer, boş (değiştirmeden geç), `.` (temizle), `-` (önceki alan), Ctrl+D (çık). Ayrıca "Değeri düzenle", "Kalanları geçip bitir", listeden "bu alana git"; değişen alanlar `eski → yeni`; araya giren loglar ayrı bir panelde. Bitiş `value = …` ya da `->` istemiyle anlaşılır. Pencere açıkken o kartta arka plan komutları (dosya gezgini) gönderilmez (`InteractiveInputActive`). |
| G27.3 | Boş bir boot alanında, o an karttan gelen log alanın değeri gibi gösterilmemeli (hata düzeltmesi) | Karşılandı | Neden: istem ("alan : ") boş değerle gelip hemen ardından bir log aynı satıra eklenince logun ilk sözcüğü değer sanılıyordu (karta yazılmıyordu, yalnız görüntü). Çözüm: kartın verisi geldiği parçalar halinde korunur (`TerminalTap.Chunk`) ve istem, kartın onu gönderdiği parçadan okunur; sonra gelen parçalar değeri değiştirmez. Değer istemden ayrı parçada gelirse yalnız tek sözcük + boşluk ve 250 ms içinde ise kabul edilir (40 ms'den geç geldiyse ve ardından log eklenirse şüpheli sayılır). İstem ile log aynı parçada gelip değer ayrılamıyorsa alan **"okunamadı (araya log girdi) — terminale bakın"** olarak gösterilir; yanlış değer asla gösterilmez. |
| G27.2 | Dosya Gezgini cihaz listesi hemen gelmeli; tüm cihazlar görünmeli, içine girilebilirlik yalnız girerken denetlenmeli | Karşılandı | G16.1'deki ön denetim (her cihaz için `ll`) kaldırıldı: Cihazlar listesi yalnız `devs` ile dolar. Bir cihaza girilirken `ll` (yoksa `ls`) dönüş değeri 0 değilse sarı uyarı şeridinde "… cihazının içine girilemiyor (dönüş değeri N)" yazar, liste değişmez, cihaz soluk ve "Cihaz · girilemiyor (N)" olarak işaretlenir. İşaret kart değişince / Cihazlar'da Yenile ile sıfırlanır; cihaz sonradan açılırsa kalkar. |

### G28 — Sentinel: kartın sağlık sayfası uygulama içinde

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G28.1 | Kart penceresinde Sentinel web sunucusunu başlatan / durduran düğme; yazı değil simge, açıklama ipucunda | Karşılandı | Hızlı komutun yanında nabız çizgisi simgeli düğme (`CardView.Sentinel.cs`). Durmuşken `SentinelWebServerBaslat`, çalışırken `SentinelWebServerBitir` gönderir (yalnız o karta; çift tıklama 1 sn korumalı). Simge rengi durumu gösterir (soluk / sarı / yeşil); ipucu ne yapacağını ve adresi yazar. Sağ tık: Sentinel sekmesinde aç. |
| G28.2 | Sunucunun çalışıp çalışmadığı doğru gösterilmeli | Karşılandı | Komutlar void olduğu için dönüş değerine bakılmaz. `SentinelTracker` kartın 8080 portuna TCP bağlantısı dener (istek göndermeden, hemen kapatır): normalde 5 sn'de bir, başlatma / durdurma sonrası 1 sn'de bir; yalnız bağlı kartlar. Başlatmada 20 sn, durdurmada 15 sn içinde değişiklik olmazsa uyarı. Çalışırken art arda iki başarısız denemede "durdu" sayılır (titreme olmaz). Terminalden başlatılan / reboot ile kapanan sunucu kendiliğinden algılanır. |
| G28.3 | Telnet / Dosya Gezgini / Komut Kütüphanesi gibi "Sentinel" sekmesi; bağlı ve sunucusu çalışan kartın HTML sayfası tarayıcı açmadan uygulama içinde görülmeli | Karşılandı | `SentinelView` (Ctrl+4): solda bağlı kartlar Sentinel durumuyla (çalışanlar üstte), sağda seçilen kartın `http://kartIP:8080` sayfası gömülü `WebBrowser` ile; sayfa kendini yeniler, F5 / Yenile elle yeniler. Sunucu yokken açıklama ve Başlat / Durdur. Sayfa yüklenemezse 2 sn sonra yeniden denenir; sekme görünmezken sayfa kapatılır. Sayfa yeni pencere / dış tarayıcı açamaz. |
| G28.5 | Sunucu çalışırken sayfa ara ara kaybolup "Sentinel çalışmıyor" yazmamalı (hata düzeltmesi) | Karşılandı | Neden: durum, port 8080'e bağlanma denemesiyle izleniyordu ve art arda iki başarısız deneme "durdu" sayılıyordu; sayfayı sunan küçük gömülü sunucu meşgulken deneme cevapsız kalınca sayfa kapanıyor, sonraki başarılı denemede geri geliyordu. Çözüm: deneme sonucu üçe ayrıldı — bağlandı / reddedildi (portu dinleyen yok) / cevapsız (meşgul). Cevapsız denemeler durumu değiştirmez; çalışırken "durdu" için kart bağlantıyı en az 3 denemede ve 8 sn boyunca, arada başarılı deneme olmadan reddetmelidir (ret varken 2 sn'de bir denenir). Bağlantı süresi 3 sn. Durdur'a basıldıysa ilk ret yeterlidir. |
| G28.6 | Başka sekmeye geçip dönünce Sentinel sayfası yeniden bağlanmamalı; arka planda sürmeli | Karşılandı | Sekme bir kez açıldıktan sonra sayfa gizlenince kapatılmaz (önceden `about:blank`'e geçiliyordu, dönüşte yeniden bağlanıp sayaçlar sıfırlanıyordu). `SentinelView.Tick` sekme görünmese de çalışır: sunucu durursa sayfa yine kapanır. Sekme hiç açılmadıysa karta bağlanılmaz. Tek görüntüleyici olduğu için arka plandaki yük, sekme açıkken olanla aynıdır (bir sayfa). |
| G28.7 | "Klasik terminal" temasının adı "Matrix" olmalı; VS Code Dark Modern teması eklenmeli | Karşılandı | `ThemeCatalog`: ad değişti, kimlik aynı. Yeni tema VS Code Dark Modern renkleriyle: zemin #181818, paneller #1F1F1F, kenarlık #2B2B2B, yazı #CCCCCC, vurgu #0078D4, başarılı #2EA043, uyarı #CCA700, hata #F85149. |
| G28.4 | Sayfa beyaz kalıp donmamalı; sayfanın kendi yenilemesi ve düğmeleri çalışmalı (hata düzeltmesi) | Karşılandı | Neden: ilk sürümdeki Windows görüntüleyicisi (WebBrowser, IE11 motoru) sayfanın modern JavaScript'ini çalıştıramıyordu; sayfa ilk hâliyle kalıyordu. Çözüm: sayfa Edge WebView2 (Chromium) ile gösterilir (`SentinelPage.cs`; NuGet `Microsoft.Web.WebView2` 1.0.1072.54 — .NET 4.5+ ve 2021 sonrası tüm WebView2 Runtime sürümleriyle uyumlu, proje 4.6.1'de kalır). Tarayıcı verileri `%LOCALAPPDATA%\TelnetCards\WebView2`'de. Runtime yoksa eski görüntüleyiciye (IE11 kipi, HKCU `FEATURE_BROWSER_EMULATION`) dönülür, uyarı gösterilir ve sayfa 2 sn'de bir yeniden yüklenir. Üst çubuktaki düzenli yenileme seçimi G37 ile kaldırıldı. Paket: `lib` klasöründe `Microsoft.Web.WebView2.Core.dll`, `Microsoft.Web.WebView2.WinForms.dll` ve `runtimes\win-*\native\WebView2Loader.dll` (G30.3). |

### G29 — Kart ekleme ve grup adı

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G29.1 | Kart eklerken başlangıç IP'si, "Sıralı doldur"a basmadan Kart 1'e de yazılmalı (hata düzeltmesi) | Karşılandı | Neden: IP alanları açılışta örnek IP'yle dolduruluyor, başlangıç IP'si değişince yalnız sonradan eklenen (boş) alanlar dolduruluyordu; Kart 1 örnek değerde kalıyordu. Çözüm: `AddCardsDialog` otomatik doldurduğu alanları izler; başlangıç IP'si geçerli bir değere değişince elle değiştirilmemiş tüm alanlar (Kart 1 dahil) sıralı güncellenir. Elle yazılan alan korunur; "Sıralı doldur" yine hepsini yeniden yazar. |
| G29.2 | Oluşturulmuş grubun adı sağ tıkla değiştirilebilmeli | Karşılandı | Kart Gezgini'nde grup sağ tık menüsünde "Grubun adını değiştir…" ve F2. Boş ad ve başka grupla aynı ad (büyük / küçük harf fark etmez) kabul edilmez. Kartlar, bağlantılar, port değişmez; ad config'e kaydedilir, kart başlıkları, Dosya Gezgini ve diğer listeler hemen güncellenir. |

### G30 — Popüler editör temaları

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G30.1 | "VS Code Dark Modern" temasının adı "Dark Modern" olmalı | Karşılandı | `ThemeCatalog`: ad değişti, kimlik (`vscode-dark-modern`) aynı kaldı; kayıtlı seçim bozulmaz. |
| G30.2 | Var olan temalara ek olarak kod düzenleyicilerde popüler temalar eklenmeli; adlarında "VS Code" geçmemeli, tema adları olduğu gibi kullanılmalı | Karşılandı | 14 yeni tema (temaların resmî renk paletlerinden): Light Modern, Dark+, Solarized Light, Dracula, One Dark Pro, GitHub Dark, GitHub Light, Nord, Night Owl, Tokyo Night, Catppuccin Mocha, Gruvbox Dark, Material Theme, Ayu Dark. "Solarized" teması "Solarized Dark" oldu (kimlik `solarized` aynı). Toplam 21 tema; menüde "Vision temaları" (Koyu, Açık, Yüksek kontrast, Matrix) ve "Popüler editör temaları" (Dark Modern, Light Modern, Dark+, Monokai, Solarized Dark, Solarized Light, Dracula, One Dark Pro, GitHub Dark, GitHub Light, Nord, Night Owl, Tokyo Night, Catppuccin Mocha, Gruvbox Dark, Material Theme, Ayu Dark) başlıkları altında. Her tema 10 temel renkle tanımlanır, arayüzün geri kalanı bunlardan türetilir (G25.1); düzenleyiciyle özelleştirilebilir. Ctrl+Shift+L menü sırasıyla sonraki temaya geçer. |
| G30.3 | Çalıştırma için gereken DLL'ler exe ile aynı seviyede değil, ayrı bir klasörde olmalı | Karşılandı | Paket düzeni: `Vision\Vision.exe`, `Vision\lib\` (WebView2 `Microsoft.Web.WebView2.Core.dll`, `Microsoft.Web.WebView2.WinForms.dll`, `runtimes\win-*\native\WebView2Loader.dll`) (Yardim klasörü G32 ile kaldırıldı). .NET yalnız exe'nin yanına baktığından `LibFolder.Setup` (Program açılışında, WebView2 türlerine dokunmadan önce) yönetilen DLL'leri `AssemblyResolve` ile `lib`'den yükler ve yerel yükleyiciyi işlemin mimarisine göre önceden yükler; ek config dosyası gerekmez. `lib` yoksa (Visual Studio çıktısı) DLL'ler exe'nin yanından yüklenir. Edge kitaplığı hiç yüklenemezse Sentinel çökmeden eski görüntüleyiciye döner. |

### G31 — Karttan bilgisayara sürükle-bırak

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G31.1 | Kart Dosya Gezgini'nden bir dosya / klasör sürüklenip masaüstüne ya da bilgisayarda herhangi bir yere bırakılınca karttan bilgisayara kopyalanmalı (bilgisayardan karta yüklemenin tersi) | Karşılandı | `FileExplorerView.DragOut.cs` (bkz. G31.3): sürüklenen veri iç biçimin yanında Windows'a geçici klasördeki gerçek bir yolu (FileDrop; öğeyle aynı adlı boş yer tutucu) verir. Fare Vision'ın DIŞINDA, kabul eden bir hedefe bırakılınca, bırakma hedefe iletilmeden önce (QueryContinueDrag) öğe bu yola indirilir: ilerleme penceresi açılır; dosya `FetchCopyAsync` ile (kartta `copy` → FTP konumu → FTP'den indirme → geçici kopyanın silinmesi; ikili dosyalar bozulmaz) `%TEMP%\TelnetCards\Surukle\…` altına alınır; klasör `CollectTreeAsync` ile taranıp (her klasör `ll`; en fazla 300 klasör / 3000 dosya) dosya dosya indirilir. Yollar Gezgin'e verilir, Gezgin bırakılan yere kopyalar (çakışmayı kendisi sorar). İptal o anki dosya bitince durdurur, hiçbir şey kopyalanmaz. Vision'ın kendi listesine / adres çubuğuna bırakma iç biçimle eskisi gibi kart içinde taşır / kopyalar (önce iç biçim denetlenir). Geçici kopyalar kapanışta, kalanlar sonraki açılışta silinir. |
| G31.3 | Sürükle-bırak Windows Gezgini'ni çökertmemeli (hata düzeltmesi) | Karşılandı | Neden: ilk sürümde dosya listesi bırakmaya kadar "yok" (null) döndürülüyordu; .NET bunu Gezgin'e başarı koduyla birlikte geçersiz (başlatılmamış) bir HDROP bloğu olarak iletiyordu, sürükleme sırasında listeyi okuyan Gezgin çöküp yeniden başlıyordu. Çözüm: Gezgin'e baştan geçerli yollar (yer tutucular) verilir; içerik, bırakma Gezgin'e iletilmeden önce Vision tarafında indirilir (Gezgin beklemez). İndirme başarısız / iptal ise bırakma iptal edilir. Hedef kabul etmiyorsa (etki None) indirilmez. |
| G31.2 | Sürüklemeden de karttan bilgisayara alınabilmeli | Karşılandı | Sağ tık → "Bilgisayara kaydet…": klasör seçilir (son seçilen hatırlanır), aynı adlı öğe varsa sorulur, aynı ilerleme penceresiyle indirilir, bitince kayıt yeri Windows Gezgini'nde açılır. |

### G32 — Yardım belgeleri yalnız exe'de

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G32.1 | Kullanım Kılavuzu ve Gereksinimler arayüzde olduğu için pakette ayrıca Yardim klasörü verilmemeli; belgeler yalnız depoda dosya olarak durmalı | Karşılandı | `HelpMarkdown.LoadDocument` belgeyi yalnız exe'ye gömülü kopyadan okur (önceden exe'nin yanındaki Yardim klasörü öncelikliydi; eski sürümden kalan klasör yeni sürümde eskimiş kılavuz gösterebiliyordu). `.md` dosyaları derleme çıktısına kopyalanmaz, paket (`Vision.zip`) yalnız `Vision.exe` ve `lib\` içerir. "Yardım klasörünü aç" menü öğesi ve yardım penceresindeki düğme kaldırıldı. Belgeler depoda `Yardim\*.md` olarak düzenlenir ve GitHub'da okunur. |

### G33 — Arayüz düzeltmeleri

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G33.1 | Pencere başlığında geliştirici adı olmamalı (Hakkında ve durum çubuğunda zaten var) | Karşılandı | Başlık yalnız "Vision". |
| G33.2 | Gereksinimler belgesi uygulamada görünmemeli; yalnız depoda geliştirici için dursun | Karşılandı | Yardım menüsünden ve yardım penceresinden "Gereksinimler" kaldırıldı; belge exe'ye gömülmez (`Yardim\Gereksinimler.md` depoda `None` öğesi). Uygulamada yalnız Kullanım Kılavuzu ve Hakkında. |
| G33.3 | Telnet sekmesindeki "Zaman damgası" varsayılan olarak açık gelmeli | Karşılandı | Yeni ayarlarda açık. Eski ayar dosyalarında önceki varsayılan (kapalı) kayıtlı olduğundan `DefaultsRevision` ile bir kez açılır; kullanıcı sonra kapatırsa korunur. |
| G33.4 | Tema değişince Yardım / Temalar menüleri ve sağ tık menüleri de yeni renklere geçmeli (hata düzeltmesi) | Karşılandı | Neden: tema geçişi yalnız pencerelerin içindeki denetimleri ve onlara bağlı sağ tık menülerini çeviriyordu; düğmeyle açılan menüler ve ayrı tutulan sağ tık menüleri eski renklerde kalıyordu. Çözüm: `DarkMenuRenderer` kullanıldığı her menüyü kaydeder (`Theme.TrackMenu`, zayıf başvuru); `Theme.Apply` pencerelerden sonra bu menüleri ve alt menülerin açılır kutularını da çevirir. Aynı denetimin iki kez çevrilmemesi için geçiş sırasında ziyaret edilenler tutulur. |
| G33.5 | Kart Dosya Gezgini'ndeki kart seçiminde yalnız bağlı kartlar listelenmeli | Karşılandı | Liste bağlı / bağlanmakta / yeniden bağlanmakta olan kartlardan oluşur ve bağlantılar değişince kendiliğinden yenilenir (oturum oluşturmadan durum sorulur). O an seçili kart bağlantısı kopsa da listede kalır (reboot sırasında açık klasör kaybolmaz; "Bağlan" ile devam edilir); başka kart seçilince düşer. Kart Gezgini'nden "Dosyalarını gez" bağlı olmayan kartı da seçer. Bağlı kart yoksa "Bağlı kart yok" açıklaması gösterilir. |

### G34 — Disk boyutları

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G34.1 | Kart Dosya Gezgini'nin cihaz listesinde erişilebilen disklerin toplam / boş / kullanılan alanı Boyut sütununda gösterilmeli; liste yavaşlamamalı; yalnız /ata… ve /bd… cihazları | Karşılandı | Liste `devs` ile hemen gelir; ardından Cihazlar görünürken ve başka işlem yokken `FileBrowser.QueryNextSpaceAsync` her seferinde bir cihaz için `dosFsShow "/cihaz"` çalıştırır (20 sn zaman aşımı; her cihaz bir kez, Yenile / kart değişince yeniden). `DosFsShowParser`: toplam = "total number of sectors" × "bytes per sector", boş = "free space on volume … bytes" (binlik ayırıcılar yok sayılır), kullanılan = toplam − boş. Boyut sütunu "226 MB boş / 250 MB" + doluluk çubuğu (%90 sarı, %97 kırmızı); hücreler yerinde güncellenir (seçim / kaydırma korunur). Disk içindeyken durum satırında toplam · kullanılan (%) · boş. Sorgu "son cevap" panelini değiştirmez; kullanıcının komutları kartın komut sırasında bekler, reddedilmez. |
| G34.2 | Boyut yazısı ile doluluk çubuğu üst üste binmemeli (hata düzeltmesi) | Karşılandı | Neden: satır yüksekliği 20 px simgeden geliyordu; yazı satırın ortasına, çubuk satırın altına çizilince iç içe geçiyordu. Çözüm: liste simgeleri 20×24 (simge ortada, bozulmadan) → satırlar 24 px; çubuklu hücrede yazı çubuğun üstündeki alana ortalanır, çubuk satırın en altında. |

### G35 — Uygulama simgesi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G35.1 | Uygulamanın işini anlatan yeni simge | Karşılandı | "Çoklu terminal" konsepti, Matrix renkleri: koyu zeminde 2×2 kart ızgarası (uygulamanın ana ekranı); seçili kartta `>_` istemi, diğer kartlarda durum ışıkları (yeşil bağlı, sarı bağlanıyor, kırmızı koptu). `Vision.ico` 16–256 px 9 boy; kaynak `docs/simge.svg`, önizleme `docs/simge.png`. |

### G36 — Uygulama turu

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G36.1 | Uygulamayı açan kişiye özellikler bir öğretici turla gösterilmeli (renklendirme dahil her özellik) | Karşılandı | `Tour.cs`: ana pencerenin üzerinde yarı saydam katman (`TourOverlay`, %62 opaklık); vurgulanan bölüm katmanın `Region`'ında yuvarlatılmış boşluktur (altındaki denetim görünür ve kullanılabilir), vurgu rengiyle çerçevelenir. Yanında opak açıklama balonu (`TourBalloon`): adım sayacı, başlık, metin, isteğe bağlı çizim (durum renkleri, renklendirme örneği, kısayollar). Balon hedefin sağına / soluna / altına / üstüne, sığmazsa hedefin içine yerleşir; pencere taşınınca / boyutlanınca ve hedef değişince (250 ms) yeniden konumlanır. 21 adım (`MainForm.BuildTourSteps`): hoş geldiniz, sekmeler, Kart Ekle, Kart Gezgini (sağ tık, arama), çoklu bağlanma, kart ekranları + durum renkleri, kart başlığı, komut satırı (geçmiş, Ctrl+R, otomatik tamamlama, bspBootChange), Sentinel / hızlı komut / reboot, renklendirme, akan çıktı, terminal ayarları, genel komut çubuğu, Kart Dosya Gezgini, dosya işlemleri + iki yönlü sürükle-bırak, Komut Kütüphanesi, Sentinel, temalar, yardım, kısayollar, bitiş. Gerekirse ilgili sekmeye geçer, bitince eski sekmeye döner; açık kart yoksa kart adımları ızgarayı gösterir ve not ekler. |
| G36.2 | Tur atlanabilmeli; "bir daha gösterme" seçilirse bir daha gösterilmemeli; Yardım'dan istendiğinde yeniden açılabilmeli | Karşılandı | Turu atla / Esc; ← → / Enter ile gezinme. `AppSettings.TourVersionSeen` < `CurrentTourVersion` (1) ise ilk açılışta (700 ms sonra) kendiliğinden açılır; sonuna kadar izlenirse ya da "Bir daha gösterme" işaretliyse sürüm kaydedilir. Yardım menüsünde "Uygulama turu". Tura yeni adımlar eklenince sürüm artırılarak yeniden gösterilebilir (etkileşimli tur ile 2'ye çıktı). Tur hiçbir ayarı / kartı değiştirmez. |
| G36.3 | Tur adım adım ve etkileşimli olmalı: kullanıcıya kart ekletilmeli, Kart Ekle penceresinin alanları anlatılmalı; karta bağlatılıp kart penceresinin tüm düğme ve göstergeleri tek tek anlatılmalı; kullanıcı kartına uygun bspBootChange / bootChange yazınca açılan pencerenin alanları anlatılmalı; renklendirme yaptırılmalı; o kartla Kart Dosya Gezgini'ne geçilip düğmeler, sağ tık (sil, bilgisayara kaydet, yeniden adlandır) ve iki yönlü sürükle-bırak anlatılmalı; Komut Kütüphanesi anlatılmalı; Sentinel için imajın Voltran olup olmadığı sorulmalı, evetse Sentinel başlatılıp sekme anlatılmalı, hayırsa genel hatlarıyla anlatılmalı | Karşılandı | `TourStep` yeni alanları: `Host` (adımın penceresi: ana pencere, açık Kart Ekle `AddCardsDialog.TourOpen`, kartın Boot ayarları `BootChangeDialog.TourOpenFor`; pencere yoksa adım atlanır), `WaitFor` + `Task` (kullanıcı işlemi beklenir, balonda "▶ Şimdi siz: …"; koşul yanlıştan doğruya dönünce tur 250 ms içinde kendiliğinden ilerler, girişte zaten doğruysa normal İleri; "Bu adımı geç" ile atlanır), `Available` (ör. bağlı kart yoksa bağlantı gerektiren adımlar ve görünmeyen bölümler atlanır), `Choices` / `Id` / `NextId` (Voltran sorusu: Evet → başlatma + Sentinel sekmesi, Hayır → genel anlatım, iki dal "Temalar"da birleşir). Geri gidilen adımlar yığında tutulur (dallanma ve atlanan adımlarla uyumlu). Adımın penceresi değişince katman ve balon o pencereye bağlı olarak yeniden oluşturulur (kalıcı Kart Ekle penceresi açıkken önceden açılmış pencereler devre dışı kalır); açılan pencerelerde balon ekranın çalışma alanında pencerenin yanına, anlatılan alanın hizasına yerleşir. Balon etkinleşmeden açılır, açık sağ tık menülerini kapatmaz. Beklenen işlemler: Kart Ekle penceresinin açılması / kapanması, ızgarada bağlı bir kart penceresi (anlatılan kart olarak seçilir), Boot ayarları penceresinin açılması / kapanması, kartın renklendirme kuralı sayısının artması (`CardSession.Highlights.Count`), Dosya Gezgini sağ tık menüsünün açılması, Sentinel durumunun `Running` olması. Dosya Gezgini adımları anlatılan kartı `ShowCard` ile seçer; Sentinel sekmesi `SelectCard` ile o kartın sayfasını açar. Denetim erişimleri `TourAccessors.cs` ve `CardView.Tour.cs`'te (yalnız okuma). Yaklaşık 65 adım; gizli bölümler (ör. salt okunur kütüphanede düzenleme düğmeleri) atlanır. |

### G37 — Sentinel sayfası yenileme seçimi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G37.1 | Sentinel sayfasındaki yenileme aralığı seçimi kaldırılmalı; her zaman sayfanın kendi yenilemesi kullanılmalı | Karşılandı | `SentinelView`: `_reloadCombo` ve 2 / 5 / 10 / 30 sn seçenekleri kaldırıldı; Edge görüntüleyicide Vision sayfayı yeniden yüklemez (sayfa kendini yeniler). Yalnız eski görüntüleyiciye düşülünce (WebView2 Runtime yok; betikler çalışmayabilir) sayfa önceki gibi 2 sn'de bir yeniden yüklenir (`LegacyReloadSeconds`). Elle Yenile (F5) sürer. |

### G9 — Dosya Gezgini işlemleri (planlanan)

| No | Gereksinim | Durum | Not |
|---|---|---|---|
| G9.1 | Bilgisayardan karta sürükle-bırak ile dosya / klasör yükleme | Kısmen | FTP sunucusuna gönderim + kartta `copy` (klasörde `mkdir` + dosya başına `copy`); FTP ayarları penceresi. Gerçek FTP sunucusu ve kartla doğrulanmalı. |
| G9.2 | Kart içinde sürükleyerek taşıma / kopyalama | Karşılandı | Klasöre ya da yol çubuğuna bırakma: `mv`; Ctrl ile `copy` / `xcopy`. |
| G9.3 | Metin dosyalarını (txt, xml…) görüntüleme | Karşılandı | `copy "dosya"` çıktısı görüntüleyicide; bilgisayara kaydetme. ASCII dışı karakterler `?` görünebilir. |
| G9.4 | Klasör oluşturma / silme, yeniden adlandırma | Karşılandı | `mkdir`, `rmdir`, `xdelete`, `rm`, `mv`; silmede onay. |
| G9.5 | Kartın imajında olmayan komut için kullanıcıya uyarı | Karşılandı | `undefined symbol` yakalanır, turuncu uyarı; kartta değişiklik olmaz; beklemeden bildirilir. |
| G9.6 | Listelemede `ll` kullanımı (tür / boyut / tarih) | Kısmen | "Listing Directory …" başlığı atlanır; `ll` yoksa `ls`. Gerçek kart çıktısıyla doğrulanmalı. |

### G38 — Otomatik sürüm yayını

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G38.1 | main'e her birleştirmede küçük sürüm artırılmalı ve o sürüm GitHub'da etiket + release olarak kendiliğinden yayınlanmalı | Karşılandı | `build-exe.yml`, "Resmî sürümü yayınla" adımı: main derlemesinde `AppInfo.cs`'teki `Version` okunur, `vX.Y` release'i yoksa derlenen commit'e etiketlenip `Vision.zip` ekiyle oluşturulur (kurulum notu + GitHub'ın otomatik değişiklik listesi, "latest"). Release zaten varsa dokunulmaz, uyarı yazılır (sürüm artırılmamış demektir). Dal ön sürümleri (`build-<dal>`) eskisi gibi sürer. |
| G38.3 | Her sürümün derlenmiş, kullanılabilir hali depoda da tutulmalı (depo başka yere aktarıldığında sürümler izlenebilsin) | Karşılandı | `release/vX.Y/Vision/` (zipten çıkarılmış: `Vision.exe` + `lib/`; v1.1 ile başladı) ve `release/README.md` (kullanım + sürüm tablosu). Paket, birleştirmeden önce dalın son kod derlemesinden (aynı kod main'de derlenir) alınıp açılarak aynı dala eklenir. `release/**` derlemeyi tetiklemez (`paths-ignore`); `.gitattributes`'ta `*.exe` / `*.dll` / `*.zip binary`. |
| G38.2 | Önceden birleştirilmiş bir commit (ör. v1.0) sonradan yayınlanabilmeli | Karşılandı | Actions → "Vision exe derle" → Run workflow, `release_ref` = commit SHA'sı: o commit derlenir ve sürümü yayınlanır (ön sürüm güncellenmez). |

### G39 — Büyük dosya yükleme

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G39.1 | Büyük dosya (ör. 2,5 GB) karta yüklenirken Vision kartın kopyalamayı bitirmesini beklemeli; sabit süre dolup kart hâlâ kopyalarken "cevap vermedi" denmemeli | Karşılandı | `FileExplorerLogic.CopyTimeoutSec(bayt)`: kartta `copy` için bekleme 300 sn + boyut / (1 MB/s), en fazla 24 saat (önceden sabit 300 sn sessizlik). Tek dosya yükleme, klasör yüklemedeki her dosya (`FolderPlan.Sizes`) ve karttan bilgisayara almadaki FTP'ye geçici kopya bu süreyi kullanır. Durum metninde boyut ve 100 MB üstünde "dakikalar sürebilir" notu. |
| G39.2 | Hedef diskte yer yoksa yükleme başlamadan bildirilmeli | Karşılandı | `CheckFreeSpaceAsync`: hedef `/ata…` / `/bd…` ise FTP'ye göndermeden önce `dosFsShow` ile güncel boş alan okunur (son cevap panelini değiştirmez); toplam boyut (klasörler dahil, planlar bir kez çıkarılıp yeniden kullanılır) boş alan + üzerine yazılacak aynı adlı dosyaların boyutundan büyükse "yeterli boş alan yok … Hiçbir şey gönderilmedi". Boş alan okunamazsa engellenmez. |
| G39.3 | FAT diskine 4 GB ve üstü dosya gönderilmemeli | Karşılandı | `/ata…` / `/bd…` hedefinde 4 GB ve üstü dosya (klasörde en büyük dosya) FTP'ye gönderilmeden atlanır: "4 GB ve üstü dosyalar kartın diskine (FAT) yazılamaz". |
| G39.4 | Kartın tek seferde kopyalayamadığı büyük dosyalar (2,5 GB'ta telnet koptu; 700 MB sorunsuz) yine de aktarılabilmeli | Karşılandı | 1 GB'tan büyük dosyalar `UploadInPartsAsync` ile aktarılır: kartta `open "hedef", 0x601, 0644` (O_WRONLY \| O_CREAT \| O_TRUNC) ile dönen tanıtıcı alınır; dosya 256 MB'lık parçalar halinde (`IFileUploader.UploadFileRangeAsync`, `ad.partN`) FTP'ye gönderilir; her parça kartta `open "parça", 0, 0` ile açılıp `copyStreams parça, hedef` ile hedefin sonuna eklenir, `close` ile kapatılır, FTP'den silinir. Sonunda hedef kapatılır. Kartın FTP sürücüsü hiçbir zaman parçadan büyük dosya açmaz. Hata olursa hedef kapatılıp `rm` ile yarım dosya silinir; bağlantı koptuysa sonraki öğeler denenmez. Parçalar FTP kopyaları listesine eklenir (yarım kalırsa "FTP kopyalarını sil" ile temizlenir). |

### G40 — Yükleme ilerleme penceresi

| No | Gereksinim | Durum | Nasıl karşılandı |
|---|---|---|---|
| G40.1 | Karta yüklemede (özellikle parça parça aktarımda) işlemin ne kadarının tamamlandığı / ne kadar kaldığı bir pencerede görülmeli | Karşılandı | `UploadProgress.cs`: `TransferProgress` (yükleme yazar, pencere 300 ms'de bir okur; kilitli) ve `UploadProgressDialog`. Toplam = (FTP'ye gönderilen + kartta kopyalanan) / (2 × toplam). FTP'ye gönderim bayt bayt izlenir (`IFileUploader.UploadFileAsync / UploadFileRangeAsync / UploadDirectoryAsync` için isteğe bağlı `Action<long> sent`; 1 MB'lık bloklarla); kartta kopyalama (copy / copyStreams) ara bilgi vermediğinden adım çubuğu hareketli, parça / dosya bitince toplam ilerler. Öğe bitince (başarılı / başarısız) baytları tamamlanmış sayılır. Kalan süre: geçen süre × (1 − oran) / oran (oran %2'yi ve 5 sn'yi geçince). Pencere kalıcı (modal) değil: `Watch` boyut hesaplanınca ≥ 50 MB ya da ≥ 20 dosyada açar, bitince kapatır; "Arka planda sürdür" kapatır, "İptal" (`CancelRequested`) öğe / klasördeki dosya / parça başında denetlenir. |

## Sürüm notları

İlk resmî yayın **1.0**'dır (G1–G38). Bundan sonra `main`'e her birleştirmede küçük sürüm bir artırılır (1.1, 1.2, …); yayın GitHub'da `vX.Y` etiketiyle, `Vision.zip` ekiyle, main derlemesinden kendiliğinden yapılır (G38); derlenmiş hali ayrıca depoda `release/vX.Y/Vision/` klasöründe (zipten çıkarılmış) tutulur.

| Sürüm | Değişiklikler |
|---|---|
| 1.0 | Temel telnet terminali; grup tabanlı kart yönetimi; çoklu kart ızgarası; bağlantı / görünüm ayrımı; bağlantı süresi; kaydırma koruması ve yeni satır göstergesi; çift satır düzeltmesi; komut geçmişi; genel komut çubuğu; gri görünüm ve kopma uyarısı; Türkçe hata mesajları; Kart Dosya Gezgini; periyodik bağlantı izleme ve otomatik yeniden bağlanma; 3 sütunlu `devs` desteği; Türkçe satır sonu seçenekleri; Yardım bölümü; Dosya Gezgini işlemleri (görüntüle, yeni klasör, yeniden adlandır, taşı / kopyala, sil), `ll` ile listeleme, eksik komut uyarısı ve FTP üzerinden karta yükleme; ayarlar `Konfig` klasöründe; Yardım menüsü; Komut Kütüphanesi; geri bildirim iyileştirmeleri (G11); salt okunur "Birlikte aç", simgeli düğmeler, sade kenar çubuğu düğmesi ve kontrast düzeltmeleri (G12); Komut Kütüphanesi modülleri ve yenilenen görünüm (G13); tüm arayüzde tek tip koyu tema kontrolleri, alt bantlı pencereler, çizilmiş simgeler ve uygulama simgesi (G14); uygulamanın adı Vision oldu ve yeni simgesi tasarlandı (G15); Dosya Gezgini yalnız içine girilebilen cihazları gösterir (G16); kart başına otomatik FTP bilgisi ve düzeltilmiş yükleme, FTP kopyalarını sil düğmesi, hızlı komut ve reboot düğmeleri, ayrı dönüş değerleri paneli, ortak yakınlaştırma ve anlık terminal kaydı (G17); tüm kartları bir kerede temizleyen genel silgi, 40 sn bağlantı zaman aşımı, ayarların yeniden `%APPDATA%`'da tutulması, 24 kart sınırı ve FTP bilgisinin `version` komutunun boot satırından alınması, klasör yükleme düzeltmesi, açık tema (G18); kalıcı ve ortak komut geçmişi, geçmiş penceresi ve geriye doğru arama (Ctrl+R), kart ekranında sağ tıkla renklendirme (G19); hazır VxWorks 6.4 komut kütüphanesi ve komutu karta aktarma (G20); kütüphane gömülü ve salt okunur, Yardım / tema sekme şeridinde, EFI koruması (G21); FTP aktarımları Vision çalışma klasörü üzerinden (G22); kartın FTP yolu her zaman karttan, kaydedilmeden (G23); akış izlenirken yatay kayma düzeltmesi (G24); hazır temalar ve tema özelleştirme (G25); uzun çıktıda kaydırma düzeltmesi (G26); bspBootChange / bootChange için Boot ayarları penceresi, Dosya Gezgini cihaz listesinin ön denetimsiz hızlı açılması (G27); Sentinel düğmesi ve kartın sağlık sayfasını uygulama içinde gösteren Sentinel sekmesi (G28); kart eklerken Kart 1'in başlangıç IP'siyle dolması, grup adını değiştirme (G29); 14 popüler editör teması, "Dark Modern" adı, DLL'lerin `lib` klasörüne alınması (G30); karttan bilgisayara sürükle-bırak ve "Bilgisayara kaydet…" (G31); yardım belgeleri yalnız exe'de, pakette Yardim klasörü yok (G32); başlıkta yalnız "Vision", Gereksinimler uygulamadan kaldırıldı, zaman damgası varsayılan açık, menüler tema değişimine uyar, Dosya Gezgini'nde yalnız bağlı kartlar (G33); disk boyutları (G34); yeni uygulama simgesi (G35); etkileşimli uygulama turu (G36); Sentinel yenileme seçimi kaldırıldı (G37); main derlemesinden otomatik sürüm yayını (G38). |
| 1.1 | Büyük dosya yükleme: kartta kopyalama süresi dosya boyutuna göre, yüklemeden önce boş alan denetimi, FAT 4 GB sınırı, 1 GB üstü dosyaların parça parça aktarımı (G39); yükleme ilerleme penceresi (G40). |
