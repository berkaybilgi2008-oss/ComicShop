# Multiplayer oturum ve etkileşim düzeltmesi

## 2026-09-08: birleşim sonrası sağlamlaştırma

- Raf/elde/ağ hareketi sırasında Rigidbody interpolation kapatılır; yerdeki host fiziğinde açık kalır. Böylece raf animasyonunun son karesinde fizik konumu doğruyken görünen Transform'un bir fizik adımı geride kalması engellenir. Kullanıcının Unity Play Mode regresyon testi bu düzeltmeyle `[MP TEST PASS]` verdi.
- Farklı kitapların raf güncellemeleri ters sırada gelirse istemci raf indeksi, her kitabın son yetkili durumuyla yeniden eşleştirilir. Tek bir kayıp yerel raf kaydı artık kalıcı olmaz.
- Geçersiz/uzak atış isteği reddedildiğinde kitap sahibine güvenilir geri bildirim gönderilir; yerel envanterden önceden çıkarılmış kitap tekrar ele alınır.
- Ağdan bırakılan kitabın geçici kitap-kitap çarpışma istisnaları temizlenir.
- Transport kapandıktan sonra raf sahipliği ve sayaçlar sıfırlanır. Canlı oturum sırasında temizlik yapılmaz.
- Kurtarma RPC'sinde NaN/sonsuz koordinatlar reddedilir; yeniden spawn edilen nesnenin önceki animasyon durumu temizlenir.
- O tuşundaki yerel `YaratikGucu` test hilesi, kitapları sunucu durumunu atlayarak taşıdığı için ağ oturumunda engellenir; offline davranışı korunur. Ağda mevcut ortak kurtarma makinesi kullanılabilir.

`python Tools/validate_multiplayer.py` yalnızca depo bağlantılarını kontrol eder; Unity derlemesi değildir.
Play Mode testine reddedilmiş atıştan envanter kurtarma ve kapanış sonrası boş raf kontrolü eklendi.
İki pencere bağlandıktan, işlemler ve ağ güncellemeleri durulduktan sonra her pencerede
**ComicShop → Tests → Audit Current Multiplayer State (Play Mode)** çalıştırılabilir.
Bu araç yerel raf kayıtlarını ağdaki kitap durumlarıyla ve GameStats ile karşılaştırır.
İki penceredeki özet sayıları da karşılaştırılmalıdır; tek başına ağ aktarımını test etmez.

Bu sürümde Unity Editor/derleyici ortamı bulunmadığından bu yeni Play Mode kontrolleri çalıştırılmadı.
Mevcut IP bağlantısının kapsamı korunur; Steam daveti, Relay/lobi, host migration ve karakter görseli eklenmedi.

Kaynak: `142179e` — Unity 6000.5.9f1, Netcode for GameObjects 2.13.2.
Oyun sahnesi: `Assets/Settings/ne.unity`.

## Değişiklikler

- Host/client başlatma, bağlanma, bağlantı zaman aşımı ve kapanış ayrı durumlarla yönetilir. NGO kapanışı ve transport temizliği tamamlanmadan yeniden başlatma kabul edilmez. Hatalı başlangıç/bağlantı girişimi temizlenir; durum arayüzde gösterilir.
- MPP pencereleri odak kaybettiğinde ağ işlemeye devam eder. Host dinleme adresi `0.0.0.0`, yerel bağlantı adresi `127.0.0.1` olarak ayrı ayarlanır.
- Kamera, CharacterController, input, nişangâh ve HUD yalnızca yerel oyuncuya bağlanır. Despawn sırasında eldeki kitaplar ayrılır, animasyonlar ve yerel referanslar temizlenir. Runtime üretilen oyunculara da eski sahnedeki tuş/elde taşıma ayarları uygulanır.
- Kitaplar yalnızca host oturumu başladığında üretilir. 16 kitap prefabına NetworkObject/NetworkBook eklenir ve mevcut NetworkPrefabs listesine kaydedilir. Kitap oluşturma editör aracı da bu bileşenleri korur.
- Her fiziksel kopyanın ayrı ağ kimliği vardır. Alma, raftan alma, yerleştirme ve bırakma/fırlatma sunucuya RPC ile gider. Sunucu mesafeyi, kapasiteyi, kitap sahibini ve raf kurallarını doğrular. Aynı kitabı iki oyuncu aynı anda alamaz.
- Kitapların kimliği, sahibi, rafı, raf içi indeksi, konumu, dönüşü ve ölçeği NetworkVariable ile paylaşılır; geç katılan oyuncu güncel durumu alır. Serbest kitap fiziğini host çalıştırır. Elde taşıma/atış pozları sahibinden hosta iletilir. Rafın mantıksal yeri host onayında ayrılır; kitap mevcut bookMoveDuration ve bookMoveCurve ile her makinede elden rafa animasyonla taşınır. Animasyon sırasında ara pozlar hedef durumu ezmez ve kitap yeniden alınamaz. Geç katılan oyuncu eski yerleştirme animasyonunu tekrar oynatmaz; güncel raf durumunu görür. Yerel alma ve atış animasyonları korunur.
- Oyuncu ayrıldığında tuttuğu kitaplar yok olmaz; host bunları serbest bırakır. Her yeni oturum yeni kitaplarla başlar. Raf sayaçları despawn sırasında temizlenir.
- Kurtarma makinesi ortak kitapları host üzerinde değiştirir; istemci kendi oyuncusu üzerinden istek gönderir.
- Depodaki BookEdgeLines başlangıç hatası giderilir: okunamayan mesh atlanır, okunabilir mesh pozisyonları vertex stride varsaymadan alınır.

