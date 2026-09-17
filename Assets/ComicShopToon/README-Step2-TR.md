# Adım 2 — Outline ve mimari ışık sızıntısı

Unity 6000.5 / URP 17.5 / Render Graph. Adım 1 üzerine eklenir. Tam kaynak dosyalar `Runtime`, `Editor`, `Shaders` altında bulunur. Oda aydınlatmasının sanatsal kurulumu, post-process ve performans optimizasyonu bu adımda yapılmaz; isteğe bağlı pencere spotları yalnız teşhis başlangıç düzeneğidir.

## Kurulum ve toplu kullanım

Mevcut PC_Renderer üzerindeki ScreenSpaceOutline güncellenmiştir. Başka URP renderer asset'leri varsa `Tools > ComicShop > Step 2 > Install Screen Outlines on Project URP Renderers` hepsine aynı feature'ı kurar. Render Graph etkin, Compatibility Mode kapalı olmalı. Projedeki PC ayarı zaten MSAA Disabled kullanıyor; bu implementasyon MSAA>1 kameraları açık bir uyarıyla atlar. MSAA depth attachment ile tek örnekli maskeyi yanlış eşleştirmez.

Bütün renk/kalınlık/eşik ayarları **aynı ToonStyleAsset** içindedir: `Tools > ComicShop > Toon Style > Select Global Style`. Outline shader'larında bu isimler Properties veya UnityPerMaterial içine alınmaz. Renderer feature'da yalnız shader referansı ve **Layer Mask** bulunur. Önceki renderer'a özel outline renk/eşik kayıtları kaldırılmıştır.

Hero'ların Hierarchy köklerini topluca seç: robot, tezgâh, kasa. Bir kez `Tools > ComicShop > Step 2 > Bake and Install Hero Outlines on Selection` çalıştır. Her alt renderer için hazır ortak materyalle ayrı `ToonHull` child oluşur. Menü kaynak FBX/mesh'i değiştirmez; UV3'e yazılan normal verisi yalnız hull kopyasındadır. Aynı menüyü tekrar çalıştırınca mevcut üretilmiş hull'lar değiştirilir. Seçimdeki bilinen eski `ToastRanger/Outline URP`, `Custom/OutlineOnly` ve `ComicShop/ToonOutline` renderer'ları Undo ile kapatılarak robotta üst üste eski/yeni hull çizimi önlenir. Undo sahne değişikliklerini geri alır; oluşturulmuş mesh asset'leri yeniden kullanılabilmesi için kalır. Kitap bileşenleri olan alt ağaçlarda hull üretilmez. Bütün dükkânı hero diye seçme: BookItem işareti olmayan raf/duvarlar da seçim kapsamında olur.

Mimari için `Create or Select Room Repair Setup` tek `ToonRoom` ayar objesi üretir. Araç aktif sahnedeki mimariyi şu kaynaklardan tanır: `Architecture` tag'i, seçtiğin Architecture Layers maskesi, `ToonArchitecture` işaretli üst hiyerarşi veya açıkken wall/ceiling/roof/duvar/tavan adları. Kaynak model anlamsız isimler taşıyorsa **mimari köklerini topluca seçip** `Mark Selected Architecture Hierarchies` çalıştır; panelleri tek tek işaretleme. Araç hiç mimari bulamazsa sessizce başarı iddia etmez; rapor sayısı sıfır olur.

Room Bounds varsayılan 20 × 4 × 12 m, merkez (0,2,0). İstersen `Fit Room Bounds to Identified Architecture` ile tek seferde hesaplat. Bu işlem renderer mesh bounds'larının oda koordinatındaki sekiz köşesini birleştirir. Dış sokak veya mobilya grubunu mimari diye işaretlersen bounds yanlış büyür. Cyan gizmo ile kontrol et. Room ayarının world scale'i **1,1,1** olmalı; konum ve dönüş desteklenir.

