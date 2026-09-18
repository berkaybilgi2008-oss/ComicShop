# ComicShop — oyun içi kod, içerik ve test denetimi

Tarih: 18 Eylül 2026. Steam, mağaza, fiyat ve pazarlama kapsam dışı.

**Karar: İncelenen Git sürümüne çıkış onayı verilemez.** Bu bir oynanış testi sertifikası değil; kaynak kodu ve kaydedilmiş varlıklar üzerinden denetimdir. Kayıt/devam sistemi ve hedef içerik eksik; bazı somut kod hataları var; dört oyuncu ve 3.600 kitap için ölçüm yok. İnceleme skoru tahmini yapmak için veri yok.

## 1. Tam olarak hangi sürüm incelendi?

Ana kaynak: `work/lighting-and-gameplay-installed`, commit `cdf94e12339d62f9f9c881e8568050bcf5c44a6b`. Etkin build sahnesi `Assets/Settings/ne.unity`. Unity `6000.5.9f1`, URP `17.5.0`, NGO `2.13.2`.

Sonraki koridor uygulaması ayrıca `feat/two-corridor-spawn` yerel çalışma ağacında incelendi; yerel commit `e8378ad`. Bu dalın `BookSpawner.cs` değişikliği ana incelenen snapshot'ta yok. Dolayısıyla iki ayrı sürümün bulguları aşağıda açıkça ayrılmıştır. Bilgisayarında sonradan hizaladığın Third Corridor ve onayladığın son dış güneş düzeni bu kayıtlı ana sahneden doğrulanamıyor. Bunları bozuk kabul etmiyorum. Gri tavan engelleyicisine, onayladığın ışıklara ve kitap atış kalibrasyonuna bu denetimde dokunulmadı.

75 C# dosyasının dosya envanteri ve özellik aramaları yapıldı; kritik oyun, ağ, kurtarma, shader ve test yolları doğrudan okundu. Bu, her satırın ve her prefabın çalıştırıldığı anlamına gelmez. Unity Editor/Player, C# derleyicisi ve Profiler burada çalıştırılmadı. TLS allocator mesajının kök nedeni ve “shutdown” mesajının bağlamı **denetlenemedi**.

## 2. Gerçekte yapılan kontroller

| Kontrol | Sonuç | Sınır |
|---|---|---|
| Build Settings sahne listesi | Tek etkin sahne `ne.unity` | Build alınmadı |
| Sahne kitap kataloğu GUID çözümleme | 15/15 referans mevcut assetlere çözüldü | Unity import doğrulaması değil |
| Katalog ID tekilliği | 15 farklı ID, 0–14; tekrar yok | Tam 360 katalog mevcut değil |
| Yayıncı/kopya sayısı | Tüm 15 kayıt BrandID=0; copiesPerBook=10; hedeflenen spawn 150 | Başarısız Instantiate durumunda gerçek sayı daha az olabilir |
| Koridor alanı kaydı | Ana snapshot sahnesinde `corridorAreas` yok; ana spawner da tek alanlı | Yerel Third Corridor doğrulanmadı |
| Ses envanteri | Assets altında wav/mp3/ogg/flac/aiff: 0; C# içinde AudioClip/AudioSource eşleşmesi yok | Gömülü/dışarıdan yüklenen içerik bütünüyle dışlanamaz; oyun dinlenmedi |
| Kalıcılık/ayar araması | İncelenen C# içinde PlayerPrefs/persistentDataPath/SaveGame/LoadGame ve çözünürlük/rebind uygulaması bulunmadı | Haricî geliştirme dallarını kapsamıyor |
| Render ayarları | PC ve Mobile renderer: Forward+; MSAA=1; GRD kapalı; shadow distance 60 | Aktif kalite seçimi ve GPU zamanı ölçülmedi |
| Mevcut testlerin kaynak incelemesi | Edit Mode kontrolleri ve Play Mode regresyon menüleri var; eski shader kontrolü saptandı | Hiçbir Unity testine PASS verilmedi |

Katalog kontrolü Python ile gerçekten çalıştırıldı: bozuk GUID=0, tekil ID=15. Bunun dışında aşağıdaki koşullar kaynak analizidir; fizik veya ağ üzerinden çalıştırılmış test değildir.

