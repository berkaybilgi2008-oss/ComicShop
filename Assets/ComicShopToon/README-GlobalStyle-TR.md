# Adım 1 — Tek global toon görünümü

Hedef proje: Unity **6000.5.9f1**, URP **17.5.0**, Forward+. Yeni sistem `ComicShop/ToonLit` shader adını ve mevcut shader GUID'sini korur. Işık konumlarını, sahne geometrisini veya Render Graph renderer feature'larını değiştirmez.

## Kullanım

`Tools > ComicShop > Toon Style > Select Global Style` tek ayar asset'ini seçer. `Resources/ComicShopToon/DefaultToonStyle.asset` editörde otomatik uygulanır, oyuncuda otomatik yüklenir. Sahneye elle component eklemek gerekmez. İstersen bir `ToonStyleController` ile başka bir ToonStyleAsset seçebilirsin; ikinci controller global değerlerle yarışamaz ve uyarı verir. Bir controller kaldırılınca kalan controller veya varsayılan asset devralır.

`ExecuteAlways` controller OnEnable, OnValidate sonrasındaki ana-thread callback ve runtime Update üzerinden değerleri yayınlar. Asset değişiklikleri editör güncellemesinde Play'e girmeden Scene/Game view'a yansır. OnValidate yükleme thread'inde çağrılabildiği için Shader API orada doğrudan çalıştırılmaz. ScriptableObject renkleri çalışma renk uzayına çevrilerek global gönderilir.

**Tek tek materyal ayarlama yok.** 30 eski ToonLit ve 266 uygun opak V16 materyali geçirilmiştir. Eski palet renkleri BaseColor'a katılmış, materyale kaydedilmiş stil değerleri temizlenmiştir. Cam/saydam, unlit, özel culling veya offset isteyen 38 V16 materyal değiştirilmemiştir. NPC'nin ayrı ToastRanger shader'ı ve özel outline/ink efektleri bu opak yüzey geçişinin dışındadır; dolayısıyla projedeki her özel shader'ın artık globali kullandığı iddia edilmez. BookToonEffect kitapları ortak ToonLit shader'ı ve Resources materyal şablonuyla oluşturur; kaynak materyal başına cache korunur. Eski BookCel kaynak dosyası uyumluluk için durur ancak BookToonEffect artık onu yüklemez.

## Parametre sahipliği

HLSL globals `_Toon` öneki taşır; örneğin `_ToonShadowSteps`. HLSL içindeki `_ShadowSteps` erişimi globali veya açıkça seçilmiş override'ı çözen alias'tır. Böylece eski materyallerde kalmış `_ShadowSteps` gibi kayıtlar global değeri gölgeleyemez.

| Parametre | Varsayılan | Sahibi | İstisnai override |
|---|---|---|---|
| ShadowSteps, RampSmoothness | 2, 0.02 | Global asset | Her biri bağımsız |
| ShadowTint | #3A2E52 | Global asset | Evet |
| BakedSteps, BakedInfluence | 3, 0.2 | Global asset | Her biri bağımsız |
| LightFalloffScale | 8 | Global asset | Evet |
| SpecEnabled, SpecThreshold, SpecStrength | 0, 0.96, 0.4 | Global asset | Her biri bağımsız |
| SpecColor | (1, 0.98, 0.9) sRGB | Global asset | Evet |
| RimEnabled, RimThreshold, RimLitOnly, RimStrength | 0, 0.75, 1, 0.2 | Global asset | Her biri bağımsız |
| RimColor | (0.85, 0.9, 1) sRGB | Global asset | Evet |
| HalftoneEnabled, HalftoneScale, HalftoneStrength | 1, 8 piksel, 0.4 | Global asset | Her biri bağımsız |
| HalftoneAngle, HalftoneRadius | 45°, hücrenin 0.27'si | Global asset | Her biri bağımsız |
| HalftoneColor | #241C35 | Global asset | Evet |
| Aktif stil asset'i / global keyword yayını | DefaultToonStyle | Yalnız global controller | Hayır |
| BaseColor, BaseMap, texture tiling/offset, BumpMap | Beyaz, beyaz, 1/0, düz normal | Gerçek per-material yüzey verisi | Global karşılığı yok |
| HalftoneEnabled yüzey anahtarı, OutlineEnabled | Açık, açık | Gerçek per-material iki anahtar | Global halftone iznini aşamaz; açık bir stil override istisnası hariç |

