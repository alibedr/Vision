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
| Hedef | .NET Framework 4.6.1, Windows Forms, klasik (SDK olmayan) `.csproj`. |
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
| G8.2 | Yardım bölümü: kullanım kılavuzu ve gereksinimler | Karşılandı | F1; belgeler `Yardim\` klasöründe (kodla birlikte), exe'ye de gömülü. |
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
| G16.1 | Cihazlar listesinde yalnız içine girilebilen cihazlar gösterilmeli | Karşılandı | `devs`ten sonra her cihaz listeleme komutuyla (`ll`, yoksa `ls`) denenir; dönüş değeri 0 olanlar gösterilir, diğerleri (ör. `/null`, `/tyCo/0`) gizlenir. Gizlenenler durum satırında adlarıyla belirtilir. Sonuç önbelleğe alınır (Cihazlar'a dönüşte kart yeniden denenmez); **Yenile** (F5) ve kart değişimi yeniden denetler. Denetim komutları terminale yazılmaz. Hiçbir cihaz girilemiyorsa bu açıkça yazılır; kartta listeleme komutu hiç yoksa süzme yapılmaz ve tümü gösterilir. |

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

### G9 — Dosya Gezgini işlemleri (planlanan)

| No | Gereksinim | Durum | Not |
|---|---|---|---|
| G9.1 | Bilgisayardan karta sürükle-bırak ile dosya / klasör yükleme | Kısmen | FTP sunucusuna gönderim + kartta `copy` (klasörde `mkdir` + dosya başına `copy`); FTP ayarları penceresi. Gerçek FTP sunucusu ve kartla doğrulanmalı. |
| G9.2 | Kart içinde sürükleyerek taşıma / kopyalama | Karşılandı | Klasöre ya da yol çubuğuna bırakma: `mv`; Ctrl ile `copy` / `xcopy`. |
| G9.3 | Metin dosyalarını (txt, xml…) görüntüleme | Karşılandı | `copy "dosya"` çıktısı görüntüleyicide; bilgisayara kaydetme. ASCII dışı karakterler `?` görünebilir. |
| G9.4 | Klasör oluşturma / silme, yeniden adlandırma | Karşılandı | `mkdir`, `rmdir`, `xdelete`, `rm`, `mv`; silmede onay. |
| G9.5 | Kartın imajında olmayan komut için kullanıcıya uyarı | Karşılandı | `undefined symbol` yakalanır, turuncu uyarı; kartta değişiklik olmaz; beklemeden bildirilir. |
| G9.6 | Listelemede `ll` kullanımı (tür / boyut / tarih) | Kısmen | "Listing Directory …" başlığı atlanır; `ll` yoksa `ls`. Gerçek kart çıktısıyla doğrulanmalı. |

## Sürüm notları

| Sürüm | Değişiklikler |
|---|---|
| 1.0 | Temel telnet terminali; grup tabanlı kart yönetimi; çoklu kart ızgarası; bağlantı / görünüm ayrımı; bağlantı süresi; kaydırma koruması ve yeni satır göstergesi; çift satır düzeltmesi; komut geçmişi; genel komut çubuğu; gri görünüm ve kopma uyarısı; Türkçe hata mesajları; Kart Dosya Gezgini; periyodik bağlantı izleme ve otomatik yeniden bağlanma; 3 sütunlu `devs` desteği; Türkçe satır sonu seçenekleri; Yardım bölümü; Dosya Gezgini işlemleri (görüntüle, yeni klasör, yeniden adlandır, taşı / kopyala, sil), `ll` ile listeleme, eksik komut uyarısı ve FTP üzerinden karta yükleme; ayarlar `Konfig` klasöründe; Yardım menüsü; Komut Kütüphanesi; geri bildirim iyileştirmeleri (G11); salt okunur "Birlikte aç", simgeli düğmeler, sade kenar çubuğu düğmesi ve kontrast düzeltmeleri (G12); Komut Kütüphanesi modülleri ve yenilenen görünüm (G13); tüm arayüzde tek tip koyu tema kontrolleri, alt bantlı pencereler, çizilmiş simgeler ve uygulama simgesi (G14); uygulamanın adı Vision oldu ve yeni simgesi tasarlandı (G15); Dosya Gezgini yalnız içine girilebilen cihazları gösterir (G16); kart başına otomatik FTP bilgisi ve düzeltilmiş yükleme, FTP kopyalarını sil düğmesi, hızlı komut ve reboot düğmeleri, ayrı dönüş değerleri paneli, ortak yakınlaştırma ve anlık terminal kaydı (G17); tüm kartları bir kerede temizleyen genel silgi, 40 sn bağlantı zaman aşımı, ayarların yeniden `%APPDATA%`'da tutulması, 24 kart sınırı ve FTP bilgisinin `version` komutunun boot satırından alınması, klasör yükleme düzeltmesi, açık tema (G18); kalıcı ve ortak komut geçmişi, geçmiş penceresi ve geriye doğru arama (Ctrl+R), kart ekranında sağ tıkla renklendirme (G19). |
