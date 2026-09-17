# Oyun içi kod denetimi ve düzeltmeler

İncelenen taban: `main`, **08649bcd00f01fcfad04c06f1e53d655bd27a5d2**.
Düzeltme dalı: `fix/gameplay-qa-audit`. Steam/pazarlama denetimi yok. Adım 3 ışık paketi bu dala dahil değil. Sahne, prefab, kapak, karakter boyu, el pozu, fırlatma hızı/spin değerleri değiştirilmedi.

Bu, kaynak kodu ve repodaki serialized içerik denetimidir. Unity Editor, ham video, profiler capture ve çalışan dört istemci yok: görüntü/ses kalitesi, gerçek FPS, paket kaybı ve oyuncu hissi **denetlenemedi**. Kodda bir koruma görülmesi, çok oyunculu testin geçtiği anlamına gelmez.

## En büyük fark: brief ile depodaki oyun aynı içerik düzeyinde değil

`ProjectSettings/EditorBuildSettings.asset` içinde etkin sahne `Assets/Settings/ne.unity`. Bu sahnenin `BookSpawner.bookTypes` listesinde **15** referans, `copiesPerBook = 10` var: **150 kitap**. Repoda BookData script GUID'sine bağlı **15 asset**, ID **0–14**, BrandID **0** bulundu. `BrandConfig` ise **22** yayıncı tanımlıyor: 20×15 + 2×30 = 360 tür kapasitesi. Brief 24 yayıncı / 360 gerçek kapak / 3.600 nesne diyordu.

Bu sonuç yalnız Git snapshot'ı için geçerli; bilgisayarındaki kaydedilmemiş/yüklenmemiş içerik hakkında çıkarım değil. Mevcut marka numaralarını 24'e körlemesine dönüştürmedim: bu, kitap/raf eşleşmesini bozardı. Kapak veya karakter icat edip eksik içeriği tamamlanmış saymadım.

## Bulgular, önem ve çözüm

Süreler kişi-gün cinsinden yaklaşık mühendislik + QA bütçesidir; iki kişinin işi bağımsız bölebildiği ölçüde paralelleşir. `Düzeltildi` kod değişikliği anlamındadır, Unity'de doğrulanmış demek değildir. BLOCKER burada çıkış onayını durduran durumdur; inceleme yüzdesi tahmini değildir.