## 3. Önceliklendirilmiş bulgular

BLOCKER: Bu kapsamla çıkış onayını engeller. MAJOR: belirgin işlev/deneyim eksikliği. MINOR: sınırlı etki. SONRA: ertelenebilir kapsam. Bu seviyeler belirli bir inceleme yüzdesi vaat etmez.

Süreler ölçülmüş iş süreleri değildir; kodu bilen iki kişilik ekip için ilk planlama aralığıdır, **kişi-gün** birimindedir. İçerik üretimi ve yeniden test ayrıca belirtilir.

| # | Bulgu ve kanıt durumu | Önem | Oyuncuya etkisi | Somut çözüm / dosya | Tahmini efor |
|---|---|---|---|---|---|
| 1 | **Doğrulandı:** oturum ilerlemesi kalıcı değil. `ConnectionManager.Update` kapanışta slotları ve GameStats'ı sıfırlıyor; Save/Load yolu bulunmadı. | BLOCKER | Uzun düzenleme oturumu kapanınca emek kaybolur. | Yeni `SessionSaveService`: hostta kitap kopyası için kalıcı ID, BookID, slot ID/index, dünya pozunu sürümlü dosyada sakla; atomik yazma/yedek ve yükleme doğrulaması ekle. NetworkObjectId'yi kalıcı kimlik olarak kullanma. | 5–8 |
| 2 | **Doğrulandı:** kayıtlı içerik 15 tür/150 kopya/1 yayıncı; hedef 360/3600/24. `BrandConfig` ayrıca 24 yerine 22 marka tanımlıyor. | BLOCKER, hedef içerik korunacaksa | Tanımlanan oyunla gönderilen oyun farklı. 150 kitap testi 3600 kitap testi sayılmaz. | `BrandConfig.cs`, `Assets/BookData`, `BookSpawner.bookTypes` ve raf eşlemesini tek katalogdan üret/doğrula. 345 türün kapak/prefab üretimi ayrı iş. | Doğrulayıcı/bağlama 2–4; eksik sanat süresi bilinmiyor |
| 3 | **Test açığı:** hedef donanımda 3600 kitap + dört oyuncu için kayıtlı CPU/GPU/ağ ölçümü yok. | BLOCKER test kapısı | 60 FPS ve oturum kararlılığı iddiası kanıtsız. | Gerçek katalogla Windows Development Build; Profiler, Frame Debugger ve ağ istatistikleri; aşağıdaki yük matrisi. Ölçüm sonrası darboğaza müdahale. | Ölçüm 2–3; düzeltme bilinmiyor |
| 4 | **Doğrulandı:** otomatik kurtarmada başarılı Teleport sonrası `recoveryCounts` sıfırlanmıyor; başarısız deneme de limiti tüketiyor. `BookRecallMachine.ScanLostBooks`. | MAJOR | Bir kitap ayrı zamanlarda üç kez kaybolunca sonraki kayıpta otomatik dönüş yok; oyun tamamlanamaz hale gelebilir. | Ardışık başarısızlık ile toplam kurtarmayı ayır; güvenli bölgede kararlı kalan kitapta başarısızlık sayacını sıfırla; güvenli fallback noktası ve manuel kurtarma bildirimi ekle. Her Teleport'ta kör sıfırlama sonsuz döngüyü geri getirir. | 0,5–1 |
| 5 | **Doğrulandı:** Play Mode regresyonu `ComicShop/Book Cel` ve `_InkWidths` istiyor; `BookToonEffect` artık `ComicShop/ToonLit` kullanıyor ve eski MPB'yi eklemiyor. | MAJOR | Render kontrolü sonraki güç/bonk kontrollerini erken durdurabilir; yanlış hata teşhisi. | `Assets/Editor/MultiplayerRegressionChecks.cs:GameplayFeatures` shader beklentisini güncelle; render doğrulamasını netcode testinden ayır. | 0,5 |
| 6 | **Doğrulandı, koridor dalı:** geçersiz/null/pasif bir alan sessizce ağırlık 0 alıyor; diğerleri geçerliyse spawn devam ediyor. | MAJOR | Third Corridor atanmadığında iki alan çalışır, üçüncü alanın neden boş olduğu açıklanmaz. Bu, kullanıcının hatasının kanıtlanmış nedeni değildir. | `BookSpawner.PrepareSession` içinde her alan için ad, aktiflik, dünya boyutu, padding sonrası alan raporu; geçersiz atanmış alan için başlangıcı açıklamalı durdur. | 0,5–1 |
| 7 | **Doğrulandı, her iki spawner:** `sessionSpawned=true` tüm üretim tamamlanmadan yazılıyor; başarısız kitaplar için başarılı sayı yerine planlanan sayı loglanıyor. | MAJOR | Eksik dünya, yanlış toplam, başarısız üretimin tekrar denemeye kapalı kalması. | Önce tüm prefab/BookItem/NetworkObject/NetworkBook ve alanları doğrula; üretim sırasında oluşturulanları izle; hata halinde rollback; gerçek başarılı sayıyı raporla. | 1–2 |
| 8 | **Doğrulandı:** `_OutlineEnabled` ToonLit materyalinde var fakat ScreenSpaceOutline maskesi onu okumuyor. Everything modu beyaz maske; filtreli mod override materyalle beyaz çiziyor. | MAJOR | “Outline off” seçilmiş yüzeyde ekran çizgisi devam eder. | `ScreenSpaceOutline.cs` maskesini per-material ToonMask pass ile entegre et; diğer shaderlar için açık fallback politikası; opt-out kullanılırken 1x1 beyaz kestirmeyi kullanma. | 1–2 |
| 9 | **Doğrulandı:** kullanıcı ayarlarını saklayan/uygulayan ses, hassasiyet, FOV, çözünürlük ve yeniden tuş atama akışı bulunmadı. `InteractionSettingsSanitizer.Apply` pickup/drop tuşlarını zorla Mouse0/Mouse1 yapıyor. | MAJOR | Uygun hassasiyet/FOV seçilemez; ileride eklenen tuş ayarı da ezilebilir. | Yeni SettingsController + Volume/AudioMixer/Camera/PlayerController bağlantıları; sanitizer'ı sürümlü ilk migrasyona indir. | 3–5 |
| 10 | **Doğrulandı:** son kitabın yerleşmesini bir sonuç/bitirme akışına bağlayan kod bulunmadı; GameStats yalnızca sayıyor, GameHUD metne basıyor. | MAJOR | İş bitince oyunun buna karşılık verdiği belirgin bir an yok. | Host yetkili CompletionState; bir kez tetiklenen sonuç paneli, devam et/yeni oda; son kitabı raftan çıkarma politikasını belirle. | 1–2 |
| 11 | **Kod/enventer eksikliği:** alma, bırakma, doğru raf, yanlış raf, bonk ve UI için AudioSource/AudioClip bağlantısı bulunmadı. | MAJOR | Ana üç saniyelik döngünün ses geri bildirimi doğrulanamıyor. | Yeni GameplayAudio; mevcut başarılı etkileşim yollarına bağla; tek ağ olayı başına tek oynatma; küçük varyasyon havuzu; müzik/efekt ayrı mixer. | Entegrasyon 2–3; ses üretimi hariç |
| 12 | **Doğrulandı:** ayağa kalktıktan sonra yeniden bonk için koruma süresi yok; yalnızca IsDown kontrolü var. | MAJOR | Oyuncu kalktığı anda tekrar düşürülebilir. | `NetworkPlayerSetup.KnockDown` hostta kısa invulnerabilityUntil kontrolü; oturum friendly-fire seçeneği; istemci sadece gösterir. | 0,5–1 |
| 13 | **Doğrulandı:** host kaybında devir yok; istemci disconnect akışına giriyor. | MAJOR | Host ayrılırsa oda biter. | Dört haftada host migration eklemek yerine #1 checkpoint + açık host çıkış onayı + kayıtlı odayı yeniden kurma. Migration sonraya. | Checkpoint üstüne 1–2 |
| 14 | **Doğrulandı:** ShelfSlot anahtarı sahne yolu + sibling index + ad hash'i. Approval yalnız kapasiteye bakıyor, sahne/katalog hash'i doğrulamıyor. | MAJOR | Aynı protokol sürümünde farklı raf düzeni kullanan buildler bağlanıp yerleşimi farklı yorumlayabilir. | `ConnectionManager` connection payload içinde build/catalog/layout hash karşılaştırması; ileride kalıcı slot GUID. | 1–2 |
| 15 | **Doğrulandı:** `PickUpRpc`/`PlaceRpc` reddedilince hedef oyuncuya sebep dönmüyor. Yerel ipuçları sunucu yarışını bilemez. | MAJOR | Aynı kitabı/son slotu iki oyuncu isterse birinde komut cevapsız görünür. | Hedefli RPC ile Claimed/Full/Blocked/TooFar sonucu; `InteractionHint` üzerinden kısa mesaj. | 0,5–1 |
| 16 | **Doğrulandı:** otomatik toon materyal dönüşümü BaseMap/BaseColor taşırken normal map'i taşımıyor. | MINOR, kaynak normal map varsa | Dönüştürülen kitabın normal detayı kaybolur. | `BookToonEffect.ResolveMaterial` kaynak `_BumpMap` aktarımı ve `_NORMALMAP` keyword; normalsiz materyalde kapalı. | 0,5 |
| 17 | **Doğrulandı:** ana lobi kontrolü 340x330 sabit piksel OnGUI kutusu; oyun HUD'ı her frame yeni metin üretiyor. | MINOR / UI test gerekli | Çözünürlük ölçeklenmesi ve okunabilirlik güvence altında değil; gereksiz GC üretimi var. | Canvas Scaler kullanan oturum paneli; HUD değer/elde liste değişiminde güncelle. Font boyutunu görüntü olmadan kusur ilan etmiyorum. | 1–2 |
| 18 | **Kapsam eksikliği:** haftalık rekabet/sıralama ve kalıcı upgrade ekonomisi uygulaması bulunmadı; O gücü geliştirme hilesi olarak tanımlı. | SONRA | Tasarımda anılan sistemler oynanabilir özellik olarak doğrulanmıyor. | Bu çıkış kapsamından çıkar; O gücünü gerçek upgrade diye sunma. Rekabetten önce otorite ve skor doğrulaması tasarla. | Kesme/doğrulama 0,5; geliştirme bu tahmine dahil değil |

