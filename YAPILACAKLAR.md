# Yapılacaklar

Kullanıcılardan gelen istekler ve henüz yapılmamış fikirler burada toplanır. Uygulamanın içinde görünmez; yalnız repoda
durur.

**Nasıl kullanılır**

- Gelen her istek kısa bir notla, **kaynağı** (kimden / hangi sürümü kullanırken) ve **tarihiyle** eklenir.
- **Hatalar** (bir şey bozuk ya da işi engelliyor) beklemeden düzeltilip çıkarılır.
- **İyileştirmeler** birbiriyle ilgili olanlar bir araya gelecek şekilde gruplanır. Birkaç hafta toplandıktan sonra
  birlikte öncelik verilir; bir grup (3–5 madde, bir oturumda denenebilecek büyüklükte) bir sürüm olarak yapılır.
- Bir madde yapılınca buradan silinir; ne yapıldığı `Yardim/Gereksinimler.md`'ye (G numarası) ve sürüm notlarına yazılır.
- Bilerek yapılmamasına karar verilenler gerekçesiyle "Yapılmayacaklar"da kalır; aynı istek yeniden gelirse oradan bakılır.

---

## Hatalar

- [ ] **Kart Dosya Gezgini'nde bazı .txt dosyaları çift tıklayınca açılmıyor**
  - *Kaynak:* 1.3 kullanıcıları · 19.11.2026
  - *Nasıl çalışıyor:* Çift tık → dosya kartta geçici bir adla (`_tkt_xxxxxxxx_<ad>`) FTP klasörüne kopyalanır → bilgisayara
    indirilir → boyutu doğrulanır → salt okunur kopya Windows'un varsayılan uygulamasıyla açılır. Hangi adımda takıldığı
    henüz bilinmiyor.
  - *Muhtemel nedenler:* Dosya adı (uzun ad — geçici ad önüne 14 karakter ekliyor —, boşluk, Türkçe / özel karakter);
    kartın `ll` çıktısında dosyanın boyutu ya da türü yanlış okunması (boyut doğrulaması başarısız olur ya da klasör
    sanılır); 0 baytlık dosya; FTP bilgisi alınamayınca "Metin olarak aç" yoluna düşülmesi.
  - *Gereken bilgi:* Açılmayan dosyanın adı, kartta bulunduğu klasör ve boyutu; çift tıklayınca ne olduğu (hiçbir şey mi,
    şeritte bir hata mı — metni); `logs\hatalar.log`; mümkünse o klasörde kartta `ll` çıktısı.
  - *Öncelik:* Hata — gruplanmayı beklemeden, bilgi gelince düzeltilir.

---

## Planlanan gruplar

### Dosya aktarımı (1.11 adayı)

İki istek de Kart Dosya Gezgini'ndeki yükleme akışını değiştiriyor; birlikte tasarlanmalı.

- [ ] **Kart Dosya Gezgini'nde Ctrl+C / Ctrl+V ile dosya aktarımı**
  - *Kaynak:* 1.3 kullanıcıları · 19.11.2026
  - *İstek:* Sürükle-bırak dışında, Windows Gezgini'nde kopyalanan dosya / klasör Vision'da Ctrl+V ile karta yüklensin;
    kartta seçilen dosyalar Ctrl+C ile kopyalanıp bilgisayarda istenen klasöre Ctrl+V ile alınsın.
  - *Not:* Karttan alma yönünde dosyanın önce karttan indirilmesi gerekir; Windows ancak ondan sonra yapıştırabilir.
    Büyük dosyada indirme ilerlemesi gösterilmeli.
- [ ] **Aynı dosyaları birden çok karta tek seferde yükleme**
  - *Kaynak:* 1.3 kullanıcıları · 19.11.2026
  - *İstek:* 5, 8 … kartı seçip dosya / klasörü bir kez bırakınca hepsine yüklensin; tek tek uğraşılmasın.
  - *Not:* Dosya FTP'ye bir kez gönderilir, her kart kendi kopyasını oradan alır (tek tek yüklemekten çok daha hızlı).
    İlerleme ve sonuç kart kart gösterilmeli; bir kartta sorun çıkarsa diğerleri devam etmeli; her kartın olay
    günlüğüne yazılmalı.
  - *Durum:* 1.10.1'den beri aynı dosya / klasör tek tek aynı anda birden çok karta atılabiliyor (FTP'de her yüklemeye
    özel geçici ad, G60). Kalan fayda: büyük dosyanın FTP'ye bir kez gitmesi ve tek bırakma. Kullanıcılara 1.10.1
    verildikten sonra hâlâ istenip istenmediği sorulacak.

### Terminalde kolaylık

İkisi de komut satırı / terminal kullanımını hızlandırıyor; birlikte yapılabilir.