| # | Kusur | Başlık | Önem | Oyuncuya etkisi | Çözüm / durum | Süre |
|---|---|---|---|---|---|---|
| 1 | Depodaki build 3.600 yerine 150 kitap ve tek BookData markasıyla açılıyor | İçerik / tamamlanabilirlik | BLOCKER, brief kapsamı için | Vaat edilen sınıflandırma ve iş yükü bu build'de yok; 150 nesne testi 3.600 testi değildir | **Açık.** Gerçek BookData kataloğunu BookSpawner'a bağla, 24 yayıncı–raf eşlemesini doğrula; asset'ler repoda yokken otomatik doldurulmadı | İçerik hazırsa 1–2 gün entegrasyon; üretim süresi verisiz |
| 2 | Kalıcı oturum kaydı ve yükleme yolu bulunmadı; host kapanınca oturum sonlanıyor | Kayıt / co-op | BLOCKER, uzun oturum hedefi için | Uzun düzenleme işi çıkışta kaybolur | **Açık.** Kalıcı kopya kimliği + host snapshot + sürümlü/atomik dosya kaydı + restore/rejoin testi gerekir; bu yamada yapılmış gibi sunulmadı. Host ayrılma düğmesi artık ilerleme kaybını açıkça söylüyor | 3–5 gün, kayıt tasarımı netleştikten sonra; host migration dahil değil |
| 3 | RaycastAll engelleri atlayıp duvar arkasındaki kitap/rafı seçiyor; sunucu yalnız yakınlık ölçüyor | Etkileşim / netcode | MAJOR | Duvar arkasından toplama/yerleştirme, istemci ile dünya kuralları tutarsız | **Düzeltildi.** PlayerInteraction engelde durur; NetworkBook PickUpRpc/PlaceRpc, GameplayPhysics.CanReach ile ayrıca görünürlük doğrular; etkileşim maskesinin dışındaki duvar da engeldir | Kod hazır; Unity regresyon 0.5 gün |
| 4 | StopAllCoroutines, çarpışmayı geri açan coroutine'i de öldürüyor | Fizik / yaşam döngüsü | MAJOR | Fırlatılan kitap oyuncuyla kalıcı olarak çarpışmaz | **Düzeltildi.** Temizlik el animasyonlarından bağımsız Update kuyruğu; uyku/kinematic veya 2 saniyelik üst sınır + ayarlı gecikme sonrası geri açılır; disable/reset de temizler | Kod hazır; regresyon 0.5 gün |
| 5 | Player collider listesine elindeki diğer kitapların collider'ları da giriyor | Fizik / performans | MAJOR | Yanlış book–book IgnoreCollision çiftleri ve her bırakışta bütün kitapları tarama | **Düzeltildi.** Yalnız gerçek oyuncu collider'ları filtreleniyor; yanlış çiftler hiç üretilmediği için O(3.600) global geri-açma taraması kaldırıldı | #4 ile birlikte |
| 6 | İptal edilmiş Relay isteği geç tamamlanıp transport'a yeni allocation yazabiliyor | Oturum | MAJOR | İptal → yeniden bağlan sırasında yeni oturum eski istek tarafından bozulabilir | **Düzeltildi.** Her async sınırdan sonra generation kontrolü; Disconnect/Destroy provider'ı da iptal eder; UGS sign-in devam ederken ikinci sign-in başlatılmaz | Kod hazır; gecikmeli internet senaryosu 0.5–1 gün |
| 7 | Concave MeshCollider için desteklenmeyen ClosestPoint sorgusu | Fizik / Console | MAJOR | Tekrarlayan uyarı, dinlenen kitabın zemin desteğinin yanlış değerlendirilmesi | **Düzeltildi.** Box/Sphere/Capsule/convex için ClosestPoint; concave yüzey için gerçek collider raycast'i; AABB'yi zemin kanıtı saymıyor | Kod hazır; raf/zemin regresyon 0.5 gün |
| 8 | Aynı BookItem, PlaceBook tekrar çağrısında birden fazla indekse kaydedilebiliyor | İlerleme / raf | MAJOR, yinelenen çağrıda | Tek fiziksel kitap birden çok sayılır | **Düzeltildi.** Hâlihazırda bir slota bağlı kitap ikinci kez kabul edilmiyor | Kod hazır; check eklendi |
| 9 | GameStats, BookID'yi katalog boyuna göre array index sanıyor | İlerleme | MAJOR, seyrek ID'li katalogda | Örneğin yalnız ID 45 ve 359 ile test edilince yerleştirmeler sayılmıyor | **Düzeltildi.** Gerçek ID anahtarlı dictionary; spawner gerçek ID listesini verir; negatif/tekrar ID başlatmadan reddedilir. Mevcut 0–14 katalogda bu hata görünmezdi | Kod hazır; check eklendi |
| 10 | Silinen kitap için raf sayacı ve HUD referansı kalabiliyor | Uç durum | MAJOR | Hayalet dolu slot veya MissingReference/NullReference | **Düzeltildi.** BookItem.OnDestroy slotu temizler; eldeki yok olmuş referanslar ayıklanır; HUD null kaydı atlar | Kod hazır; runtime destroy/despawn testi 0.5 gün |
| 11 | Recall eski frozen-support durumunu koruyor | Kurtarma fiziği | MAJOR, önceden donmuş kitapta | Kitap yeni konumdayken eski zeminin destek durumu kullanılabilir | **Düzeltildi.** Teleport önce SetHeld(false) ile eski temas/destek durumunu sıfırlar | Kod hazır; runtime recall testi |
| 12 | Domain/scene reload kapalı yeniden oturumda slot verisi kalabilir | Oturum | MAJOR, Editor yeniden başlatmada | Eski doluluk ve sahiplik yeni oturuma taşınabilir | **Düzeltildi.** StartPreparedSession, boş/kapalı oturumda slotları sıfırlayıp registry'yi kurar | Kod hazır; üç yeniden başlatma testi |
| 14 | Yanlış raf denemesi yalnız debug Console'a gidiyor | UI / öğrenme | MAJOR | Oyuncu tıklamanın neden çalışmadığını bilmiyor | **Düzeltildi.** GameHUD üzerinden dolu raf, yanlış yayıncı, ayrılmış kitap grubu ve alma/yerleştirme girdisi gösterilir; kitap yere atılmaz. Server-side yarışta ret mesajı hâlâ ayrı bir ağ geri bildirimi gerektirir | Kod hazır; HUD yerleşimi video ile doğrulanmalı |
| 16 | Yeniden kalkış koruma süresi ve bonk kapatma/host yönetim seçeneği görünmüyor | Co-op / troll kontrolü | MAJOR, açık lobi hedefinde | Oyuncu kalkar kalkmaz tekrar düşürülebilir | **Açık.** NetworkPlayerSetup'ta host yetkili grace süresi ve oda seçeneği eklenebilir; denetim yaması savaş/troll dengesini sessizce değiştirmedi | 0.5–1 gün + dört kişi testi |
| 17 | Son kullanıcı ayar ekranı / kalıcı tuş eşlemesi bulunmadı; Sanitizer Mouse0/Mouse1'i zorla atıyor | UX | MAJOR | Inspector'daki tuş değişimi runtime'da ezilir; oyuncu temel kontrollerini ayarlayamaz | **Açık.** Tek settings asset/verisi + kalıcı kullanıcı tercihleri, sanitizer yalnız eski şema göçünde; kontrol seçenekleri oynanabilir build'de doğrulanmalı | 1–2 gün, lokalizasyon hariç |
| 18 | Assets altında WAV/MP3/OGG/AIFF/FLAC dosyası yok; oyun scriptlerinde AudioSource/AudioClip eylem bağlantısı bulunmadı | Ses / döngü | MAJOR, kaynak snapshot'ında | Alma/dizme/bonk döngüsünün ses geri bildirimi kanıtlanamıyor | **Açık.** Kullanılacak lisanslı klipleri getir; yerleştirme sonucuna ve host bonk olayına event bağlantısı kur. Dinlenmemiş sesi kötü/iyi diye sınıflandırmadım | Klipler hazırsa 1–2 gün; ses üretimi hariç |
| 19 | Aynı frame'de pickup + drop/scroll iki coroutine/eylem başlatabiliyor | Girdi / fizik | MAJOR | Bırakılmış kitabın alma animasyonu devam edip kitabı ele geri sürükleyebilir | **Düzeltildi.** Frame başına tek envanter eylemi; Escape/focus kaybında el animasyonları güvenle iptal edilir ve eldeki kitaplar hizalanır | Kod hazır; aynı frame giriş ve alt+tab testi |
| 13 | Çapraz hareket normalize edilmiyor | Hareket | MINOR | Aynı hız ayarında çapraz yürüyüş yaklaşık %41 hızlı | **Düzeltildi.** HandleMove içinde Vector3.ClampMagnitude(...,1); analog düşük girdi korunur | Kod hazır |
| 15 | Her bakış güncellemesinde RaycastAll array allocation | Performans | MINOR | Sürekli GC üretimi | **Düzeltildi.** 128 hit NonAlloc tamponu; yalnız taşmada tam sorgu; yalnız geçerli hit aralığı sıralanır | Kod hazır; Profiler ölçümü bekliyor |