## 4. On başlıkta deneyim denetimi

### İlk on dakika

Başlangıçta bağlantı debug paneli, oyun içinde sayaçlar ve etkileşim ipuçları var. Yapılandırılmış ilk kitap/ilk raf öğreticisi bulunmadı. “Oyuncu 30 saniyede anlıyor” veya “12px yazı okunmuyor” demek için güncel video yok. **Denetlenemedi.** Kabul testi: oyunu hiç görmemiş bir kişi yardım almadan oda kursun, ilk kitabı alsın ve doğru rafa yerleştirsin; geçen süre ve takıldığı metni kaydet. Takılma görülürse GameHUD'a hedefe bağlı üç görev ekle: al → yayıncıyı bul → yerleştir. Sürekli ekranda uzun kontrol listesi koyma.

### Çekirdek döngü

Alma/taşıma/atma/yerleştirme, aynı kitabın tekrar sayılmasını önleme ve bazı yanlış raf mesajları var. Bunları koru. His, elin görüşü kapatması ve animasyon süreleri video olmadan değerlendirilemez. Ses ve bitirme karşılığı eksik (#10–11); ağ reddi geri bildirimi eksik (#15). 10 farklı kitapla Mouse0/Mouse1/Q, dolu el, dolu raf ve yanlış yayıncı senaryolarını ayrı doğrula.

### Okunabilirlik ve içerik

Kayıtlı katalogda tek yayıncı olduğu için 24 yayıncı ayrımı test edilmiş sayılamaz. Kapak/sırtların 0,4 saniyede tanınması **denetlenemedi**. Teste önce iki benzer renkli yayıncı ve benzer kapaklar koy; yayıncı adı/ikonunu elde ve raf etiketinde eşleştir. Renk tek ayırt edici olmasın. BrandConfig'in 22 markası ile hedef 24 marka ve sahnedeki raf atamalarının toplam kapasitesi birlikte doğrulanmalı; her rafın dolması gerektiğini varsayma.

### Görsel tutarlılık

ToonLit'te Forward, ShadowCaster, DepthOnly, DepthNormals, Meta mevcut; Forward+ ışık döngüsü kaynakta gerçek. Noktasal ışıkların yumuşaması son kullanıcı talebi olduğundan ilk sert bant hedefinden sapmayı bu raporda hata saymıyorum. #8 ve #16 somut. Son güneş gölgelerinin düzgün olduğunu kullanıcı bildirdi; yeni görüntü olmadan yeniden ışık hatası teşhisi yapılmadı. Tavan küpünü silme. Işık huzmeleri dekoratif derinlik kırpmalı hacimlerdir; fiziksel, gerçek gölge örnekleyen volumetrik sis olarak kabul edilmemeli.

### Co-op / netcode

Kitap sahiplenme sunucu tarafında; reach/duvar kontrolü, envanter limiti, ayrılan oyuncunun kitaplarını bırakma ve Relay eski isteğinin yeni oturuma yazmasını önleme var. Bunlar değerli korumalar. Dört farklı ağdaki oyuncu, late join ve packet loss **denetlenemedi**. ClientNetworkTransform oyuncu hareketini istemci yetkili yapıyor; sunucunun mesafe kontrolü tek başına rekabetçi hile koruması değildir. Co-op için bunu hemen yeniden yazmak yerine rekabet modunu ertele. NGO kimliklerinin kayda taşınmaması ve layout hash kontrolü önemlidir.

### UI/UX

Bağlantı iptal/zaman aşımı, oda kodu, dolu oda sebebi ve host ayrılırsa ilerleme kaybolacağı uyarısı mevcut. Gerçek ayarlar menüsü ve kalıcılık yok (#9). Kendi başına çevrimdışı “Solo” menüsü bulunmadı; Direct IP Host üzerinden tek oyuncu yolu var, internet olmadan gerçek buildde test edilmelidir. Escape akışı imleci açar; dünyayı durdurduğunu varsayma. Alt+Tab dönüşünde atış tuşu takılması ve yanlışlıkla bırakma ayrıca test edilmeli.

### Ses ve maskot

Yerel ses dosyası/oynatma entegrasyonu araması sonuçsuz. Oyunu dinlemeden “tamamen sessiz” hükmü vermiyorum. Maskotun ensesine vurma → şarkı olayını doğrulayan uygulama bulunmadı; mevcut maskotun görsel/animasyon durumu denetlenemedi. Önce kitap döngüsü/bonk seslerini bitir; maskot şarkısını ertelenebilir tut. Bu rapor müzik lisansı incelemesi değildir.

### Performans

Şu anki 150 kitap ile 3600 hedefi arasında 24 kat nesne farkı var. Her NetworkBook LateUpdate çalıştırıyor; sunucu her kitap için 15 Hz pose karşılaştırıyor. 3600 kitapta yaklaşık 54.000 karşılaştırma fırsatı/saniye eder, **54.000 paket demek değildir**: değişmeyen state yazılmıyor. İstemci serbest/raftaki kitaplarda da frame başına transform yolu çalıştırıyor. Uyuyan/raftaki kitapları olay tabanlı, hareket edenleri aktif registry ile güncelleme adaydır; maliyeti ölçmeden FPS kazancı verilemez.

Sahne aramaları kurtarma ve güç yollarında tahsis oluşturuyor. Kitaplar tek foreach içinde üretildiğinden 3600 başlangıcı bir frame'e yüklenebilir; önce başlangıç profilini al, gerekirse kontrollü batch spawn ve hazır olma bariyeri ekle. Son lambalar/huzmeler için GPU ölçümü yok. Main snapshot shadow atlas 2048; sonraki dış güneş aracı 4096 ayarlıyor: bunları aynı ayar sanma. Frame Debugger ile ShadowCaster, DepthNormals, Sobel ve shaft maliyetlerini ayrı ölç. Hedef 60 FPS=16,67 ms; mevcut CPU/GPU ms, draw/SetPass, RAM/VRAM ve ağ byte/s **bilinmiyor**.

### Hatalar ve uç durumlar

Kurtarma limiti, kısmi spawn ve sessiz geçersiz koridor somut bulgular. Third Corridor boşluğu için kesin teşhis yok: mevcut sahne kaydı ve Console stack trace eksik. Koridor dalı collider.enabled durumuna bakmıyor; alan collider'ının disabled olması beklenen kullanım. Buna karşılık GameObject pasifliği alanı dışlıyor. Padding .35 ise dünya X/Z boyutlarının her biri .70 m'den büyük olmalı. Ağırlıklı rastgele seçim her alana asgari kitap garantilemiyor. Örneğin alan payı %1 ve 150 kitapta sıfır seçilme olasılığı yaklaşık %22,1; gerçek üçüncü alanın payı bilinmiyor. Her koridorda kitap isteniyorsa önce alan başına minimum kota, kalanlar ağırlıklı dağılım kullan.

“Shutdown” tek başına spawn hatası kanıtı değildir; normal oturum kapanışında da görülebilir. Hatalı alanın hepsi boşsa koridor kodu exception atıyor; önce tam exception ve ConnectionManager state geçişi birlikte kaydedilmeli. TLS allocator mesajı için Editor.log ve tekrar üretim adımı gerekir; şimdilik motor veya proje hatası diye sınıflandırılmadı.

### Oyun içi çıkış hazırlığı

Windows Player build alınması, kalıcı ilerleme, tamamlanma akışı, gerçek içerik ve co-op soak testi kapıları açık. Derleyici hatası olmadığı, shaderların buildde strip edilmediği ve sahnenin ilk kurulumda açıldığı test edilmedi. Çalışan ışık görünümü çıkış hazır olduğu anlamına gelmiyor. Onaylanmış ışıkları yeniden kurmak bu auditin çözümü değil.

## 5. Eksik test matrisi — Unity'de çalıştırılacak

Aşağıdaki satırların tamamı **BEKLİYOR**. Rapordaki varlık kontrolü dışında hiçbirine PASS verilmedi. Ayrı test sahnesi/kopya proje kullan; multiplayer regresyon menüsü oturum açıp kapatır, canlı oturumu bozar.

| Test | Adımlar | Geçme ölçütü |
|---|---|---|
| Edit Mode regresyon | Play kapalı → `ComicShop/Tests/Run Gameplay QA Checks (Edit Mode)` | Tüm mevcut assertion'lar PASS; beklenmeyen Console hata/uyarı yok |
| Play regresyon | #5 düzeltildikten sonra `ComicShop/Tests/Run Multiplayer Regression (Play Mode)` | Restart, timeout, sahiplenme, yerleştirme ve güç/bonk bölümleri tamamlanır |
| Üç koridor | Sahneyi kaydet, kapat/aç; array=3; alanları say/raporla; 20 seed ile yeni host | Tüm geçerli alanlarda kota karşılanır; rafta/duvarda başlangıç çakışması yok |
| Bozuk alan | Element2=null, pasif, X/Z=.5 veya scale=0 varyasyonları | Başlamadan hangi alanın bozuk olduğu belirtilir; kısmi kitap dünyası bırakılmaz |
| Tam katalog | 360 tür ×10; tüm katalog ID/prefab ve yayıncı/slot kapasitesi | 3600 başarılı spawn, her tür 10, tüm kitaplar bir rafa sığabilir |
| Yarış | İki istemci aynı kitabı, sonra aynı son slotu aynı anda ister | Tek sahip/yerleşim; kaybedene sebep; tüm sayaçlar eşit |
| Late join | Host 30 kitap dizsin, birini taşısın, birini atsın; üçüncü katılsın | Slot, kitap sayısı ve eldeki durum yakınsar; mükerrer yok |
| WAN co-op | Dört kişi farklı ağ; 30 dakika; 100–150 ms RTT, ayrıca %1–3 loss simülasyonu | Ayrışma/ghost book yok; reddedilen eylem geri bildirimli |
| Kapanış/yeniden açma | Host/client 10 tekrar; bağlanırken iptal; hatalı kod; host kapanışı | Stale kitap/player/slot yok; yeni oturum açılır; anlaşılır sebep |
| Kurtarma | Aynı kitabı dört ayrı kez alan dışına çıkar; aralarda güvenli bölgede beklet | Dördüncüde de kurtarılır; zeminsiz çıkış sonsuz spam/döngü yaratmaz |
| Atış/bonk | Duvar yanında, çapraz, raf köşesi, art arda bonk, disconnect elde kitap | Duvar arkası vurma yok; kitap kaybolmaz; kalkış koruması işler |
| Save/Load | 50 yerleşim + yerde/elde kitap; kayıt; kapat/aç; bozuk son dosya | Kopya kimlikleri tekil, sayaç/slot eşit; bozuk kayıtta yedek kullanılır |
| Bitirme | Son iki kitabı iki oyuncu aynı anda dizsin; sonra birini çıkar | Sonuç bir kez; herkes tutarlı durum görür |
| Görüntü | 720p/1080p/1440p ve ultrawide; güneş/raf/elde kitap; outline off | UI taşmaz; materyal opt-out çalışır; buildde pembe shader yok |
| Odak/girdi | Şarj sırasında Alt+Tab; Escape; çözünürlük değişimi; tuş yeniden atama | İstenmeyen atış/kitap kaybı yok; ayarlar yeniden açılışta korunur |
| Ses | Yerleştirme/atma/bonk iki istemcide; 100 kitaplık döngü | Yinelenen ağ sesi yok; mixer mute çalışır; rahatsız edici tekrar/clipping yok |
| Yük ölçümü | 150/1500/3600 kitap; idle/raf/çoklu atış; hedef PC 1080p | CPU/GPU frame time, p95/p99, GC, draw/SetPass, ağ ve hafıza kaydedilir |
| Uzun oturum | 4 kişi 60 dakika; join/leave, kurtarma, tekrar host | Artan bellek/stale nesne yok; bütünlük sayaçları eşit |

`ComicShop/Tests/Audit Current Multiplayer State (Play Mode)` ve `ComicShop/Tests/Audit Character Book Carry (Play Mode)` yardımcıdır; farklı ağ testi yerine geçmez. Shader testini düzeltmeden bir testin erken durmasını “netcode bozuk” diye yorumlama.

## 6. Dört haftalık gerçekçi kapsam

İki kişi ×20 iş günü yaklaşık 40 kişi-gün teorik kapasite. Yukarıdaki BLOCKER çekirdeği kayıt 5–8 + katalog bağlama 2–4 + performans ölçümü 2–3 = **9–15 kişi-gün**; bu toplam eksik 345 kapak/prefab üretimini, bulunan performans hatalarını ve gerçek WAN düzeltmelerini içermiyor. Dolayısıyla “dört haftaya kesin sığar” denemez.

Önerilen sıra:

1. İlk 1–2 gün: gerçekten oynadığın güncel dal/sahne ile tek snapshot; eski test beklentisini düzelt; üçüncü alanı açıklamalı doğrula; küçük katalogla kurtarma ve kısmi spawn testleri.
2. İlk hafta devamı: kalıcı kayıt/devam, sabit kopya/slot kimlikleri; minimum ayarlar ve completion akışı ikinci kişide.
3. İkinci hafta: gerçek katalog/raf kapasitesi, ses entegrasyonu; ilk 3600 yük ve gerçek WAN ölçümü. Darboğazı ancak bundan sonra seç.
4. Üçüncü hafta: ölçülen sorunlar, layout uyumu, ağ ret mesajları, bonk koruması. Yeni özellik ekleme.
5. Dördüncü hafta: regression + uzun oturum + Windows temiz kurulum/build; bug tamponu. Bu haftayı içerik üretimiyle doldurursan test kapılarını kapatamazsın.

En az zarar veren kesintiler: haftalık rekabet/sıralama, host migration, kalıcı upgrade ekonomisi, maskot şarkısı. Huzme efektleri ölçümde pahalı çıkarsa kalite seçeneği olsun; onaylanmış aydınlatmayı sökmek gerekmez. Kayıt/devam, kitap bütünlüğü, bağlantıdan toparlanma ve ana etkileşim seslerini kesme.

Korunacak işler: sunucu yetkili kitap claim/slot rezervasyonu, duvar arkası reach kontrolü, Relay iptal kuşağı, ortak toon materyal önbelleği ve kullanıcının onayladığı güneş/tavan çözümü.

**Son hüküm: İncelenen snapshot ile çıkışı onaylama; önce kayıt ve içerik kapsamını tamamla, sonra güncel buildde dört oyuncu/3600 kitap test kapılarını kapat.**
