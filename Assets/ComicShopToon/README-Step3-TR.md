# Adım 3 — Son iç mekân kurulumu

Hedef: Unity **6000.5.9f1**, URP **17.5.0**, Render Graph, Forward+, Linear renk uzayı, 1920×1080. Bu dal Adım 1 ve 2'yi içerir. Sahne dosyanın üzerine hazır bir sahne yazılmaz: araç, kendi hizaladığın `ToonRoom` ve sarı pencere açıklığını kullanır.

## Şu anki karanlığı giderme — önce bunu yap

1. Unity'yi kapat, çalışma kopyandaki sahne değişikliklerini kaybetmeden aşağıdaki Git kurulumunu uygula. Yeni C# dosyaları derlenmeden menü görünmez; Console'daki ilk kırmızı hatayı önce çöz.
2. Kendi düzenlediğin sahneyi aç. `ToonRoom` içindeki oda ve sarı pencereyi yeniden ayarlamana gerek yok.
3. **Tools → ComicShop → Step 3 → 1 - Apply Final Interior Setup (Undo)**.
4. **Tools → ComicShop → Step 3 → 2 - Audit Lighting and Performance**. Beklenen: **11 etkin ışık, 3 gölgeli spot**. Directional Light yok. Sahneyi `Ctrl+S` ile kaydet.
5. **Tools → ComicShop → Step 3 → 3 - Bake Bounce Lighting**. Bake bitmesini bekle. GPU lightmapper cihaz/bellek hatası verirse `Window → Rendering → Lighting` içindeki oluşturulan Lighting Settings asset'inde `Lightmapper = Progressive CPU` seç; diğer sayılar aynı kalsın, tekrar bake et.
6. Game View'da pencereye yakın bir kitap, orta raftaki kitap ve arka köşeyi karşılaştır. Pencere bölgesi en güçlü geniş aydınlık alan, orta alan okunur, köşe daha koyu olmalı. Scene View'da `Scene Lighting` ve `Post Processing` açık olsun.

**Bake bitmeden de önceki siyaha yakın görüntü açılır.** Shader'daki eski iki sorun giderildi: ışık enerjisi 1'de kırpılmıyor; düşük GI, pozlama uygulanmadan sıfıra yuvarlanmıyor. Ayrıca gölge renginin tonunu koruyan `ShadowLift = 0.12` global okunurluk dolgusu var. Bu değer fiziksel GI değildir; kitap bulma oyununda karanlıkta bile nesne şekillerini ayırt etmeyi sağlar. Kaçakları çözmek için blocker'ı kapatma.

Araç aktif sahnedeki önceki ışıkları ve Volume'leri **devre dışı bırakır**, yeni düzeni `ToonRoom/Final Toon Lighting` altında kurar. Eski avize konumlarını örnekleyerek 4×2 alan dağılımıyla sekizini seçer; uygun konum bulunamayan hücreye tavanın 60 cm altında ışık koyup Console'a bildirir. Sahnedeki 26 eski avize ışığının tümünü üst üste çalıştırmaz. Sırayla en yakın pencere avizesine üçüncü gölgeyi verir; tezgâh konumunu isimden tahmin edip başka bir yere gölge ışığı koymaz.

Üç yeni ayar asset'i `Assets/ComicShopToon/FinalSetup` içine gelir: `FinalToonStyle`, `FinalToonVolume`, `FinalToonLighting`. Emissive materyaller, OPEN mesh'i ve gerekli UV2 mesh kopyaları da burada üretilir. Yeniden çalıştırınca mevcut rig yenilenir; önceki asset dosyaları geri dönüş için tutulur. Sahne ve kaydedilen ayar değişiklikleri Undo kapsamındadır; üretilmiş asset dosyaları Undo ile silinmez. Geçici legacy property block temizliği seri hale getirilen bir sahne ayarı değildir.

**Sarı çerçeveyi daha sonra taşırsan Adım 3/1'i tekrar çalıştır.** Böylece blocker, spotlar ve neon birlikte yeni açıklığa gider. Sonradan elle ayarladığın rig/Volume yerine başlangıç değerlerini tekrar kuracağını unutma.

## 1. Işık düzeni

### Kesin başlangıç değerleri