## On başlıkta denetim sonucu

1. **İlk 10 dakika:** Video olmadığı için akış/süre denetlenemedi. Kaynakta ağ bağlantısı için debug OnGUI arayüzü var. Raf ret açıklamalarını HUD'a bağladım; bunun yeterli öğretici olduğunu iddia etmiyorum.
2. **Çekirdek döngü:** PlayerInteraction alma ve raf animasyonlarını içeriyor; yanlış slota koyma başarısızsa kitabı elinde tutması doğru davranış ve korundu. Çarpışma temizliği ile sayaç bütünlüğü onarıldı. Ses, gecikme hissi ve el animasyonu görünümü denetlenemedi.
3. **Okunabilirlik:** 0.4 saniyede marka tanıma, kapak/sırt büyüklüğü ve renk ayrımı video/görüntü olmadan denetlenemedi. BookItem.DisplayName yalnız `Book N`; yayıncı/karakter metin isimleri BookData'da yok. Kitap ID'sini oyuncu için marka adı yerine koymak nihai çözüm değil.
4. **Görsel tutarlılık:** Bu yamada shader/ışık düzenlenmedi; yerel son sahne görülmedi, sızıntı veya outline düzelmiş kabul edilmedi. Henüz kurmadığın Adım 3 ayrı dalda kalıyor.
5. **Co-op:** Server-owned kitap ve atomik Claim mevcut: aynı kitabın çift sahibini engellemek için doğru temel. Client rigidbody'lerinin kinematic olması doğru. Mesafe + engel doğrulaması eklendi; iptal edilen Relay hazırlığı güvenli hale getirildi. Late join, RTT, paket kaybı, iki eşzamanlı pickup ve host kapanışı iki/dört gerçek peer ile hâlâ test edilmeli. Host migration yok; owner-authoritative player movement tam anti-cheat değildir.
6. **UI/UX:** Mevcut HUD TMP metni ve debug lobi incelendi; hatalı yerleştirme geri bildirimi eklendi. Çözünürlük, FOV, ses, rebind, dil ve altyazı için kullanıcı ayarı akışı bu kodda bulunmadı; video ile alternatif bir UI kanıtlanmadıkça hazır sayılmıyor.
7. **Ses:** Dosya ve bağlantı eksikleri yukarıda; işitsel kalite/tekrar denetlenemedi. Maskot şarkısı sesi/lisansı ve runtime davranışı bu snapshot'tan doğrulanamadı.
8. **Performans:** O(kitap sayısı) çarpışma geri-açma taraması ve sürekli hedefleme array allocation'ı kaldırıldı. CPU/GPU ms, draw call, SetPass ve 3.600 nesne ölçümü yok; GTX 1060 60 FPS onayı verilmiyor.
9. **Uç durumlar:** Tek kitap tekrar placement, seyrek ID, unsupported concave sorgu, animasyon iptali sonrası ignore çiftleri, relay cancel/retry kod düzeyinde kapsandı. Başlangıçta duvarın içinde duran collider, 207 km/sa atış, raf kenarında takılma, alt+tab ve yüksek gecikmede bonk Unity fizik/peer testi bekliyor; her duvar geçişi çözüldü denmiyor.
10. **Çıkış hazırlığı:** İstek gereği Steam kapsam dışı. Oyun içi onay; gerçek katalog, ilerleme kaydı ve aşağıdaki multiplayer/stress testleri tamamlanmadan verilmez.