Global stil parametrelerinin tamamı varsayılan olarak globali izler. Normal map kullanımı texture varlığından otomatik çıkarılır; üçüncü bir kullanıcı stil anahtarı değildir. OutlineEnabled yalnız mevcut ToonMask pass'ine veri verir; bu adım yeni outline uygulaması eklemez. Önceden var olan tüm farklı outline efektlerini kapattığı anlamına gelmez.

Advanced bölümünde her parametre için bağımsız `Override` seçimi bulunur. Tümü kapalıyken `_TOON_LOCAL_STYLE` derlenmez: override seçimi veya override uniform okuması yoktur. **Talepteki her parametreye ayrı keyword yerine bilinçli olarak tek shader_feature_local_fragment kapısı kullanıldı.** 21 bağımsız keyword yalnız stil için 2^21 = 2.097.152 kombinasyon üretirdi. Seçili override varyantı parametre bazında uniform seçim yapar; kullanılmayan override varyantı build'de strip edilebilir. Sabit CBUFFER yerleşimi için override alanları bellekte her zaman bulunur; “sıfır maliyet” bellek veya build süresi için mutlak iddia değildir.

`Reset All ToonLit Materials to Follow Global` tüm proje ToonLit materyallerinde override flag'lerini kapatır ve keyword'leri eşler. Albedo/normal ve iki yüzey anahtarı korunur; Undo desteklidir. Runtime'da sıfırdan keyword kombinasyonu üretilecekse ilgili materyal varyantının build'de tutulması gerekir. Kitapların kullandığı halftone varyantı Resources şablonuyla tutulur.

## Shader / ışık davranışı

- `UnityPerMaterial` CBUFFER bütün pass ve varyantlarda aynı düzeni kullanır. Texture nesneleri dışarıda, renkler/ST/materyal alanları içeridedir. Global görünüm uniformları CBUFFER **dışındadır** ve ShaderLab Properties listesinde yoktur. Aksi halde Unity'nin materyal buffer'ı globali ezer; SRP Batcher materyal verisini cache'lerken tek global kaynak çalışmaz. SRP Batcher uyumlu düzen draw call'ların otomatik birleştiği anlamına gelmez. Klasik instancing varyantları mevcuttur; DOTS desteği iddia edilmez.
- UniversalForward, ShadowCaster, DepthOnly, DepthNormals, Meta eksiksizdir. Eski renderer feature uyumluluğu için altıncı ToonMask pass'i korunmuştur. DepthNormals normal map'i ve URP oct encoding'i destekler. Meta, efektlerle karartılmamış albedoyu bake'e verir.
- Ana ışık `NdotL * shadowAttenuation` üzerinden 2/3 banda ayrılır. Cascade fragment world position üzerinden seçilir. Screen-space shadow varyantının koordinatı ayrıdır.
- Forward+ ek directional döngüsü ve `GetAdditionalLightsCount` + `LIGHT_LOOP_BEGIN/END` cluster döngüsü gerçektir. Forward+'ta GetAdditionalLightsCount sıfır dönebildiğinden düz `for(count)` yeterli değildir. Ek spot/point gölgeleri `GetAdditionalLight(..., shadowMask)` ile alınır. Işık zayıflaması da bantlanır; bant üzerine düzgün bir radial gradient çarpılmaz. Sekiz lamba aynı shader yolunu kullanabilir; ışık/atlas ayarlarının etkin olması renderer'a bağlıdır.
- Gölge `lerp(ShadowTint * albedo, albedo, band)` ile mor-mavi çarpana gider. Tam doygun tek kanallı albedoda bu çarpım mevcut olmayan renk kanalını yaratamaz; istenen formülün bu sınırı korunmuştur. Lambaların renk katkısı sınırlanır; çok sayıda lamba sınırsız beyazlatmaz.
- `OUTPUT_LIGHTMAP_UV`, `OUTPUT_SH4`, `SAMPLE_GI` ile statik/dinamik lightmap, SH ve APV L1/L2 alınır. Mixed-light GI karışımı posterizasyondan önce yapılır. GI RGB kanalları 0–1 aralığında 3 seviyeye yuvarlanır, sonra BakedInfluence ile eklenir; HDR baked enerji bilinçli olarak clamp edilir. APV bake/veri ve renderer ayarı ayrıca mevcut olmalıdır.
- Specular tek `step` eşiğidir, ışıklar arasında max ile birleştirilir. Rim de `step` kullanır; yalnız aydınlık taraf seçeneği vardır.
- 0.02 RampSmoothness yalnız dar bant sınırı geçişidir. Matematiksel olarak tamamen kesintili, hiçbir ara değer içermeyen sonuç için **0** seç. Yumuşak kenar isteği ile “hiç gradient olmasın” ancak bu ayrımla birlikte sağlanabilir.
- URP fog: `multi_compile_fog`, `ComputeFogFactor`, `MixFog`. Built-in `UNITY_FOG_COORDS` / `UNITY_APPLY_FOG` kullanılmaz. Sis kendi doğası gereği sürekli renk karışımı oluşturur; tamamen düz final renk istendiğinde sahne sisi kapalı tutulmalıdır. Post-processing de bantları değiştirebilir.