Aşağıdaki Intensity sayıları araçtaki **`Light.intensity`** değerleridir; `LightUnit.Candela`, `enableSpotReflector = false` kullanılır. Bu toon shader enerji üzerinde global ölçek ve bantlama yaptığı için bunlar fiziksel iç mekân lux hesabı iddiası değildir. Başka bir shader/exposure ile aynı ekran parlaklığını vermez.

| Kaynak | Adet | Mode | Kelvin / renk | Intensity | Range | Outer / Inner Angle | Gerçek zamanlı gölge |
|---|---:|---|---|---:|---:|---|---|
| Window 1–2 | 2 | Mixed | 6500 K, White | 48 **her biri** | 18 m | 80° / 60° | Hard, ikisi de |
| Pendant 1 | 1 | Mixed | 3200 K, White | 8 | 8 m | 120° / 90° | Hard |
| Pendant 2–8 | 7 | Mixed | 3200 K, White | 8 | 8 m | 120° / 90° | None |
| Neon Fill | 1 | Mixed | Temperature Off, RGB (1, 0.08, 0.4) | 1.2 | 1.5 m | 100° / 70° | None |
| Directional Light | 0 etkin | — | — | — | — | — | — |

Tüm ışıkların `Indirect Multiplier / bounceIntensity = 1`, `Culling Mask = Everything`, Rendering Layers = tümü. Pencere spotları sarı açıklığın eninin ±%25'ine, merkez yüksekliğinin %30 pencere boyu üstüne yerleşir. Görünür cepheden dışarı ofset `shellOffset + shellThickness + 0.1 m` = varsayılan **0.32 m**. Yön: içeri + aşağı 0.45; yataya yaklaşık 24° eğim. Pencerenin önüne/arkasına kapanmış başka geometri olmadığını kontrol et; bu araç gerçek cam mesh'ini kesmez.

**Mixed Lighting Mode = Baked Indirect** (`MixedLightingMode.IndirectOnly`). Statik yüzeylerin bile doğrudan aydınlatması gerçek zamanlı hesaplanır; bake dolaylı bounce ve ortam katkısını sağlar. Bu, toon bandını shader'da korumamızı sağlar. `Baked` lamba kullanıp doğrudan yumuşak aydınlatmayı lightmap'e gömmüyoruz. `Subtractive` bu amaç için uygun değil. `Shadowmask` teknik olarak kullanılabilir ama bu başlangıçta gereksiz maske/kanal yönetimi getirir; örtüşen Mixed ışıklar için shadowmask kanal sınırıyla uğraşmıyoruz.

Gölgeli **3 spot = 3 gölge görünümü**, point light cubemap'inin altı yüzü yok. Atlas **2048²**, pencere spotları **High / 1024**, tek avize **Medium / 512**. Atlas yerleşimine göre URP çözünürlüğü düşürebilir; Frame Debugger'da kontrol et. Diğer yedi avizede örtülme gölgesi hesaplanmadığı için çok ince rafların arkasına doğrudan ışık düşebilir. Bu bilinçli bütçe değiş tokuşudur: ışık menzilini oda dışına uzatma; gerektiğinde belirli bir avizeyi gölgeli yaparken mevcut üçüncü gölgeyi kapat. Gölgesiz ışığı engellemek için blocker kullanmak işe yaramaz.

| URP / Light gölge ayarı | Değer | Yanlışsa belirti |
|---|---:|---|
| Depth Bias | 0.1 | Çok yüksek: gölge nesneden ayrılır; çok düşük: acne |
| Normal Bias | 0.2 | Çok yüksek: ince duvar/raf kenarında ışık sızması |
| Shadow Near Plane | 0.05 m | Çok yüksek: lambaya yakın engel gölgeye giremez |
| Max Distance | 35 m | Daha ileride gerçek zamanlı gölgeler kaybolur |
| Cascade Count / Split | 2 / 0.35 | Directional yeniden açılırsa geçerli; bu spot düzenine faydası yok |
| Soft Shadows | Off | Hard bantların üstüne gereksiz yumuşak gölge yayılması engellenir |

**Işık huzmesi ile yüzey aydınlanması farklıdır.** Pencereden zemine ışık düşmesi bu sistemde var. Havada görünen hacimsel güneş huzmesi bu adımda yok. Bloom havayı aydınlatan volumetric değildir.

### Lightmapping

