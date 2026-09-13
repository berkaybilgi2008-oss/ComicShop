# Toast Ranger — kitap tutma + ayarlanabilir kol yüksekliği

## Kitapla yürüme ve koşma

Yeni klipler: **IdleBook, WalkBook, RunBook**. Sağ kol kitabı alttan destekler; bacaklar ve diğer tüm kemikler normal Idle/Walk/Run klipleriyle birebir aynıdır. Tek elle taşıma pozudur; parmakların kitap kenarını kavradığı ayrı bir parmak rig'i yoktur.

1. Unity dosyalarını aynı Assets/ToastRanger klasöründe güncelleyin.
2. ToastRangerRig.json dosyasını seçip Tools > Toast Ranger > Create Animated Player Visual komutunu yeniden çalıştırın. Önceki üretilmiş prefab kendiliğinden güncellenmez; yeni prefab oluşur.
3. Yeni prefabı Player altında kullanın ve Motion Source alanını Player'a bağlayın.
4. Play sırasında **Toast Book Carry > Carrying Book** seçeneğini açın. Normal hız algılama açıkken kitapla bekleme/yürüme/koşma arasında geçiş yapılır.
5. **Hand Height** kaydırıcısını -0.10 ile +0.16 arasında ayarlayın. Birim model metresidir; karakter ölçeği hesaba katılır. Değer 0 varsayılan pozdur. Bu aralık içinde bile hedef kolun erişimini aşarsa erişilebilir noktaya sınırlanır.

Yükseklik düzeltmesi Animator'dan sonra sağ üst kol, dirsek ve bilek dönüşlerine uygulanır. Kemik uzunlukları değiştirilmez; elin animasyondaki dünya yönü korunur. Önceki karedeki düzeltme geri alınarak birikme önlenir. Normal Animator Update Mode ve tek tip XYZ ölçek kullanın; aynı kola başka bir IK/rig bileşeni eklemeyin. Bacak animasyonları bu bileşen tarafından değiştirilmez.

**Kitabı bağlama:** Sağ elin altındaki BookSocket tutma noktasıdır. Önizleme için basit PreviewBook eklenmiştir. Gerçek kitabınızın görselini BookSocket altına koyun, local konum/dönüşünü kitabınızın pivotuna göre ayarlayın ve PreviewBook'u kapatıp bileşendeki Preview Book referansını boşaltın. Bu paket fizik/network sahibi kitap nesnesini otomatik taşımaz; mevcut alma-bırakma kodunuzdan görseli bağlayın. Kitap boyutu değişirse dirsek/gövde kesişmesini sahnede kontrol edin.

Koddan taşıma durumunu değiştirmek için ToastBookCarry.SetCarrying(true/false) kullanın. Carrying Book durumu ve Hand Height ağ üzerinden otomatik senkronize edilmez; multiplayer sisteminiz bu değerleri diğer oyunculara iletmelidir. Inspector'da Play sırasında yaptığınız ayarları durdurmadan not edip prefab üzerinde kaydedin; Play değişiklikleri normalde kalıcı değildir.

Yeni kitaplı klipler ToastRanger_Animated.glb içinde Blender'a da aktarılır. Unity'ye özel Hand Height düzeltmesi GLB'ye gömülü değildir. Kitap GIF'lerindeki kırmızı kitap yalnızca ölçü referansıdır.

## Önceki yüz düzenlemesi ve temel kurulum