## Kullanım

İki pencerede de aynı güncel proje ve `ne.unity` açık olmalı. Play → Player 1: **Oda Kur (Host)** → Player 2: **Katıl (Client)** (`127.0.0.1`, aynı bilgisayarda).

Sol tık kitap alır, sağ tık bırakır/rafa koyar, Q basılı tutup bırakmak fırlatır. **Esc** bağlantı menüsünü açar veya oyuna döner. **Ayrıl / İptal** oturumu kapatır; yeniden host/katıl butonları kapanış bittiğinde görünür.

## Doğrulama durumu

Bu değişiklikler Unity Editor bulunmayan bir ortamda hazırlanmıştır. Unity derlemesi, IL post-processing ve iki gerçek istemciyle Play Mode testi burada çalıştırılmadı. “İki oyuncuda test edildi” iddiası yoktur.

Çalıştırılan kontroller: C# sözdizimi ayrıştırması; prefab root/component bağlantıları; 17 kayıtlı prefabın (oyuncu + 16 kitap) benzersiz hash/kayıtları; sahnedeki 15 BookData girdisinin doğru ağ prefablarına bağlanması; `git diff --check`. Kullanılan NGO API imzaları resmi `v2.13.2` kaynaklarıyla kontrol edildi.

### Otomatik Unity kontrolü (hazırlandı, burada çalıştırılmadı)

`ne.unity` içinde Play'e bas; henüz Host/Client başlatmadan **ComicShop → Tests → Run Multiplayer Regression (Play Mode)** seç.

Araç gerçek NGO/UTP üzerinden üç host aç/kapat döngüsü, kitap alma, aynı kitaba ikinci alma isteği, raf yerleştirme/geri alma, fiziksel bırakma, elde kitap varken çıkış, kapanırken erken host isteğinin reddi, client zaman aşımı ve ardından host açma senaryolarını çalıştırır. Başarılıysa Console'da `[MP TEST PASS]` yazar. Portu başka host kullanmamalıdır. Bu araç iki ayrı istemci testinin yerine geçmez.

### İki pencere kabul testi

1. Host aç, Player 2 katılsın. Her pencere yalnızca kendi oyuncusunu kontrol etsin; tek MainCamera/AudioListener etkin olsun.
2. Player 2 kitap alsın, rafa koysun, geri alsın ve Q ile fırlatsın. Player 1 aynı kopyanın hareketini ve raf sayacını görsün. Roller tersken de tekrarla.
3. Aynı kitaba iki oyuncu birlikte tıklasın: yalnızca bir oyuncu alabilsin. Son raf kapasitesinde iki oyuncu yerleştirmeyi denesin: kapasite ve marka/kitap kuralları korunsun.
4. Player 2 elinde kitap varken ayrılsın; hostta kitap kaybolmasın. Tekrar katıldığında etkileşim çalışsın; eski oyuncu/HUD kalmasın.
5. Host ayrılsın; client menüye dönebilsin. Aynı Play oturumunda tekrar Host → Client yap. En az üç kez tekrarla.
6. Her iki pencerede Play'i durdurup yeniden başlat; Host → Client tekrar çalışsın. Host kapalıyken client bağlantısı zaman aşımından sonra tekrar denenebilsin.
7. Host bazı kitapları yerleştirdikten sonra yeni client katılsın; mevcut raf içerikleri, eldeki kitaplar ve sayaçlar aynı olsun.

Kapsam: mevcut IP/UnityTransport co-op akışı. Steam/lobi veya farklı sahneler arasında oyun ilerletme eklenmedi. Mevcut oyuncu prefabında görünür karakter modeli yok; hareket senkronizasyonu model eklemez.

## Oynanış geri bildirimi sonrası düzeltme

Kullanıcı, iki oyunculu ana akışın çalıştığını doğruladı; karakterin kısaldığını ve raf yerleştirme animasyonunun kaybolduğunu bildirdi. Bunlar ilk düzeltmenin getirdiği regresyonlardı. Player.prefab içindeki CharacterController.center.y 1'den özgün değer 0'a döndürüldü; prefab artık kaynak sürümle birebir aynı. Ağ yerleştirmesine sunucu onayından sonra görsel geçiş eklendi. Unity kontrol aracına başlangıçta ışınlanmama, animasyon bitmeden alınamama ve nihai raf pozuna ulaşma kontrolleri eklendi. Bu ek değişikliklerin Play Mode testi bu ortamda çalıştırılmadı.