| Ayar | Değer |
|---|---|
| Realtime GI | Off |
| Baked GI | On |
| Mixed Lighting | Baked Indirect |
| Lightmapper | Progressive GPU; cihaz/bellek sorunu varsa Progressive CPU |
| Sampling | Fixed |
| Direct Samples | 64 |
| Indirect Samples | 512 |
| Environment Samples | 128 |
| Max Bounces | 3 |
| Lightmap Resolution | 16 texel/m |
| Max Lightmap Size | 2048 |
| Padding | 4 texel |
| Directional Mode | Non-Directional |
| AO | Off |
| Albedo Boost / Indirect Intensity | 1 / 1 |
| Filtering | Advanced |
| Direct Denoiser / Filter | None / None |
| Indirect Denoiser / Filter | OpenImage / None |
| AO Denoiser / Filter | None / None; AO kapalı |
| Lightmap Compression | None; bant eşiğinde blok artefaktını önlemek için |

Denoise edilen şey dolaylı örnekleme gürültüsüdür; ışık bandı değildir. Shader GI'yı aldıktan sonra **+2 EV**, RGB kanalı başına **3 seviye**, ardından **0.25 BakedInfluence** uygular. İşlem sırası önemlidir: sonradan artırılan Influence, önceden sıfıra yuvarlanmış GI'yı kurtaramaz. Baked Indirect doğrudan ışığın lightmap'e gitmesini engeller; yalnız sample sayısını azaltmak bunu sağlamaz.

Önceki kaçak bake'de kayıtlıysa yeni blocker anında o veriyi silemez. `Window → Rendering → Lighting → Clear Baked Data`, ardından tekrar bake gerekir. Kurulum mevcut bake verini otomatik silmez.

Mimari etiket/layer/`ToonArchitecture` marker'ı, ayrıca floor/shelf/shelv/counter adlarıyla bulunan sabit yüzeyler `Contribute GI` olur. Eksik UV2 için **kaynak mesh'e dokunmadan**, vertex kanallarını koruyan paylaşılan mesh kopyası çıkarılır ve UV açılır. BookItem, Rigidbody veya Animator altında kalanlar lightmap/statik batching dışında tutulur. Araç tanımadığı anonim sabit mobilyayı kendiliğinden statik saymaz; tek üst objesine `ToonArchitecture` eklemek bütün hiyerarşiyi kapsar. Robotu/taşınabilirleri bu hiyerarşiye alma.

### Probe, ortam ve SSAO

**APV: bu oda için hayır.** Mevcut shader APV varyantlarını destekliyor; son preset klasik **Light Probe Groups** kullanıyor. Küçük, sabit bir oda ve eski GPU hedefi için ek APV streaming/volume ayarı gerekmiyor. `Light Probe System = Light Probe Groups`. 2 m X/Z aralık, duvardan ilk sıra 0.5 m; zeminden **0.35 / 1.5 / 2.8 m** yükseklik: 20×12 m odada **180 probe**. Taşınabilir renderer'lar `Blend Probes`. Probelar bake ile doldurulur. Raf içine düşen probe'lar lokal sızıntı yaratırsa Light Probe Group noktalarını rafın boş tarafına topluca taşı; otomatik grid, collider'sız rafın içini geometrik olarak tanıdığını iddia etmez.

| Ortam / yansıma | Değer |
|---|---|
| Ambient Source | Gradient / Trilight |
| Sky Color (sRGB) | (0.24, 0.29, 0.40) |
| Equator Color | (0.14, 0.12, 0.18) |
| Ground Color | (0.07, 0.06, 0.09) |
| Ambient Intensity alanı | 0.35; Gradient'te belirleyici renklerdir, Skybox yoğunluğu gibi düşünme |
| Reflection Probe | 1, Baked, 128², Box Projection On, oda bounds |
| Probe Intensity | 0.25 |
| Default Reflection Intensity / Bounces | 0.25 / 1 |
| Fog | Off |

ToonLit şu anda çevresel specular reflection örneklemiyor. Reflection Probe; ayrı URP Lit cam/metal kullanan özel yüzeyler içindir, toon rafı aydınlatmaz. Realtime reflection probe yok. Bake sonrasında Lighting penceresinden reflection probe bake'inin de oluştuğunu kontrol et.

**SSAO Off.** Outline, AO'nun yerine geçen fiziksel temas kararması değildir: outline siluet/normal/depth sınırı, AO ise yüzey yakınlığıdır. Yani varsayımının “aynı işi yapıyorlar” kısmı doğru değil. Buna rağmen bu görünümde SSAO kapalı seçiyorum: 3.600 kitap arasını gereksiz kirletir, zaten karanlık olan rafları daha da bastırır ve ek GPU masrafı yaratır. Temas hissi için gerçek gölge ve kontrollü outline yeterli başlangıç.

