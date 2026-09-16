# Işık düzeltmesi
Unity 6000.5 / URP 17.5, Forward+, Render Graph.

## Uygulama
Güncel sahne açıkken Tools > ComicShop > Rebuild Active Scene Lighting çalıştır.
Komut önce Assets/LightingBackups altında sahnenin kopyasını kaydeder.
Altı kaynak sarkıt Spot, tezgâh Point, neon Point ve tek kaynak güneş kullanılır.
Güneş ve iki Spot gerçek gölge üretir. Kaynak güneşin mevcut pencereye bakan yönü korunur.
Eski otomasyonun ComicShop Pendant Spot ışıkları ve varsayılan Directional Light kapatılır.
Dükkân dışında bulunan oyuncu el fenerlerine dokunulmaz.
Warm materyallerin gölge tabanı 0.32, baked influence 0.08 olan ayrı kopyaları atanır.
Sonucu kontrol edip sahneyi kaydet. Tekrar çalıştırmak yeni ışık veya materyal çoğaltmaz.

## Neden
ShopV16Appearance her karede Light.enabled durumundan bağımsız sanal ışık yazıyordu.
V16 shader gölgeleri yalnız ana ışığın yönü sanal ışıkla eşleşirse kabul ediyordu.
V16 artık gerçek URP Forward+ ışıklarını ve gölgelerini örnekler.
ToonForward ışık şiddetini ikili eşik yerine sürekli çarpan olarak uygular;
mekânsal aydınlatma bantları korunur, ambient tabanı ayrıca hesaplanır.

## Sınırlar
Bu commit büyük sahne/prefab dosyalarını değiştirmez: yerel sahneye yukarıdaki komut uygular.
Mevcut V16 kaynak shader değişikliği diğer V16 sahnelerinin görünümünü de etkiler.
Yedek sahne eski ışık/materyal atamalarını korur; eski shader kodunu geri getirmek için Git değişiklikleri geri alınmalıdır.
Eski huzme mesh'leri silinmez. Bu işlem hacimsel ışık sistemi eklemez.
V16 shader Realtime içindir; lightmap/APV bake desteği eklenmedi.
Yeni bağımsız ShadowCaster, DepthOnly ve DepthNormals pass'leri kullanılır.
Mevcut ComicShopToon lightmap/APV/Meta desteği korunur.
Unity Editor bu ortamda yok; derleme, Game görünümü ve GPU performansı test edilmedi.

Kabul kontrolü: pencere önündeki rafın zemine gölgesi; ışık intensity azaltılınca aydınlık bandının kararması;
tüm gerçek ışıklar kapalıyken emissive/unlit işaretler dışında yalnız düşük ambient kalması.
API: https://docs.unity3d.com/6000.5/Documentation/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html