## Halftone sabitleme

Noktalar fragment `SV_POSITION.xy` üzerinden raster ekran koordinatında hesaplanır; dünya pozisyonu, obje UV'si, kamera pozisyonu veya zaman desen fazına girmez. Render/output çözünürlüğü oranıyla ölçeklenir, sonra 45° döndürülür. Böylece aynı ekran pikselindeki faz kamera hareketinden bağımsızdır, mesafeyle nokta büyümez/küçülmez. `fwidth` tabanlı dar kenar filtresi ve minimum 4 piksel aralık, nokta kenarındaki aliasing'i azaltır. Maske yalnız doğrudan aydınlatmanın en alt bandındadır.

Bu **ekrana kayıtlı** bir baskı desenidir: nesne hareket ettikçe nesne ekran deseninin altından geçer; yüzeye yapışık bir doku değildir. Ekrana ve nesneye aynı anda sabit olma sözü verilemez. Geometrinin hareketli gölge sınırı doğal olarak değişir. TAA, temporal upscaling veya motion blur daha sonra bu deseni history ile karıştırıp iz bırakabilir; shader tek başına bunu garantiyle engelleyemez. Sabit render ölçeği + SMAA/FXAA ile incele; TAA sonrası ayrı bir compositing çözümü bu adımın kapsamı dışında.

## Keyword seti ve varyant sayısı

| Tür | Keyword seti | Çarpan |
|---|---|---:|
| Local / yalnız fragment | `_TOON_LOCAL_STYLE`, `_TOON_HALFTONE` | 2 × 2 |
| Local / vertex ve fragment | `_NORMALMAP` | 2 |
| Global / fragment | `_TOON_GLOBAL_SPECULAR`, `_TOON_GLOBAL_RIM`, `_TOON_GLOBAL_HALFTONE` | 2 × 2 × 2 |
| URP ana gölge | boş, `_MAIN_LIGHT_SHADOWS`, `_MAIN_LIGHT_SHADOWS_CASCADE`, `_MAIN_LIGHT_SHADOWS_SCREEN` | 4 |
| URP ek ışık / Forward+ | `_ADDITIONAL_LIGHTS`, `_CLUSTER_LIGHT_LOOP` | 2 × 2 |
| URP ek gölge | `_ADDITIONAL_LIGHT_SHADOWS` | 2 |
| URP soft shadow | boş, `_SHADOWS_SOFT`, `_SHADOWS_SOFT_LOW`, `_SHADOWS_SOFT_MEDIUM`, `_SHADOWS_SOFT_HIGH` | 5 |
| GI | `DYNAMICLIGHTMAP_ON`, `LIGHTMAP_ON`, `DIRLIGHTMAP_COMBINED`, `SHADOWS_SHADOWMASK`, `LIGHTMAP_SHADOW_MIXING`, `USE_LEGACY_LIGHTMAPS` | 2⁶ |
| Fog | boş, `FOG_LINEAR`, `FOG_EXP`, `FOG_EXP2` | 4 |
| APV include | boş, `PROBE_VOLUMES_L1`, `PROBE_VOLUMES_L2` | 3 |
| Instancing | boş, `INSTANCING_ON` | 2 |