## 2. Volume ve global toon değerleri

`Global Volume`, Weight **1**, Priority **100**. Tek kaynak profile; eski sahne Volume'leri devre dışı. Kamera HDR ve Post Processing On, URP **HDR Color Grading**, LUT **32**, MSAA Disabled, **SMAA Medium**. Dynamic Resolution / render scale oynatılmaz; halftone piksel boyutunu korumak için render scale 1.

| Override | Kesin değerler |
|---|---|
| Tonemapping | **None** |
| Color Adjustments | Post Exposure **+0.35 EV**, Contrast **6**, Saturation **8**, Hue Shift **0**, Filter White |
| White Balance | Temperature **0**, Tint **0** |
| Shadows Midtones Highlights | Shadows **(0.94, 0.96, 1.08, 0)**; Midtones **(1, 1, 1, 0)**; Highlights **(1.04, 1.01, 0.96, 0)** |
| SMH ranges | Shadows Start **0**, End **0.3**; Highlights Start **0.6**, End **1** |
| Bloom | Threshold **1.2**, Intensity **0.18**, Scatter **0.55**, Clamp **8**, Tint White, High Quality Filtering Off, Dirt Texture None |
| Vignette | Black, Intensity **0.10**, Smoothness **0.35**, Center **(0.5,0.5)**, Rounded Off |
| Film Grain | Intensity **0** |
| Motion Blur | Intensity **0** |
| Depth Of Field | Off |
| Chromatic Aberration | Intensity **0** |
| Lens Distortion | Intensity **0** |
| Color Lookup | Contribution **0** |

SMH vektörünün son bileşeni **0** luminance ayarıdır; alpha değildir. White Balance ile tüm görüntüyü turuncuya boyamıyoruz; sıcak/soğuk ayrımı gerçek ışık renklerinden ve hafif SMH düzeltmesinden gelir.

**Neden None?** ACES'in highlight sıkıştırması ve renk dönüşümü düz doygun çizgi roman paletini değiştirir. Neutral aşırı HDR aralığını korumak gereken daha fiziksel bir sahnede düşünülebilir; burada ışık enerjisini zaten global `DirectMax=2` ile sınırlıyoruz. None seçimi HDR'nin display aralığı üstünü kırpabilir; bu küçük emissive kaynaklarda bilerek kabul edilir. Bloom bu kaynakların parıltısını taşır. Duvarın büyük alanı dümdüz beyaz oluyorsa Bloom'u değil, ışık gücünü/global DirectMax'ı kontrol et.

**Film Grain yok:** Ben-Day noktalarının üzerine zamanla değişen ikinci bir doku bindirip titreşim üretir. **Tam ekran Posterize pass yok:** kitap kapaklarının çizimlerini, UI'ı ve Bloom'u da bozardı; aydınlatma ve GI zaten doğru yerde bantlanıyor. Yeni bir Render Graph pass eklemek gerekmiyor; Adım 2'nin outline pass'i Render Graph üzerinden devam ediyor.

| ToonStyleAsset | Final preset |
|---|---:|
| ShadowSteps / RampSmoothness | 3 / 0.01 |
| ShadowTint | #3A2E52 |
| ShadowLift | 0.12 |
| LightFalloffScale | 1 |
| DirectMax | 2 |
| BakedSteps / BakedExposure / BakedInfluence | 3 / +2 EV / 0.25 |
| HalftoneEnabled / Scale / Angle / Strength | On / 8 px / 45° / 0.25 |
| HalftoneRadius | mevcut asset değeri; varsayılan 0.27 |
| SpecEnabled / RimEnabled | Off / Off |
| OutlineThicknessPixels | 1.25 |
| EmissionGain | 4 |

Yeni `ShadowLift`, `BakedExposure`, `DirectMax`, `EmissionGain` **global-only**; materyal override'ları yok. Asset → controller → Shader globals, `UnityPerMaterial` dışında. Adım 1'in mevcut advanced override'ları araç tarafından Follow Global'a sıfırlanır. Albedo ve normal bilgisi korunur; elli materyali ayrı ayrı ayarlamazsın.