**Sınır kutusu pencerenin nerede olduğunu söylemez.** Aynı Room Inspector'da tek seferlik `Shopfront`, `Opening Center`, `Opening Size` değerlerini sarı gizmo ile pencereye hizala. Varsayılan açıklık eksi-Z cephesinde, yatay merkez 0, tabandan 2 m yukarı, 4 × 3 m'dir; bunlar gerçek sahnenin ölçüldüğü iddiası değildir. Araç bu açıklığı kapatmadan shell oluşturur. Birden fazla kapı/pencere, L planı veya eğimli çatı tek dikdörtgen shell ile temsil edilmez; böyle mimaride bounds aracı yerine özel kapalı collision/shadow kabuğu gerekir.

## A1 — Inverted hull

`ToonOutline.shader`: `Cull Front`, `ZWrite Off`, `ZTest LEqual`, UV3/TEXCOORD2'deki smoothed tangent-space normal yönünde world-space extrusion. `ToonHullBinding` skin bones ve blendshape hareketini izler, renderer enabled durumunu eşler ve kamera culling başlamadan önce extrusion için güvenli bounds genişletir. Hull shadow casting kapalıdır; nesnenin gerçek mesh'i gölge üretir. Birden fazla submesh varsa her biri için ortak outline materyali atanır; son submesh'i materyal slotu ekleyerek tekrar çizme hatasına düşülmez.

| Global ayar | Başlangıç |
|---|---:|
| OutlineColor | sRGB (0.025, 0.018, 0.04) |
| OutlineWidth | 0.01 m |
| OutlineReferenceDistance | 5 m |
| OutlineThicknessPixels | 1.5 px |
| DepthThreshold | 0.035 |
| NormalThreshold | 0.25 |

Perspektif kamerada world kalınlığı `OutlineWidth × viewDepth / referenceDistance × sqrt(3) / abs(P.m11)` ile telafi edilir. Referans 5 m / 60° vertical FOV'dur; 1080p'de 1 cm yaklaşık **1.87 piksel** nominal sınır kalınlığı verir. Mesafe ikiye katlandığında extrusion ikiye çıkar, projeksiyon onu yarıya indirir. Ortografikte depth çarpanı kaldırılır; ortho büyüklüğü projeksiyondan telafi edilir. Bu screen-space görünür kalınlığı korumaya yönelik hull yöntemidir; yüzey normalinin ekrana izdüşümü, sivri köşeler, near-plane clipping ve self-intersection yüzünden her silüet noktasında matematiksel olarak aynı piksel kalınlığı garanti edilmez. Mutlak piksel kalınlığı için ekran konturu kullanılır. World unit genişlik **referans mesafede** tanımlıdır; her mesafede hem aynı fiziksel genişlik hem aynı piksel genişliği mümkün değildir.

## A2 — Normal bake ne zaman gerekir?

İlk hero outline kurulumunda, mesh topolojisi/normal/tangent verisi veya model importu değiştiğinde tekrar çalıştır. Transform hareketi, kamera mesafesi ve global outline rengi/kalınlığı değişince bake gerekmez. Bake dikişlerde aynı konuma denk gelen vertex'leri weld anahtarında birleştirir; triangle köşe açısıyla ağırlıklandırılmış smooth normal hesaplar. UV3'te tangent-space olarak saklandığından skinning sonrası normal/tangent çerçevesiyle yeniden world-space'e çevrilir. Eksik tangent varsa oluşturulur. Kaynak vertex renkleri ve UV/lightmap kanalları korunur; yalnız ayrı hull clone UV3'ü kullanır.

Model Read/Write kapalıysa araç ilgili model importer'larını topluca geçici açar, hull asset'lerini oluşturur ve eski bayrakları `finally` içinde geri yükler. Tek tek importer ayarlama gerekmez. Importer'ı olmayan özel unreadable mesh için hata bildirir. Birbirine tam çakışan ama farklı hareket eden skin parçaları aynı weld noktasında birleşebilir; böylesi hero modellerinde mesh bölünmesi gerekir. Bu bir universal CAD topology onarım aracı değildir.

## A3 — Screen-space outline / Render Graph