Sanat/yerel feature ürününün üst sınırı **64**; Forward pass'in tüm pragma seçeneklerinin ham Kartezyen üst sınırı **15.728.640**. Bu sayı final build sayısı değildir: geçersiz GI kombinasyonları, URP asset/renderer ayarları, materyal keyword kullanımı, platform ve shader-stage ayrımı stripping/deduplication sonucunu belirler. Yardımcı pass ham ürünleri: ShadowCaster 4, DepthOnly 2, DepthNormals 8, ToonMask 2, Meta 1. Altı pass toplam nominal üst sınırı 15.728.657; vertex/fragment ayrı compiler program sayısı ile karıştırılmamalıdır. XR/procedural instancing hedefleri bu sayıya dahil değildir.

Runtime değiştirilebilir global feature'lar `multi_compile_fragment` kullanır; yalnız `shader_feature` kullanmak başlangıçta kapalı specular/rim varyantını strip edip runtime açmayı bozardı. Materyal feature'ları `shader_feature_local` kullanır. `ToonVariantReport` player build sırasında geç aşamadaki gerçek shader-stage aday sayısını pass bazında Console'a yazar. **Unity player build yapılmadan final derlenmiş varyant sayısı verilemez.** Bu geniş ham matrisin ilk import/build süresi hedef makinede ölçülmelidir.

## Doğrulama ve sınırlar

Unity Graphics `6000.5/staging`, sabit commit `c930feab9f105738328d183bb3c32758d8da0ac3` paket metadata'sı 17.5.0 olarak doğrulandı. Kaynaklar:

- [URP LitForwardPass / APV ve GI çağrıları](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl)
- [Forward+ cluster light loop](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl)
- [URP Core / `_CLUSTER_LIGHT_LOOP`](https://github.com/Unity-Technologies/Graphics/blob/c930feab9f105738328d183bb3c32758d8da0ac3/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl)

198 temsilî HLSL entry/keyword kombinasyonu Microsoft DXC 1.9 ile, gerçek URP/Core include'larına karşı başarılı derlendi. Kontrol D3D tanımları + SM6 DXIL kullanır; Unity'nin ShaderLab importer'ı, C# derleyicisi, D3D11 SM4.5 player build'i veya GPU görüntü testi değildir. Bu ortamda Unity Editor bulunmadığından bunlar çalıştırılmadı. 60 FPS hedefi doğrulanmış bir sonuç değildir.

Tekrarlanabilir kontrol: `python Tools/Rendering/compile_toon_matrix.py --unity-source <Graphics-checkout> --dxc <dxc-path> --report <report-path>`.

Unity'de `Tools > ComicShop > Toon Style > Validate Shader Import` import mesajlarını kontrol eder. Ardından aktif Forward+ sahnede ana ışık cascade sınırı, altı-sekiz lamba, APV/lightmap ve kitaplar kontrol edilmelidir. Materyal Inspector'daki SRP Batcher uyumu ve Frame Debugger/Profiler sonuçları hedef PC'de doğrulanmalıdır. Bu menü tek başına bütün varyantları derlemiş sayılmaz.

## Neyi ayarlamalısın?

Önce yalnız global asset'teki **ShadowTint** ile soğuk gölge tonunu, **ShadowSteps** ile iki/üç bant karakterini seç. Mutlak sertlik için **RampSmoothness = 0**, dar kenar yumuşatma için **0.02**. **BakedInfluence** düşük tutulmalı ki GI gölge rengini yıkamasın. **HalftoneScale = 8** ve **Strength = 0.4** başlangıç baskı dokusudur; daha sakin görünüm için Strength'i azalt. Lambaların bant kapsaması için **LightFalloffScale** kullan. En son global **SpecEnabled/RimEnabled** aç; önce temel ışık bantlarını değerlendir.