Işık birikimi artık kanal başına **en güçlü quantize edilmiş katkıyı** seçer; sekiz avize üst üste geldiğinde beyaza eklenerek patlamaz. Bu bilinçli, fiziksel olmayan toon birleşimidir. NdotL bandı 3 kademedir; enerji bantları, GI ve farklı ışıkların birleşimi nedeniyle final pikselde toplam tam üç renk bulunacağı anlamına gelmez. Kaynak başına angular ve energy quantization korunur.

Araç avize uçlarına sekiz küçük emissive küre ve pencereye tek mesh'lik pembe `OPEN Neon (Generated)` ekler. `ComicShop/ToonEmission` SRP Batcher uyumlu, global EmissionGain kullanır; materyalde yalnız base color vardır. Kaynak ışımaları lightmap'e ikinci kez bake edilmez; bounce'u Mixed ışık sağlar. Mevcut özel OPEN modelin varsa üretilen işareti kapatıp aynı `NeonGlow` materyalini onun ışıklı alt grubuna topluca atayabilirsin. Bu script mevcut adsız modelin hangi üçgenlerinin neon olduğunu varsaymaz.

**Pencere en güçlü geniş aydınlık bölge olarak tasarlandı; neon/ampul birkaç pikselde daha yüksek HDR tepe üretebilir.** Spot eklemek camın ya da dışarıdaki siyah bir duvarın materyalini kendiliğinden parlatmaz. Camı açılan boşluğun önündeki opaque bir duvar gibi çiziyorsan veya dışarı tamamen karanlık mesh varsa pencerenin görünen yüzeyi hâlâ koyu kalabilir. `Window` ışıklarını tek başına açarak önce içeri düşen aydınlığı doğrula. Dışarının yerine otomatik beyaz bir levha koyup hatayı gizlemiyorum. Görünen dış çevrenin/albedonun hiç verilmediği bir sahnede mutlak en parlak pencere pikselini garanti etmek doğru olmaz.

## 3. 3.600 nesne: tercih ve bütçe

**Bu teslimatta tercih: SRP Batcher.** ToonLit bütün pass'lerde aynı `UnityPerMaterial` layout'unu kullanır; look uniformları dışarıdadır. Aynı kaynak kapak materyali için ortak cache kullanılır. `SetCoverMaterial` da bu cache üzerinden gider; 3.600 adet `renderer.material` getter'ı ile instance üretmeyiz. Eski `ShopV16Appearance` artık ToonLit renderer'larına `_Receive` property block yazmaz ve kullanılmayan light array'lerini ToonLit materyallerine her frame göndermez.

| Yöntem | Karar / gerçek sınır |
|---|---|
| SRP Batcher | On. CPU material/state hazırlık maliyetini azaltır. **Draw call birleştirmez.** |
| GPU Instancing | Shader'ın klasik instancing varyantı var. SRP Batcher uyumlu renderer'da öncelik SRP Batcher'dadır; kutuyu işaretlemek 3.600 kitabı tek draw yapmaz. Farklı mesh/material grupları ayrı kalır. |
| GPU Resident Drawer | Off. Mevcut özel shader DOTS Instancing metadata/property erişimi taşımıyor; sadece URP kutusunu açarak destekli olduğunu söylemek yanlış. GRD için tüm gerekli pass'lerde DOTS uyumu, BRG varyantları ve uygun platform doğrulaması gerekir. |

**Forward+**: sekiz avize + iki pencere gibi örtüşen ışıklarda Forward'ın per-object ışık sınırına göre yüzeylerin farklı ışıkları seçmesini istemiyoruz. Mevcut `LIGHT_LOOP_BEGIN` cluster loop gerçek Forward+ ışıklarını dolaşır; `GetAdditionalLightsCount()` Forward+'ta sıfır olsa da loop bununla sınırlı değildir. Çok sayıda obje tek başına Forward+ gerekçesi değil; örtüşen ışık sayısı ve tutarlı seçim gerekçedir.

### Culling / LOD