Yüzdeki tüm küçük benek/kabartı parçaları kaldırıldı. Yanakların çapı yaklaşık 1 cm, yüzeyi neredeyse düz, rengi ten rengine yakın pembe-bej (#F0CCA0). İnce gövde, uzun kollar, düzeltilmiş omuzlar ve cips şapka korundu.

18 kemikli, ağırlıklandırılmış iskelet ve altı klip vardır. Idle/IdleBook 2 saniye, Walk/WalkBook 1 saniye, Run/RunBook 0.64 saniyedir. Animasyonlar yerinde döner; Player'ın dünya üzerindeki hareketini mevcut oyun kodu sağlar.

## Unity'ye ekleme — yeni animasyonlu yol

1. Paketteki Unity klasörünün içeriğini projenizin Assets/ToastRanger klasörüne koyun. Önceki dosyalar varsa aynı isimli dosyaları güncelleyin; aynı C# sınıflarını ikinci klasöre tekrar kopyalamayın.
2. Derleme bitince Project panelinde **ToastRangerRig.json** dosyasını seçin.
3. **Tools > Toast Ranger > Create Animated Player Visual** komutunu çalıştırın.
4. Oluşan ToastRangerAnimated klasöründe **ToastRanger_PlayerVisual.prefab** bulunur. Araç ayrıca meshleri, materyalleri, Idle.anim / Walk.anim / Run.anim dosyalarını ve Locomotion.controller dosyasını üretir.
5. Prefabı mevcut Player objenizin altına yerleştirin. Önceki statik görselin Renderer'larını kapatın. Player kökündeki hareket, kamera, CharacterController ve network bileşenlerini koruyun.
6. Yeni görselde **Toast Locomotion > Motion Source** alanına Player objesini sürükleyin. Automatic Speed açık olsun. Bileşen Player'ın yatay yer değiştirmesinden hız hesaplar ve Animator'daki Speed parametresini günceller.
7. Walk Speed ve Run Speed alanlarını oyununuzun gerçek yürüme/koşma hızlarına ayarlayın. Varsayılanlar 1 ve 2.5 metre/saniye; bunlar başlangıç değerleridir. Ölçek ve oyun hızına göre ayak kaymasını azaltmak için klip hızını da ayarlamak gerekebilir.

Modelin boyu 2.30 metre. 0.8 ölçek yaklaşık 1.84 m yapar. Model pivotu ayak tabanına yakın; Player pivotunuz kapsülün ortasındaysa modelin local Y konumunu ayaklara göre aşağı alın. Collider ölçüleri paket tarafından değiştirilmez.

Shader URP içindir. Built-in/HDRP için uyarlanmalıdır. Kontur: Outline.mat > Width in metres (0.003). Ana Directional Light cel aydınlatmayı sürer; Point/Spot ışıkları shader'a dahil değildir.

### Hızlı animasyon kontrolü

Automatic Speed'i kapatın. Play sırasında Animator panelindeki Speed parametresini 0 (bekleme), 1 (yürüme), 2 (koşma) yapın. Hareket sistemine bağladıktan sonra Automatic Speed'i açın.

Mevcut koddan hız göndermek isterseniz Automatic Speed'i kapatıp ToastLocomotion.SetSpeed(yatayHiz) çağırabilirsiniz. Bileşen klavye girdisi okumaz, Player'ı hareket ettirmez ve network mesajı göndermez.

Multiplayer'da uzak oyuncunun görseli senkronize transformun hareketinden animasyon seçebilir. Bu yöntem animasyon fazını oyuncular arasında tam eşleştirmez. NetworkAnimator eklenmedi. Birinci şahıs kameranızda kafa/şapka görünürlüğü ve katman ayarı proje içinde yapılmalıdır.

## Blender

File > Import > glTF 2.0 ile **ToastRanger_Animated.glb** dosyasını açın. İskelet, ağırlıklar ve altı animasyon GLB içindedir. Dope Sheet / Action Editor veya NLA Editor'da animasyonları seçebilirsiniz; gösterim Blender sürümüne bağlıdır.

Cel görünümü için mesh seçiliyken Text Editor'dan Blender_Cel_Setup.py betiğini bir kez çalıştırın; EEVEE Rendered görünümü kullanın. Betik cel materyalleri, iskeleti izleyen kontur kopyası ve Sun ışığı ekler. File > Save As ile .blend kaydedebilirsiniz. Hazır .blend veya FBX üretilmedi. GLB'nin standart materyalleri Unity cel shader'ıyla aynı görünmez.

## Kapsam ve doğrulama

- 18 kemik, en fazla 3 etkili kemik ağırlığı/vertex (4 slotlu veri). Özel Generic/transform iskeleti; Humanoid avatar veya Mixamo eşlemesi yok.
- İki eklemli bacak hedef çözümü, dönüşümlü ayak kaldırma, zıt kol salınımı, koşuda dirsek bükümü ve gövde eğimi. Yüz ve şapka baş kemiğine bağlıdır.
- Diz/dirseklerde ek geometri ve ağırlık geçişi vardır. Giysi parçaları kesişen ayrı yüzeylerdir; birleşik retopoloji, parmak/yüz rig'i, LOD veya cloth simülasyonu yoktur. Aşırı pozlarda kesişmeler oluşabilir.
- GLB bağımsız tekrar okundu: bind pose, kemik indeksleri, ağırlık toplamları, koordinatlar, normaller ve döngü eşleşmesi kontrol edildi. Sonuçlar Validation.json içindedir.
- GIF'ler dışa aktarılmış GLB'nin gerçek iskelet/keyframe verisinden çizildi. Unity/Blender ekran kaydı değildir; önizlemede kontur ve aydınlatma yaklaşık uygulanır.
- Unity/Blender bu ortamda çalıştırılamadı. C# kurulum aracı ve shaderlar motor içinde derleme/oynatma testinden geçmedi. Son entegrasyon doğrulaması kendi Unity projenizde yapılmalıdır.
- 15 renk materyali; kontur ek çizim maliyeti getirir. Parça bazlı üst üste gelen UV'ler vardır; benzersiz boyanabilir atlas/normal map/AO bake yoktur.

## Dosya seçimi

**Animasyonlu Player:** Unity/ToastRangerRig.json + Create Animated Player Visual menüsü.

**Blender:** ToastRanger_Animated.glb.

**Eski statik yol:** ToastRanger.glb ve Unity/ToastRanger.obj statiktir. Eski Create Cel Prefab from selected OBJ menüsü animasyon üretmez. Yeni Player kurulumu için JSON yolunu kullanın.

Kaynakları yeniden üretmek için Python + NumPy + Pillow ile build_rig.py, ardından validate_and_preview.py çalıştırın. build_rig.py statik modeli de yeniden oluşturur. Hızlı çizici opsiyoneldir: Linux'ta g++ -O3 -shared -fPIC fast_raster.cpp -o fast_raster.so; derlenmemişse Python çizici kullanılır.

## Resmi kaynaklar

- [Unity SkinnedMeshRenderer](https://docs.unity3d.com/6000.0/Documentation/Manual/class-SkinnedMeshRenderer.html)
- [Unity animasyon eğrileri](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimationUtility.SetEditorCurve.html)
- [Unity Animator hız parametresi](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.SetFloat.html)
- [Khronos glTF skin ve inverse bind matrisleri](https://github.khronos.org/glTF-Tutorials/gltfTutorial/gltfTutorial_020_Skins.html)
- [Unity URP aydınlatma](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-lighting.html)
