# Karakterin elinde gerçek kitap taşıma

PlayerInteraction çalışma zamanında CharacterBookCarryBridge ekler. Player altında
aktif ToastBookCarry bileşeni ve onun BookSocket referansı varsa mevcut
rightHandPoint buraya bağlanır. Kitapların mevcut toplama, sıralama, ölçek ve
NativeRotation değerleri kullanılır. Karakter bulunamazsa mevcut kamera taşıması
çalışmaya devam eder.

ToastRanger paketi bu repository'de yoktur. Daha önce içe aktardığınız kitaplı
animasyon paketindeki ToastRanger_PlayerVisual prefabını gerçek Player prefabının
altında tutun; BookSocket ve ToastBookCarry referansları dolu olmalı. Multiplayer'da
sadece sahnedeki bir oyuncuyu değil, spawn edilen Assets/Prefabs/Player.prefab
dosyasını da kontrol edin. İki aktif karakter varsa eski görseli kapatın.

- İlk kitap onaylanıp envantere girince taşıma pozu açılır.
- Başka kitaplar eklenince mevcut stack sıralaması korunur.
- Son kitap çıkınca taşıma pozu kapanır; reddedilen bırakma envantere dönünce tekrar açılır.
- PreviewBook otomatik gizlenir. Hand Height ayarınız değiştirilmez.
- Uzak oyuncu pozunda NetworkBook'un zaten senkronize Holder bilgisi kullanılır.
- Held pose paketleri Animator/kol IK'sından sonra gönderilir. Taşıma yetkisi,
  NetworkObject sahipliği, release/place RPC'leri değiştirilmez.

Kitap yönü için bridge üzerindeki Stack Rotation varsayılanı (90,0,0): Toast
socket'inin +Z avuç normalini kitap stack'inin +Y yönüne çevirir. Socket Offset
gerekirse gerçek kitabın pivotuna göre küçük oturma düzeltmesi içindir. Boy
ayarı ToastBookCarry üzerindeki Hand Height'ta kalır. Tek tip pozitif karakter
ölçeği kullanın; eski dünya kitap ölçeği korunur.

## Unity'de doğrulama

Bu ortamda Unity 6000.5.9f1 çalıştırılamadı. Play Mode'da aşağıdakileri kontrol edin:

1. Boş elde normal Idle/Walk/Run; kitap veya kalıcı taşıma pozu yok.
2. İlk kitap alındığında gerçek kapak/textures elde görünüyor, PreviewBook görünmüyor.
3. Kamerayı yukarı/aşağı çevirin: stack kameraya dönmüyor, ele bağlı kalıyor.
4. 2 kitap alın, tekerlekle sırayı değiştirin; tekini bırakınca kol hâlâ taşıyor.
5. Son kitabı bırakın veya rafa koyun: kol normale dönüyor.
6. Hand Height'ı değiştirin: stack eli izliyor, bacak döngüsü aynı.
7. Host + client'ta tekrarlayın; sahiplik reddi, geç katılma, çıkış ve yeniden
   bağlanmayı mevcut Multiplayer Regression menüsüyle de doğrulayın.

Yeni salt-okuma kontrolü: ComicShop > Tests > Audit Character Book Carry (Play Mode).
Toplama/bırakma tamamlandıktan sonra çalıştırın. Şarjlı atışın özel kamera pozu
mevcut davranışını korur; ayrı bir baş üstü karakter kol animasyonu eklenmedi.