- Oda ve raflar Occluder/Occludee Static; hareketli kitaplar Occluder Static veya Batching Static **değil**. Kamera Occlusion Culling On. Sabit raflar için `Window → Rendering → Occlusion Culling`: **Smallest Occluder 0.5 m, Smallest Hole 0.2 m, Backface Threshold 100** başlangıcıyla bake. Kitap kalınlığındaki boşlukları büyük occluder gibi kapatma. Aynı geniş odada frustum culling çok önemlidir; occlusion'ın görünür bütün kitapları ortadan kaldıracağını varsayma.
- Kitap collider/BookItem/network nesnesini render culling için kapatma. Taşınan kitaplarda fizik ve etkileşim çalışmaya devam etmeli.
- Basit 12–24 triangle kitap kutusuna üç ayrı mesh LOD eklemek çoğu kez kazanç sağlamaz. Karmaşık kitap mesh'i varsa paylaşılan prefab için LOD0 screen-relative height **0.04**, LOD1 basit kutu **0.008**, altı Cull; `Fade Mode = None`. Gerçek düşük poligon mesh yoksa aynı mesh'i üç LOD'a koyma. Bu teslimat LOD mesh uydurmaz ve kullanıcının kitaplarını görüş mesafesinde gizlemez.
- 3.600 ayrı görünür kitap her frame gerçekten gerekiyorsa SRP Batcher tek başına yeterli olmayabilir. Sonraki optimizasyonun atlas/texture array kullanan grup tabanlı explicit instancing veya DOTS uyumlu GRD olması gerekir. SRP Batcher ile “50 materyal → 50 draw” diye bir dönüşüm yok.
- Kitaplar screen outline; hero hull yalnız robot/tezgâh/kasa. Screen outline **Everything** maskesiyle bir fullscreen draw; dar layer maskesi ek geometri çizimi getirir. DepthNormals ön geçişi ayrıca renderer çizim maliyeti taşır. Inverted hull'u tüm kitaplara açmak yaklaşık **+3.600 submesh draw** daha yaratabilir.

### 1080p GTX 1060 / RX 580 sınıfı hedef bütçe

Bunlar **ölçülmüş FPS değil, profiling kabul bütçesi**. CPU modeli, triangle sayısı, kapak texture boyutları, gerçek görünür kitap sayısı ve shadowcaster sayısı bilinmeden 60 FPS garantisi verilemez. 1060 3 GB ile 6 GB aynı VRAM alanına sahip değil.

| GPU işi | Hedef süre |
|---|---:|
| Üç spot shadow map | 1.5–2.5 ms |
| Depth / Normals | 1.0–2.0 ms |
| Opaque toon + Forward+ ışık | 4.0–5.5 ms |
| Screen outline + az sayıda hero hull | 0.8–1.3 ms |
| Bloom + color grading + SMAA + final | 1.0–1.8 ms |
| Transparan/cam/UI ve diğer GPU payı | 0.7–1.0 ms |
| **Toplam GPU hedefi** | **9.0–14.1 ms** |

CPU ana thread hedefi **≤8 ms**, render thread **≤8 ms**; bunları GPU toplamına doğrudan toplama, işler örtüşür. 60 FPS sınırı **16.67 ms**, en az yaklaşık **2 ms** dalgalanma payı bırak. Bütün 3.600 renderer aynı anda görünür, çok submesh'li ve üç gölge haritasına da giriyorsa maliyet bu bütçeyi aşabilir; **20–35+ ms frame** mümkündür. Bu durumda post-process'ten 0.2 ms kesmek asıl sorunu çözmez.

Ölçüm: Windows standalone build, 1080p, VSync Off ölçüm için, Profiler ile GPU/CPU ayrı; Frame Debugger draw/SRP batch sayısı. Ön raflara ve oda çaprazına bakarak iki görüş, dört oyuncu + dağınık kitaplarla test. Editor Scene/Game View'un ikisi açıkken çıkan süreyi build performansı sayma. Gerçek yayın öncesi bu ölçüm halen gereklidir.

## 4. Sıfır URP projesinden kurulum sırası