`ScreenSpaceOutline` bir ScriptableRendererFeature'dır. Pass yalnız `RecordRenderGraph(RenderGraph, ContextContainer)` uygular; obsolete `Execute(ScriptableRenderContext, ref RenderingData)` yoktur. `AddRasterRenderPass`, `RendererListHandle`, `UseRendererList`, `UseTexture`, `SetRenderAttachment` kullanılır; UnsafePass gerekmiyor.

**Enjeksiyon: `RenderPassEvent.BeforeRenderingTransparents`.** Opaque color/depth ve istenmiş normals hazırken kontur eklenir. Cam/transparent nesneler daha sonra üstünü doğru biçimde örter; post-process ise bundan sonra konturu görüntüyle birlikte işler. Transparent depth/normals desteği iddia edilmez. UI overlay, reflection ve preview kameraları atlanır; Game ve Scene view desteklenir.

3×3 Sobel, `_CameraDepthTexture` lineer eye depth ve `_CameraNormalsTexture` üzerinde ayrı ayrı hesaplanır. Depth gradyanı `8 × max(1 m, nearestDepth)` ile normalize edilir; 0.035 eşik yaklaşık 10 m'de 0.35 m, 20 m'de 0.70 m normalize edilmiş derinlik gradyanı ölçeğine karşılık gelir. Bu değer basit iki piksel farkı değildir, Sobel kernel sonucudur. Normal eşiği ayrıca 12–25 m arasında kademeli yükseltilerek uzak küçük normal detayları bastırılır; depth silüeti kalır.

Layer Mask=Everything olduğunda beyaz 1×1 maskenin clear işlemi ve **tek fullscreen üçgen çizimi** vardır; nesneler maskeye yeniden çizilmez. Filtreli maskede seçili opaque renderer'lar mevcut depth attachment'a ZTest ederek R8 maskeye çizilir, sonra fullscreen kontur uygulanır. Önündeki duvardan arkadaki kitabın konturu sızmaz. Filtreli maske opak yüzeyleri hedefler; özel alpha-cutout/deformation shader'larının kendi mask pass'leri yoksa generic override gerçek kesik yüzeyi temsil etmeyebilir. Bunları ayrı layer'da tut. Kaynak materyalin OutlineEnabled flag'i generic layer mask tarafından okunmaz; bu feature'ın filtresi layer'dır. Adım 1 ToonMask pass'i uyumluluk için korunur.

## A4 — 3.600 kitapta draw call cevabı

**Evet, batch/instancing yok ve kitap başına bir submesh varsayımıyla her kitaba hull eklemek color çizimlerini 3.600 → 7.200 yapar.** Bu bütün frame'in tam iki katı demek değildir; shadow, prepass, UI gibi çizimler ayrıca vardır. SRP Batcher draw call birleştirmez. GPU instancing/static batching gerçek sayıyı değiştirebilir.

| Yöntem | 3.600 görünür, tek-submesh kitap için ek geometri çizimi | Diğer maliyet |
|---|---:|---|
| Her kitaba inverted hull | +3.600 | Ek vertex transform ve hull overdraw |
| Screen-space, Everything | +0 mask/hull | +1 fullscreen draw; normals/depth prepass gerekiyorsa +3.600'e kadar |
| Screen-space, yalnız Book layer mask | +3.600 maske çizimine kadar | +1 fullscreen; gerekirse ayrıca normals/depth prepass |
| 3 hero, her biri tek submesh, hull | +3 | Normal kitaplar screen-space kalır |

Üç hero toplam 20 submesh ise hull ilavesi **20** olur. Ekran filtresini sadece Book layer'a daraltmak bedava değildir; bizim maskeli yol draw-call sayısında hull'a benzer ek çizim üretebilir, fakat pahalı hull genişletmesi/renk shader'ı yapmaz. Bu yüzden **kitaplarda screen-space, hero'larda hull** varsayımın doğru; ilk kullanımda Everything önerilir. Hero'lar ekran konturuna da girer, aynı renkte konturlar örtüşebilir; Book-only maske bu örtüşmeyi ayırır ama tablodaki maliyeti getirir. Gerçek sayılar Frame Debugger ile ölçülmelidir. Renderer zaten normals prepass yapıyorsa o maliyet outline'a ikinci kez yazılmaz. Eski kitap ink MPB üretimi ToonLit kitaplar için durdurulmuştur; yeni kitap hull'ları üretilmez.

