# Işık sızıntısı ve sıcak güneş
Unity'de mevcut sahne açıkken Tools > ComicShop > Seal Light Leaks and Warm Sunset çalıştır.

Önce sahne Assets/LightingBackups içine kopyalanır. V16 Mesh_1/G_0_1 zemin ve Mesh_2 tavan referanslarıyla yerel mimari ölçüler bulunur; ölçek ve dönüşler korunur.
Tavan üzerine yalnız gölge atan kapalı bir kutu eklenir.
Sınırdaki opak, basit duvar parçaları kalınlaştırılmış gölge kopyalarıyla desteklenir.
Cam, transparent, unlit ve 24'ten fazla vertex içeren karmaşık mesh'ler dışlanır.
Duvar kopyaları pencere açıklıklarına doğru genişletilmez. Görünür mesh ve materyaller değiştirilmez.
Engelleyicilerin collider'ı yoktur. Tekrar çalıştırınca önceki shell yenilenir.
Güneş 4200 K / intensity 0.95 olur, mevcut pencereye dönük yönü korunur.
RenderSettings ve diğer ışıklar değiştirilmez. Gece-gündüz döngüsü eklenmez.

Bu işlem geometriyi onarmaz; görünmeyen bir gölge kabuğu ekler. Gölge almayan ışıklar bu kabuktan etkilenmez.
Görünür, elle yerleştirilmiş huzme mesh'leri otomatik silinmez; gerçek ışık sızıntısından ayrı olabilirler.
Model değişmişse veya duvarlar karmaşık mesh ise Console'daki oluşturulan duvar sayısına dikkat edilmeli.
Sıfır duvar bulunduğunda araç bunu uyarı olarak bildirir ve yalnız tavanı kapatır.
Unity Editor ile derleme/görsel kontrol burada yapılamadı. Kaynak zemin GUID'i ve mimari referanslar repo prefab'ından doğrulandı.
Sonucu Game görünümünde kontrol edip kaydet.
