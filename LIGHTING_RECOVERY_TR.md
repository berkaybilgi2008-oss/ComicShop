# Avize konumları ve karanlık iç mekân düzeltmesi
Tools > ComicShop > Recover Ambience and Follow Pendants

Bu komut sahneyi önce Assets/LightingBackups içine yedekler.
Eski kaynak Spot ışıklarını kapatır. Mevcut ComicShop Pendant Spot nesnelerinin
bağlı olduğu görünür avize gruplarının güncel bounds değerlerini kullanır.
Işıklar avize grubunun çocuğu olarak kalır; grup taşınınca ışık da taşınır.
Avizenin alt mesh'leri grup içinde ayrıca taşınırsa komut tekrar çalıştırılmalıdır.
3600 K, intensity 0.65, range 7, 120/95 derece uygulanır; yalnız ilk iki Spot gölge üretir.

İthal modelde M_193 materyalini kullanan sabit zemine çizilmiş ışık lekeleri kapatılır.
Bu kaynak materyal (GUID 133abf9e61ddc7840aebd209f408bd7f) prefab ve materyal YAML'ından doğrulandı.
Neon, cam ve diğer transparent materyaller topluca kapatılmaz.

Zemin sınırındaki dikey, geniş transparent yüzeyler pencere camı olarak bulunur.
Bunların shadow casting'i kapatılır. En geniş cam yüzeyin bulunduğu cepheden
içeri yaklaşık 10 derece aşağı bakan güneş ayarlanır: 4600 K, intensity 1.15.
Cam bulunmazsa güneş yönü değiştirilmez ve Console uyarı verir.
Bu bir cam geçirgenliği yaklaşımıdır; renkli ışık aktarımı eklemez.

Toon materyallerin ayrı kopyaları: ShadowFloor 0.55, BakedInfluence 0.08, ShadowSteps 3.
V16 kaynak shader varsayılan ambient tabanı 0.06'dan 0.32'ye yükseltildi,
gölge renginin ambient üzerindeki baskısı azaltıldı. Bu değişiklik kaynak shader'ı
kullanan şehir yüzeylerinin de aşırı karanlık kalmasını azaltır.
V16 Realtime shader'da fiziksel GI bake eklenmedi; bu kontrollü sanatsal ambient katkısıdır.

Önceki gölge kabuğu korunur. Diğer eski kurulum komutları tekrar çalıştırılırsa bazı ayarlar değişebilir.
Kaynak kontrolleri yapıldı; Unity Editor'de derleme ve görsel sonuç bu ortamda doğrulanmadı.
Game görünümünde kontrol ettikten sonra sahneyi kaydet.