## B — Kök neden

Üç dış duvarın ve tavanın dışarıdan kaybolması içe bakan tek yüzlü üçgenlerin kamera backface culling sonucudur. Aynı düzlem ışığın bakış açısından arka yüz olduğunda `ShadowCaster` içindeki `Cull Back` onu gölge haritasına yazmaz. Kamera culling'i ve gölge pass'i culling'i ayrı ayarlardır; iç yüzeyi görebiliyor olman dış güneşi engellediği anlamına gelmez. Ana render pass'e Cull Off koymak tek başına shadow pass'i düzeltmez.

Tavandaki panel aralıkları ise **gerçek açıklıktır**. Ahşap ızgara gölge düşürür, aradaki açıklıklar ışığı geçirir; zemindeki tekrarlayan parlak dikdörtgenler bu geometrinin izdüşümüdür. Eğik güneş yönü tavan boyunca çapraz kesitler üretir. Bunları bias ile gizlemek doğru çözüm değildir. Volumetrik ışın ayrı transparan mesh/shader ile çiziliyorsa ayrıca shadow-aware olması gerekir; bir shadow blocker gölge örneklemeyen görsel ışın mesh'ini silmez.

## B5 — Directional Light kalsın mı?

Kapalı bir iç mekânda directional fiziksel olarak yanlış değildir: doğru kapalı kabukla yalnız gerçek pencerelerden güneş girebilir. Ancak bu oyun için kontrol edilebilir, gölgesi olan **shopfront spotları** daha sade başlangıçtır. Varsayılan Directional Light'ı silmek yerine topluca kapatıp karşılaştır; kapatma sızıntının geometri nedenini onarmaz. Önce kabuğun o ışığı engellediğini doğrula.

`6 - Create Shopfront Daylight Test Rig (Undo)` pencere açıklığına bağlı iki spotu otomatik oluşturur:

- Light Type: Spot, Realtime; pencerenin dış tarafında **0.30 m**, açıklık genişliğinin ±%25'inde, açıklık merkezinden yüksekliğin %30'u yukarıda.
- Yön: içeri, yaklaşık **8.5° aşağı**. Outer/Inner Angle: **70° / 50°**. Range: **18 m**.
- Temperature: **6500 K**, Color: white, `Light.intensity = 3` **her spot için**. Bu renderer'ın sayısal intensity başlangıcıdır; ölçülmüş 3 lux/lumen veya garantili pozlama değildir. Sonraki lighting adımında 2–5 aralığında sahneye göre kalibre edilir.
- Shadows: **Hard**, bias 0.1/0.2; Shadow Near Plane **0.05 m**. Camın shadow casting'i kapalı olmalı veya gerçek açıklık sağlayan materyal kullanılmalı; opak shadow-caster cam spotu kesebilir. Additional Lights Shadows URP'de açık olmalı.

Spotlar pencereyi kendiliğinden parlak bir ışık kaynağı yüzeyine dönüştürmez. Pencereden görünen dış ortam/sky veya emissive yüzeyin görüntü parlaklığı iç raflardan yüksek tutulmalı; başlangıç için dış görünür kaynak 2–4 linear HDR değer aralığında değerlendirilebilir. Mutlak “en parlak pencere” sonucu albedo, exposure, tonemapping ve iç lambalara bağlıdır. Bu adım dış sokak görüntüsünün önüne beyaz bir kart koymaz ve post-process'i değiştirmez; nihai parlaklık oranı sonraki lighting adımında ayarlanır. Gerçek zamanlı rectangle Area Light'ı URP'de spot yerine önermez; bake-only davranışını gerçek zamanlı ışık sanma.