1. Unity Hub → Unity 6000.5.x → **Universal 3D**. Packages'te URP 17.5.x. `Project Settings → Player → Other Settings → Color Space = Linear`.
2. `Assets → Create → Rendering → URP Asset (with Universal Renderer)` ile `ComicShopURP` ve `ComicShopRenderer` oluştur. `Project Settings → Graphics` ve kullanılan bütün `Quality` seviyelerine URP asset'i ata. Mevcut repoda PC_RPAsset / PC_Renderer zaten var.
3. `Project Settings → Graphics → Render Graph` altında **Compatibility Mode / Render Graph Disabled = Off**. Adım 2 feature'ı yalnız Render Graph yolunu uygular; compatibility Execute fallback yok.
4. Renderer `Rendering Path = Forward+`; URP `HDR On`, Depth Texture On, MSAA Disabled, Render Scale 1, SRP Batcher On, GPU Resident Drawer Disabled, Additional Lights Per Pixel ve Additional Light Shadows On. Adım 3 aracı projedeki URP asset'lerine bunları topluca uygular. Quality değişiminde eski karanlık ayarların geri gelmemesi için tüm proje URP asset'lerini kapsar.
5. Adım 1 shader/runtime/editor ve Resources klasörlerini, ardından Adım 2 ve 3 dosyalarını import et. Projede console compile hatası olmamalı. `Create → ComicShop → Toon Style` ile bir style asset; scene root'ta `ToonStyleController` ile bağla. Final tool bunun kopyasını üretip tek etkin global owner yapar. Resources default asset player fallback'idir.
6. Opaque materyaller `ComicShop/ToonLit`: yalnız base color/albedo, isteğe bağlı normal, halftone/outline toggles. `Tools → ComicShop → Toon Style → Reset All ToonLit Materials to Follow Global`. Mevcut repo materyal geçişini Adım 1 içerir; tamamen yeni üçüncü parti paketin bütün alpha/cam shader'larını körlemesine opaque shader'a çevirme.
7. Scene mimarisi için Step 2 Room Repair Setup oluştur. **Önce oda bounds ve sarı gerçek pencere açıklığını hizala.** `Preserve Shopfront Opening` açık. Gerekirse mimari kökünü bir kez işaretle; objeleri tek tek düzenleme.
8. Hero outline için robot/tezgâh/kasa köklerini birlikte seç → `Step 2 → Bake and Install Hero Outlines on Selection`. Mesh değiştiğinde yeniden çalıştır. Kitapları dahil etme.
9. `Step 3 → 1 - Apply Final Interior Setup (Undo)`. Bu; blocker'ı hizalı açıklıkla yeniden üretir, Two Sided repair yapar, screen outline feature'ını kurar, mevcut ışıkları kapatır, final rig/Volume/Lighting Settings/ToonStyle/Light Probe Group/Baked Reflection Probe/ışıklı yüzeyleri oluşturur. Doğru kodun geldiğini görmek için `Final Toon Lighting` kökünü seç.
10. Scene kameraları HDR, Post Processing, SMAA Medium ve gölgeler için ayarlanır. `ToonCameraSetup` runtime'da geç doğan oyuncu kameralarını da **en geç 1 saniye içinde** ayarlar. RenderTexture kamera ve Overlay kamera ayarlarına dokunmaz. İlk spawn frame'inden itibaren zorunlu post istiyorsan aynı ayarları ortak oyuncu kamera prefab'ında sakla.
11. Sabit mimarinin UV2/Contribute GI raporunu incele; taşınan kitaplar probes kullanır. Pipeline Probe System = Light Probe Groups. Eski APV verisi bu preset'te kullanılmaz.
12. `Step 3 → 3 - Bake Bounce Lighting`; ardından reflection probe sonuçlarını kontrol et. Kaçak taşıyan eski bake varsa Clear Baked Data sonrası bake.
13. Occlusion Culling bake'ini yukarıdaki sayılarla çalıştır; görüşlerin hiçbirinde raf arkasındaki görünen kitapların yanlış cull edilmediğini kontrol et.
14. `Step 3 → 2 - Audit Lighting and Performance`, sonra Frame Debugger. 11 ışık, 3 gölgeli spot, tek etkin style/Volume, aktif Render Graph screen outline, eksik UV2 uyarısı yok, gereksiz MPB yok.
15. Dört oyunculu standalone test; 1080p GPU/CPU bütçelerini ölç. Hedefte kalıyorsa normal oyun VSync / frame limiter tercihini uygula. 60 hedefi için `Application.targetFrameRate = 60` tek başına optimizasyon değildir.

### Git: kendi pencere ayarını kaybetmeden

Kendi güncel Step 2 çalışma klasöründe PowerShell:

```powershell
git status
git add Assets ProjectSettings Packages
git commit -m "Save local Step 2 room and window alignment"
git fetch origin feat/final-lighting-step3
git merge --no-edit origin/feat/final-lighting-step3
```

