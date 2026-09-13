# ComicShop — 15 gözlü raf dış çerçevesi

Taslaktaki iki yan dikme, ayak/başlık silmeleri, üst kayıt ve merkezde **boş kare marka alanı**. İç raf, 5 sütun × 3 sıra olarak mevcut projede kalır.

## Kurulum

1. Unity'de sahneni kaydet; Play modundan çık.
2. ZIP içindeki **Assets/ComicShopShelfFrame** klasörünü projenin **Assets** klasörüne kopyala. Son yol `C:\UnityProjects\ComicShop\Assets\ComicShopShelfFrame` olmalı. Paketin tamamını ikinci bir Assets içine atma.
3. Mevcut oyun sahneni aç. Hierarchy'de çerçeve eklemek istediğin **RAF** nesnesini seç. `ShelfSlot_R1C1`, kitap, spawner veya bütün dükkânı seçme.
4. Scene görünümünü rafın açık ön yüzüne getir.
5. Üst menü: **Tools → ComicShop → Raf Dis Cercevesi**.
6. **Secili raftan mesh al** düğmesine bas. `Gorunur RAF mesh` alanında kitaplığın MeshFilter bileşeni olmalı.
7. Koyu malzeme otomatik olarak dükkânın **M_6** malzemesiyle dolar. Boşsa `Assets/ComicShopV16/Generated/Materials/M_6.mat` dosyasını alana sürükle. Paket farklı bir klasördeyse Project aramasında `M_6` ara. Bu malzeme `ComicShop/V16 Source Toon` shader'ını ve `T_6` ahşap dokusunu kullanır.
8. **Secili rafa cerceve ekle** düğmesine bas. Sonra **Ctrl+S** ile sahneyi kaydet.
9. Diğer raflarda aynı işlemi tekrarla. Rafların yeni geniş dış ölçüsü duvar veya komşu rafla çakışıyorsa yerleşimlerini buna göre ayarla.

Ön/arka tersse **Ctrl+Z**, `On / arka yonunu cevir` kutusunu değiştir ve yeniden ekle. Aynı rafa ikinci çerçeve eklenmesi engellenir. Sadece çerçeveyi kaldırmak için RAF altındaki `ComicShop_OuterFrame` nesnesini sil. Oluşturulan mesh dosyası `Assets/ComicShopShelfFrame/Generated` altında kalır; Undo dosyayı silmez.

## Görünüm ve davranış

- Koyu ahşap, mevcut tezgâhın **#43200A / #180901** dokusunu kullanan **aynı paylaşılan toon malzemesidir**. Renk, oyundaki ışık ve ton eşleme ayarlarına göre görünür.
- Siyah konturlar ince geometrik kenarlarla oluşturulur. Yeni shader gerekmez; Ink malzemesi mevcut Source Toon shader'ının ışık almayan sürümüdür.
- İç raf malzemeleri değiştirilmez. İç raf zaten projenin toon malzemeleriyle çalışmalıdır.
- M_6, mevcut ShopV16Appearance tarafından aydınlatılan dükkân malzemesi olmalıdır. Yeni, boş bir sahnede yalnızca bu çerçeveyi açmak dükkânın ışık düzenini taşımaz.
- BookSpawner, ShelfSlot, karakter, kamera, URP ve mevcut sahne ayarları değiştirilmez. Çerçeveye fizik collider'ı eklenmez; mevcut raf collider'ları korunur.
- Sıfır olmayan ölçeklerde ölçü gerçek mesh sınırlarından alınır. `1,1,1` zorunluluğu yoktur. Dik duran, Y yönünde döndürülmüş raflar için tasarlanmıştır; eğik rafların önce doğru konuma getirilmesi gerekir.
- **Logo alanı bilerek BOŞ ve KAREDİR.** Marka yazısı/resmi eklenmedi. `LOGO_BOS_KARE_...m` nesnesi konum işaretidir; adındaki sayı karenin dünya ölçüsüdür. Bir görsel eklerken kare oranı korunmalıdır.

## Referans ve doğrulama

Güncel `backup/tum-proje-2026-09-13`, `3f5762e` commit'indeki `Assets/comics/raf/RAF.fbx` ve `Assets/Settings/ne.unity` esas alınmıştır. Kaynak USD tek sütunlu 5 gözlü modeldir; oyundaki 15 gözlü FBX'in yerine geçirilmez.

`RAF (20)` referans ölçüsü yaklaşık **4,495 × 3,016 × 0,666 m**; kare logo alanı **0,535 × 0,535 m**. Araç bu sayıları sabitlemez, seçilen rafı yeniden ölçer.

Documentation~ içindeki PNG bir geometri önizlemesidir, Unity oyun ekranından alınmamıştır. OBJ dış çerçevenin sabit referans ölçülü geometrisidir; OBJ/MTL tek başına toon shader taşımaz. Oyuna kurulum için yukarıdaki Unity aracını kullan.

Kaynak mesh ve hesaplanan ölçüler incelendi. Bu ortamda Unity Editor bulunmadığından Unity derlemesi ve Play testi yapılamadı. Projeye yalnızca bu yeni klasör eklendi; mevcut kod veya sahne dosyalarının eski sürümü pakete konmadı.