## B6 — Tek yüzlü mimarinin üç çözümü

| Çözüm | Ne düzeltir? | Sınırı / maliyeti |
|---|---|---|
| Mesh Renderer Cast Shadows = Two Sided | Işığa arka yüz olan üçgen de gölge haritasına yazılır | Kamera dış yüzü hâlâ görünmez; panel boşluklarını kapatmaz; çok ince yüzey bias'a duyarlı kalır |
| ToonLit ShadowCaster'da `Cull Back` yerine `Cull Off` | Bu shader'ı kullanan bütün nesnelerde iki yüzlü shadow rasterization | Sadece duvarları değil kitap/prop shadow rasterization'ını da etkiler; materyal/drift yerine shader genel davranışı değişir; delikleri kapatmaz |
| Gerçek duvar/tavan kalınlığı | Dış görünüş, solid geometri, gölge ve bake için sağlam sınır | Bir miktar ek geometri; pencere/kapı açıklıkları doğru modellenmeli |

**Shipping önerim:** basit, gerçek kalınlığı olan kapalı ana mimari kabuk + dekoratif paneller. Dekoratif tek yüzlü kaplamaları gerektiğinde Two Sided yap. Görünmeyecek ana kabuk için aşağıdaki Shadows Only slab'lar uygun ve düşük poligonlu kalıcı temsildir. Oyuncu dışarı çıkabiliyorsa dış duvar yüzeyleri ayrıca modellenmelidir. ToonLit'in ShadowCaster'ını bütün kitaplarda Cull Off'a çevirmedim; toplu mimari renderer onarımı daha dar kapsamlıdır.

## B7 — LightBlockers shell

`3 - Generate or Rebuild LightBlockers (Undo)` tek Room objesinin altında **LightBlockers** üretir. Varsayılan açıklıklı oda: tavan + temel + 3 tam duvar + cephe açıklığının 4 kenarı = **9 kapalı box slab**. Pencere kapalıysa 6 slab. Primitive collider'ları kaldırılır; `MeshRenderer.shadowCastingMode = ShadowsOnly`, receiveShadows kapalı, dedicated shader yalnız ShadowCaster + Meta içerir. Kamera color/depth pass'i yoktur; eski görünmez ama kamera depth yazan InvisibleOccluder kullanılmaz.

Başlangıç: görünür yüzeyin dış tarafına **2 cm boşluk**, slab kalınlığı **20 cm**. Slab merkezinin yüzeye uzaklığı 2+10=**12 cm** olur. Kenarlar dış boyuta uzatıldığı için tavan-duvar ve köşeler örtüşür; dikiş bırakılmaz. Tavan blocker'ı dekoratif kiriş/panel grubunun üzerindedir ve gerçek panel deliklerinden geçebilecek dış ışığı keser. Shell gizlendiğinde aydınlık karo deseninin geri gelmesi doğrudan karşılaştırma sağlar.

Çok büyük offset pencere üstü/duvar kenarında dışarıdan fazla ışık girebilecek boş cep yaratır, gölgeyi görünür yüzeyden ayırır. Sıfıra yakın offset ince/çakışan caster ve bias hassasiyetini artırır. 2 cm model toleransı içindir; asıl güvence 20 cm kalınlık ve köşe örtüşmesidir. Offset'i bias yerine rastgele büyütme. Modelde gerçek taşıyıcı tavan yüzeyi başka yükseklikteyse Room Bounds'u ona göre doğrula.

“Her dış ışığı engeller” yalnız **bu slab'ları gölge haritasına alan, gölge üreten** ışıklar için geçerlidir. Light Culling Mask'te shell GameObject layer'ı (varsayılan Default) dışlanmamalı; Rendering Layer maskelerinin de eşleşmesi gerekir. ShadowDistance/atlas dışında kalan caster, Shadows=None ışık, ambient SH/sky, eski lightmap/APV ve shadow örneklemeyen volumetrik efekt fiziksel slab'ı otomatik dikkate almaz. Araç slab'ları Contribute GI yapar; fakat mevcut bake'i otomatik silmez veya yeniden üretmez. Lightmaps/APV yeniden bake edilmelidir.