- [ ] **Terminalde arama (Ctrl+F)**
  - *Kaynak:* Ali · 20.11.2026
  - *İstek:* Telnet ve seri terminalde metin arama: eşleşenler vurgulansın, Enter / F3 ile sonrakine, Shift+F3 ile
    öncekine gidilsin. Bugün Ctrl+F yalnız Komut Kütüphanesi'nde arıyor.
  - *Not:* Ctrl+F terminal odaktayken terminalde, Komut Kütüphanesi'ndeyken orada aramalı. Seri pencerede Ctrl+F karta
    da gidebilir (TeraTerm'de yok, kartta anlamı yok); seri tarafta arama kısayolu ayrıca düşünülmeli. Ekran (BIOS)
    görünümünde arama gerekmez.
- [ ] **Komut Kütüphanesi'ndeki komutlar otomatik tamamlamada**
  - *Kaynak:* Ali · 20.11.2026
  - *İstek:* Otomatik tamamlama bugün yalnız komut geçmişinden öneriyor; Vision'ı yeni kullanan birinin geçmişi boş
    olduğu için hiç öneri çıkmıyor. Kütüphanedeki komutlar (yazılışlarıyla) da önerilsin; yeni başlayan biri komut
    yazmaya başlayınca kütüphaneden tamamlansın.
  - *Not:* Önce geçmiş (kişinin kendi kullandığı biçim), yoksa kütüphane. Kütüphane önerisi soluk metinde ayırt
    edilebilir olmalı (ör. yanında kısa açıklama). Seri komut satırında da geçerli olsun mu, karar verilecek.

### Altyapı

- [ ] **Yeni sürüm bildirimi**
  - *Kaynak:* Ali · 20.11.2026
  - *İstek:* Herkes Vision'ı iç ağ klasöründen kendi bilgisayarına kopyalıyor; eski sürümle çalışan kalabiliyor. Vision
    açılışta yeni sürüm olup olmadığına bakıp haber versin ("Vision 1.11 çıktı — klasörü aç").
  - *Not:* Bakılacak yer ayarlardan (iç ağ klasörünün yolu; boşsa kapalı). Klasöre ulaşılamazsa sessiz geçilir, açılışı
    geciktirmez. Kendiliğinden güncelleme yok; yalnız bildirim. Sürüm, klasördeki `Vision.exe`'nin dosya sürümünden
    (1.10.1.0) okunur.
- [ ] **Çok kartlı kullanımda performans**
  - *Kaynak:* Kullanıcı sorusu (tek kartta yük yok, Görev Yöneticisi'nde %0,1–0,2) · 20.11.2026
  - *İstek:* Çok kart aynı anda log basınca ekran çizimi ağırlaşmasın.
  - *Not:* Kodda bulunanlar: (1) yeni veri gelen her kartın terminali 40 ms'de bir baştan çiziliyor; (2) terminal satır
    sınırına ulaşınca her eklemede eski satırlar siliniyor (geçmişte %10 pay var, ekranda yok). Öneriler: ekranda da %10
    pay; pencere küçültülmüşken çizmemek; görünmeyen kartları daha seyrek çizmek; Sentinel sekmesi görünmüyorken
    sayfayı askıya almak. Önce 20 kartlık sahte log akışıyla ölçülmeli.

### Küçük işler

Herhangi bir sürüme eklenebilir.

- [ ] **Seri sekmesinin adı "Seri Kanal" olsun**
  - *Kaynak:* 1.3 kullanıcıları · 19.11.2026
  - *Not:* Sekme adı, ipuçları, tur ve kılavuz birlikte güncellenir.

---

## Fikirler (henüz istenmedi, değerlendirilecek)

- **Break sinyali (seri):** TeraTerm'deki Control → Send break (Alt+B). Bazı kartlarda bootloader'a düşmek için
  kullanılır. Kullanılıyor mu diye soruldu, cevap bekleniyor.
- **Gerçek BIOS kaydıyla test:** TeraTerm'in ikili (binary) log kaydıyla kartın BIOS / boot manager çıktısı alınıp seri
  Ekran görünümünün testlerine eklensin.
- **Gerçek kart çıktılarıyla test:** bspBootChange, `ll` gibi komutların gerçek kart çıktıları testlere örnek olarak
  eklensin.
- **Seri pencerenin tamamı tek terminal:** Günlük / Ekran ayrımı olmadan, geçmişi tutan tek bir terminal yüzeyi
  (TeraTerm gibi). Bugünkü Ekran görünümü yetmezse değerlendirilecek; büyük iş.
- **Reboot'u kimin attığını ayırt etme:** Kartın kendi yeniden başlamasıyla başkasının attığı reboot'u ayırmak için kartta
  bir iz dosyası.
- **FTP parolasını şifreli saklama:** Boot ayarı kayıtlarındaki FTP parolası bugün `config.xml`'de düz metin; Windows'un
  kullanıcıya özel şifrelemesiyle (DPAPI) saklanabilir. Not: kayıtların başka bilgisayara taşınmasını zorlaştırır.
- **İç ağda derleme betiği:** GitHub Actions olmadan iç ağda exe'yi üretecek bir betik.

---

## Yapılmayacaklar (karar verildi)

- **Kart pencerelerini taşıma / boyutlandırma** — 1.9 beta geri bildirimi. Serbest yerleşim düzen saklama, çakışma ve
  çözünürlük sorunları getiriyor; kartlar grup ve IP sırasıyla ızgarada kalıyor, odaklanmak için Büyüt. (G58.5)
- **Yardım menüsünde ayar klasörü** — ayarlar uygulamadan değiştirilir; elle düzenlenen `config.xml` bozulup kart listesi
  boş açılabilir. (G58.6)
- **Ctrl+Shift+L tema kısayolu** — kaldırıldı; tema menüden seçilir. (G58.8)
- **Seri pencerede sağ tıkla yapıştırma (TeraTerm gibi)** — sağ tık menü açmaya devam ediyor (Telnet tarafıyla aynı,
  yanlışlıkla yapıştırmayı önler); yapıştırma Ctrl+V / Shift+Insert. (G59.9)
- **Seri pencerede hızlı tuşlar menüsü** (F2, Del, F10 … düğmeleri) — gereksiz düğme kalabalığı; tuşlar klavyeden karta
  gidiyor. (G59.5)
- **Backspace / Delete kodunun ayardan seçilmesi** — TeraTerm varsayılanlarıyla sabit (Backspace = BS, Delete = DEL).
  (G59.6)