## Bozmaman gerekenler

- NetworkBook.Claim host tarafında tek sahip atıyor; client'a kitap fiziği yetkisi verme.
- Raf yeri animasyondan önce host'ta rezerve ediliyor; animasyon bitmesini bekleyerek rezervasyon yapma.
- Yanlış raf denemesi kitabı yere atmıyor; bu davranış korundu.
- Kalibre edilmiş el pozları, fırlatma/spin ve karakter ölçüleri bu dalda değiştirilmedi.

## Testler — çalıştırılan ile eklenen ayrımı

**Bu ortamda çalıştırıldı:** Assets altındaki 57 C# dosyasının syntax-tree parse kontrolü (0 syntax error), `git diff --check`, etkin build sahnesi/katalog referanslarının statik sayımı. Bu kontroller Unity C# compilation veya gerçek fizik testi değildir.

**Unity için eklendi, burada çalıştırılmadı:**

`ComicShop → Tests → Run Gameplay QA Checks (Edit Mode)`

Araç geçici, kaydedilmeyen additive sahnede 16 assertion çalıştırır: seyrek ID, completion rollback, duplicate catalogue, çift raf kaydı, slot boşaltma, duvarla reach engelleme, açıklık, concave temas, yanlış book–book ignore çifti, el animasyon iptalinden sonra collision restore, disable temizliği, eski Relay generation reddi. Gerçek UGS bağlantısı kurmaz. Mevcut sahne içeriğini değiştirmez; test GameStats değerlerini geri yükler.