## B8 — Tarama ve tek tıklama

`1 - Report Architecture and Open Meshes` tüm eşleşen MeshRenderer'ları gezer, aynı shared mesh'i bir kez analiz eder. Editor `MeshUtility.AcquireReadOnlyMeshData` ile Read/Write açmadan vertex/index verisi okur. Konuma göre weld sonrası boundary/non-manifold edge sayısını ve düzlemselliği raporlar. Böylece hard-normal seam'li kapalı kutu sırf split vertex nedeniyle açık sanılmaz. Analiz hataları ve named ShadowCaster pass'i bulunmayan materyaller ayrıca bildirilir.

Keyfi bir mesh'in sadece topolojisinden “kesin tek yüzlü materyal” sonucu çıkarılamaz: kapalı ters-winding mesh, çift kopya üçgenler ve shader culling ayarı bunu etkiler. Araç bunu **açık/düzlemsel/tek-kabuk adayı** diye raporlar. `2 - Repair Architecture Shadow Casting (Undo)` seçili mimari kapsamındaki bütün normal renderer'ları konservatif olarak TwoSided yapar; var olan ShadowsOnly korunur. Başlangıç/sonuç/changed adetleri Console'a yazılır. Kapalı meshleri kapsam dışı bırakmak istiyorsan mimari katman/grup seçimini daralt; nesne nesne işlem yoktur.

## B9 — 20 × 12 × 4 m başlangıç gölge ayarları

Oda köşegeni yaklaşık 23.7 m. Aşağıdakiler **ölçek metre olduğu varsayımıyla test başlangıcıdır**, her model/atlas için evrensel hatasız sabitler değildir. `4 - Apply Room Shadow Baseline (Undo)` aktif kalite seviyesindeki URP asset'ini ve mevcut gölgeli ışıkların pipeline bias kullanımını birlikte ayarlar.

| Ayar | Başlangıç | Yanlışsa görülen belirti |
|---|---:|---|
| Depth Bias | **0.1** | Çok yüksek: gölge temas yüzeyinden kopar, peter-panning; çok düşük: self-shadow acne/çizgiler |
| Normal Bias | **0.2** | Çok yüksek: caster normal yönünde fazla ötelenir, ince duvar/kenarlardan ışık sızar; çok düşük: yüzey acne'si |
| Max Distance | **35 m** | Mesafe sınırından sonra realtime gölgeler solar/kaybolur; panel ızgarasıyla aynı desen yapması beklenmez |
| Cascade Count | **2**, split **0.35** | Directional ışığa aittir; fazla bölüşüm/yanlış split yakın-uzak netlik ve seam/popping sorunları yaratır; directional kapalıysa etkisiz |
| Soft Shadows | **Off**, ışık Shadows=Hard | Teşhiste açık filtre sınırları bulanıklaştırır; geometrik deliği kapatmaz |
| Main Shadow Resolution | **2048** kontrol et | Düşük çözünürlük titreme/dişlenme ve bias duyarlılığı; araç çözünürlüğü değiştirmez |
| Additional Lights Shadows / Atlas | **On / 2048** kontrol et | Kapalıysa spotlardan gölge yok; atlas yetersizse bazı ışıkların shadow tile'ları düşebilir |

Bias Inspector değerleri doğrudan “metre” değildir; URP onları ışık/projeksiyon/texel ölçeğiyle kullanır. Acne kalırsa 0.1/0.2 çevresinde küçük adımlarla artır; sızıntı/peter-panning kalırsa önce geometri/maske/passta hata arayıp yüksek bias'ı düşür. Cast Shadows TwoSided hiç ShadowCaster pass'i olmayan shader'a pass eklemez. `4` menüsü mevcut Shadows=None lambaları otomatik gölgeli yapmaz; bütün lambalara shadow açmak sonraki performans/lighting kararını gerektirir.

## B10 — Sırayla doğrula