`nothing to commit` varsa commit atmaya gerek yok, fetch/merge'e geç. Bu dal hazır sahne dosyanı değiştirmediği için senin oda/pencere konumlarını taşır. Conflict olursa rastgele `--theirs` veya reset kullanma; dosyaya göre çöz. Unity Hub'dan **aynı çalışma klasörünü** 6000.5.9f1 ile aç. Başka worktree açarsan yerel sahne değişikliklerin otomatik orada bulunmaz.

## 5. En olası beş hata

| Hata | Nasıl anlarsın? | Çözüm |
|---|---|---|
| Yanlış proje/dal, compile hatası veya eski Volume | Step 3 menüsü yok; yeni ışıklara rağmen görüntü aynı | Hub path ve Console'un ilk hatası; commit/branch doğrula; tek etkin Final Volume ve HDR Color Grading |
| Açıklık ile blocker/spot uyuşmuyor; cam opaque gölge kesiyor | Window spotlar açıkken içeri hiç ışık düşmüyor, avizeler çalışıyor | Step 3/1 tekrar; gerçek pencere mesh'i/duvarı boşluğu kapatıyor mu kontrol; camda gereksiz Cast Shadows kapat; global blocker'ı devre dışı bırakma |
| Eski lightmap, yanlış Mixed mode veya dinamik kitapta Contribute GI | Kitabı kaldırınca eski gölge yerde kalır; duvar bandı yumuşar; tavanda eski aydınlık lekeler | Baked Indirect + dinamik kitap probes; Clear Baked Data ve yeniden bake |
| HDR enerji/pozlama ya da GI sırası yanlış | Pencere ve avize eşit parlak; bake arttırmak siyah köşeyi açmıyor; glow yok | Yeni ToonForward import edilmiş mi; DirectMax 2, BakedExposure 2, Influence .25; Camera HDR/Post ve global EmissionGain 4 |
| Yanlış batching/gölge/outline beklentisi | 3.600 “batched” kitapta render thread tavan, kitap başına ikinci outline pass | SRP Batcher draw birleştirmez; legacy MPB kaldır; 3 spot sınırı; screen mask Everything; hull yalnız hero; build'de ölç |

## Doğrulama ve sınırlar

- Değişen ToonLit için **198** temsilî HLSL entry/varyant kombinasyonu DXC ile başarılı.
- Adım 2 için **26**, yeni emissive shader için **8** HLSL kontrolü başarılı.
- Unity Graphics referansı: `c930feab9f105738328d183bb3c32758d8da0ac3`; C# API kontrolleri UnityCsReference **6000.5** kaynaklarıyla yapıldı. Bu, Unity Editor'ün C# ve ShaderLab import/build testinin yerine geçmez.
- Bu çalışma ortamında Unity Editor veya GTX 1060/RX 580 çalıştırılmadı. Yerel sahnenin son hâli/bake sonucu burada yok. Dolayısıyla “görüntü kusursuz” veya “60 FPS ölçüldü” iddiası yok; kurulum, sayısal başlangıç ve doğrulama adımları somuttur.
- Mevcut geometry/UV3, kişisel sahne hizalaması, network/kitap etkileşim kodu korunur. Shader'a Built-in RP yolu veya obsolete RenderPass Execute eklenmedi.

Resmî kaynaklar: [URP Asset API kaynağı](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs), [URP gerçek zamanlı ışık hesapları](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl), [Unity 6.5 LightingSettings API](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5/Runtime/Export/GI/LightingSettings.bindings.cs), [Unity 6.5 Light API](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5/Runtime/Export/Graphics/Light.bindings.cs).

## En son neyi ayarlamalısın?

**Önce yalnız `FinalToonStyle.ShadowLift`**: 0.12 başlangıç, hâlâ okunmuyorsa 0.16, çok düzse 0.08. Köşe okunurluğunu değiştirir; materyallere dokunmaz.

**Sonra bake sonucu için `BakedExposure`**: +2 EV başlangıç; bounce hâlâ sıfıra düşüyorsa +2.5, çok yayılıyorsa +1.5. `BakedInfluence` 0.25 kalsın; band sayısını artırarak GI'yi düzleştirme.

**Son olarak pencere/avize oranı**: iki Window ışığını birlikte 48 → 64 çıkar veya sekiz Pendant'ı birlikte 8 → 6 indir. Bu, ışık dengesi ayarıdır; global toon görünüm ayarı değildir. Önce ışığın gerçekten açıklıktan geçtiğini doğrula. Exposure'u +2/+3 EV yapıp kaçak veya kapalı pencere problemini örtme.