Mevcut `ComicShop → Tests → Run Multiplayer Regression (Play Mode)` host restart/pickup/release/raf/timeout akışlarını içerir. Shader assertion'ları eski `ComicShop/Book Cel` yoluna özgüdür; ToonLit dalıyla birleştirince bu görsel assertion'ı ayrı değerlendir, network hatası sanma. Test aktif oturum yokken, bir test sahne kopyasında çalıştırılmalıdır. `Audit Current Multiplayer State (Play Mode)` her iki peer'de state/slot/sayaç tutarlılığını kontrol eder.

### Runtime kabul matrisi

| Senaryo | Adımlar | Kabul koşulu |
|---|---|---|
| Duvar arkasından pickup/place | Kitap/rafı görünmeyen duvar arkasına, erişim mesafesine koy; istemciden dene | Alınmaz/yerleştirilmez; kitabın sahibi ve sayaç değişmez |
| Fırlat → hemen başka kitabı al | Önceki kitap hareket halindeyken yeni pickup/bonk/ESC ile el animasyonu iptal et | Eski kitap–oyuncu çarpışması en geç üst süre sonrası geri gelir |
| Yerleştir → al → yerleştir | Aynı kitabı 20 kez dolaştır; iki kişi aynı kitabı isteyin | Tek sahip, tek slot indeksi; sayacın net değişimi 1 |
| Relay iptal/retry | Yavaş ağda oda hazırlığını iptal et; Direct IP veya yeni Relay oturumu başlat; eski istek tamamlanmasını bekle | Eski hazırlık yeni transport/join code'u değiştirmez |
| Oturum yeniden başlatma | 3 kez host aç, kitap diz, ayrıl; Domain Reload kapalı ayrıca dene | Eski dolu slot/eldeki kitap/istatistik kalmaz |
| Destek ve recall | Concave zemin üstüne kitap bırak; desteği kaldır; kayıp kitabı recall et | Unsupported ClosestPoint uyarısı yok; eski destek konumu kullanılmaz |
| Host ayrılması | Dört kişi oynarken host kapat | Kayıt olmadığı açık; client menüye döner, process takılmaz; ilerleme korunur diye vaat edilmez |
| Gerçek stres | Gerçek 360 BookData ×10; dört farklı ağ/istemci; 30–60 dk, 150 ms RTT ve kontrollü paket kaybı | Sayaç/state denetimi geçer; 1080p CPU/GPU frame süreleri kaydedilir; 150-kitap ölçümü yerine kullanılmaz |

## Kalan iş ve karar

Kodda doğrulanmış hatalar bu yamada giderildi; Unity derleme ve yukarıdaki regresyonlar için **2–3 kişi-gün** ayır. Bu, eksik içerik/ses/kayıt üretimini içermez. Kayıt ve içerik hazır olmadan yalnız bu PR ile oyunu “çıkışa hazır” ilan etmek doğru değil. Eksik asset'lerin ve dört oyunculu testin süresi bilinmediği için toplam BLOCKER süresini veya dört haftaya kesin sığacağını uydurmuyorum.

İlk çalıştırmada yeni ayar aramana gerek yok: davranış düzeltmeleri otomatik. `GameHUD.hudText` atanmamışsa eklediğim ret açıklaması görünmez; mevcut HUD referansını kontrol et. Sonraki ayar işi önce gerçek katalog ve kayıt kapsamı, sonra ses bağlantıları olmalı; kitap hızını/elin hizasını yeniden bozarak bu açıklar kapanmaz.