1. Sahneyi kaydet. Room ayarını oluştur, mimari kapsamı/bounds/pencere gizmosunu doğrula; raporu çalıştır. Console sayılarını kaydet. Final teşhis sırasında önce directional açık kalsın.
2. `2 - Repair Architecture Shadow Casting`: dışa bakan ışığın tek-yüzlü duvar üzerinden sızması azalmalı. Gerçek tavan aralıklarının aydınlık karo deseni **hâlâ kalabilir**; bu beklenir.
3. `3 - Generate or Rebuild LightBlockers`: gerçek tavan aralıklarından gelen çapraz ışık/karo deseni realtime direct light'ta kaybolmalı. LightBlockers'ı kapat/aç; desen yalnız kapalıyken dönüyorsa geometrik nedeni doğruladın. Pencere açıklığından gelen ışık kalmalı. Önce güneşi kapatarak test etme; aksi halde hatayı yalnız gizlemiş olursun.
4. `4 - Apply Room Shadow Baseline`: artık kalan temas ayrılması/acne/mesafe sorununu değerlendir. Bias'ın geometri deliğini kapatması beklenmez.
5. Eski lightmaps/APV varsa yeniden bake et. LightBlockers aç/kapa baked dokuyu değiştirmez. Sabit parlak karo bake'e yazıldıysa yeni bake olmadan kaybolmasını bekleme; BakedInfluence'ı sıfırlayıp direct/baked farkını yalnız teşhis için karşılaştırabilirsin.
6. İstersen `5 - Disable Directional Lights for Interior Test`; bu, doğru kabuğu doğruladıktan sonraki ışık kaynağı seçimidir. **Oda belirgin biçimde kararacak: beklenen sonuç budur.** Eskiden sızan dış ışık odayı yanlış aydınlatıyordu. Exposure/ambient'i hemen yükselterek hatayı örtme.
7. İhtiyaç varsa `6 - Create Shopfront Daylight Test Rig` ile yalnız pencere kaynaklı, gölgeli ışığı karşılaştır. Nihai lamba atmosferi/post-process bir sonraki adımdır. En son hero hull ve ekran konturunu birlikte kontrol et; mimari ışık testinde konturu kapatmak teşhisi kolaylaştırır.

## API ve doğrulama

Kaynak karşılaştırması Unity Graphics **6000.5**, commit `c930feab9f105738328d183bb3c32758d8da0ac3`, paket sürümü **17.5.0** ile yapıldı:

- [Render Graph builder bağımlılık/attachment API'leri](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/IRenderGraphBuilder.cs)
- [URP DrawObjectsPass renderer list örneği](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/Runtime/Passes/DrawObjectsPass.cs)
- [URP asset bias/distance ve soft-shadow erişimi](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs)
- [Editor mesh okuma API'si](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MeshUtility.AcquireReadOnlyMeshData.html)
- [TwoSided shadow davranışı](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.ShadowCastingMode.TwoSided.html)

`supportsSoftShadows` setter'ı URP 17.5'te internal olduğu için araç doğrulanmış `m_SoftShadowsSupported` SerializedProperty'sini Undo ile değiştirir. Shader kaynaklarında Built-in include ve eski Execute yolu yoktur.

`Tools/Rendering/compile_outline_matrix.py` ile gerçek URP/Core include'larına karşı **26 HLSL entry/keyword kombinasyonu DXC'de geçti**. Perspektif/ortografik kalınlık telafisi ve shell açıklık/örtüşme hesabı sayısal olarak kontrol edildi. Bunlar Unity C# / ShaderLab import veya sahne görüntü testi yerine geçmez. Bu çalışma ortamında Unity Editor bulunmuyor; menüler Unity içinde çalıştırılmadı, gerçek sahneye blocker/hero child'ları henüz yerleştirilmedi. İlgili menüler bunları senin açık sahnenin güncel hiyerarşisine Undo destekli uygular. C# derlemesi, renderer feature çalışması, bake ve frame maliyeti Unity'de doğrulanmalı.
