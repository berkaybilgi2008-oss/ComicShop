# Guncel kule dagilimi
Mixed Tower Book Fraction = 0.1, Small Tower Size = 10, Large Tower Size = 15.
3600 kitap: 18 adet 10 kitaplik + 12 adet 15 kitaplik = 30 kule / 360 kitap.
3240 kitap daginik kalir. Kule konumlari, boy gruplarinin uretim sirasi ve kitaplar rastgele.
Yeni alan adlari eski sahnelerde kayitli %20 / 25 ayarinin geri gelmesini engeller.
Diger toplam adetlerde her gruba ayrilan kitap butcesinden tam kuleler yapilir; artanlar daginik kalir.
Yer bulunamazsa ilgili kitaplar daginik kalir ve Console uyari verir.
Asagidaki eski %20 / 25 aciklamalari onceki surume aittir.

# Son duzeltme
El acisi tekrar prefab NativeRotation + mevcut hand anchor kalibrasyonundan gelir.
Dunya-yukari hesap ve kuculup buyume kaldirildi; 0.12 saniyelik kisa kayma kullanilir.
Kule destek zinciri her fizik adiminda kokune kadar kontrol edilir; kat basina 0.1s beklemez.
Normal kitaplar sabitlenmeden once tum dinlenme suresince destek gostermelidir.
Spawn Areas bossa Allow Legacy Area kapaliyken spawn durur; sessiz V16 fallback yoktur.
BookSpawnArea Inspector gercek dunya olcusunu ve miras alinan Scale uyarisini gosterir.
Baslangicta daginik kitaplarin collider kutulari cakismayacak sekilde konumlari ayarlanir.
Bu alanlar dogma alanidir, fiziksel duvar degildir; dusme/carpisma sonrasinda kitaplar disina cikabilir.
Cok dar alanlarda yer bulunamazsa Console uyari verir; mevcut kitap adedi korunur.
Unity derlemesi/Play Mode burada calistirilamadi; ilk kitap, kule ortasi alma ve dolu 4x4 alan testi gerekir.

# Rastgele dagilim ve kuleler
BookSpawner > Rastgele Kuleler: Tower Book Fraction = 0.2, Books Per Tower = 25.
Her yeni spawn oturumunda liste, alan secimi, konum ve yon yeniden rastgele secilir.
Daginik kitaplarin 3D donusleri de rastgeledir; kulede kitaplar yatay kalir, ust/alt kapak ve yon sapmasi rastgeledir.
Toplam kitap/yayin turu sayilari mevcut BookData ve Copies Per Book ayarlarindan gelir; kitap eksiltilmez veya eklenmez.
Kule sayisi en yakin tam kuleye yuvarlanir: 150 kitapta 1 kule (25 kitap), 1000 kitapta 8 kule (200 kitap).
Tower Yaw Jitter kule icindeki rastgele yon sapmasidir (varsayilan 8 derece).
25 kitaplik kulenin sigacagi bos, duz zemin bulunamazsa ilgili kitaplar daginik kalir ve Console uyari verir.
Dagilim alanlari ve kitap adedi Inspector'da senin kontrolundedir; bu sinirlar rastgele degistirilmez.
Host son konumlari belirledikten sonra kitaplari agda spawn eder; clientlar yeniden zar atmaz.
Kuleler baslangicta destek kaydi ile sabitlenir. Her kitap altindaki collider izini tutar.
Alttaki kitap alininca/yer degistirince ust kitaplar sirayla fizik moduna doner.
Ustten kitap almak kalan kuleyi bozmaz; destekli kule Q carpmasiyla uyanmaz.
Hostun ilk ag spawn callbacki destek kaydini silmez; clientlar host durumunu izler.

Unity testleri: Farkli oturumlarda konum/sira degisimi, 25 kitap sayisi, yuksek kule stabilitesi,
karisik prefab olculeri, dar/alansiz zemin fallback'i, host-client ve gec katilim kontrol edilmelidir.
Unity bu ortamda yok; derleme ve Play Mode testleri calistirilamadi.

# Yeni: dogrudan kutu secimi
BookSpawner Inspector > Tek kutu alanla basla dugmesi onceki alan bilesenlerini kapatir,
listeyi yeni 2x2 alanla degistirir ve bu alani secer. Undo desteklenir.
BookSpawnArea Inspector > Edit Area acikken Scene kenarlarindan boyutlandir.
W ile konum/yukseklik, E ile Y donusu ayarla. Ek kutu alan ekle dugmesi listeye otomatik baglar.
Yeni Spawn Areas listesi doluyken eski V16 / Area Size degerleri kullanilmaz.
V16 Spawn Area kendi basina kitap ureten bir bilesen degil, Transform referansidir.
Birden fazla aktif BookSpawner varsa Inspector uyari gosterir; her birine secim dugmesiyle ulasilir.
Unity editor derlemesi ve gorsel test bu ortamda yapilamadi.

# Spawn alanlari ve havada uyku duzeltmesi

## Kurulum
Unity Play modundan cik. Hierarchy'de BookSpawner bileseni olan nesneyi sec.
Tools > ComicShop > Spawn > Mevcut Alandan Guvenli Bolgeler Olustur komutunu calistir.
Eski v16SpawnArea / areaSize sinirlari icinde zemin ve bosluk taranir.
Olusan BookSpawnAreas altindaki yesil alanlari Scene gorunumunde kontrol et; Ctrl+S ile sahneyi kaydet.
Bu arac raf duzenini senin acik sahnenden okur; Git'teki sahne otomatik degistirilmedi.

## Manuel ayar
- SpawnArea nesnesini sec. W ile tasi, E ile sadece Y ekseninde dondur.
- Width / Depth genislik ve derinliktir; Scene tutamaclariyla da degisir.
- Height zeminden dogma yuksekligidir. Varsayilan 0.4 birim.
- Alan duvar ve raflardan kitap yarisi kadar uzak olmali. Otomatik tarama 0.2 birim pay birakir; buyuk kitaplarda daha fazla pay birak.
- Yeni alan icin Ctrl+D kullan; kopyayi BookSpawner > Spawn Areas listesine ekle.
- Kullanmayacagin alanin GameObject tikini kapat. Listedeki tum alanlar kapaliysa kitap uretilmez ve hata yazilir.
- Liste tamamen bossa eski dikdortgen spawn sistemi kullanilir.
- Buyuk bolgeler alanlari oraninda daha fazla kitap alir; toplam kitap adedi degismez.
- Otomatik tarama collider gerektirir; masa/raf ustunu zemin sanabilir. Alanlarin dukkân zemini uzerinde kaldigini kontrol et.
- Raflar eski spawn sinirlari disina tasindiysa once eski alanin konumunu ve areaSize degerini ayarla.
- Yeniden uretme mevcut listeyi ezmez. Undo ile geri alabilirsin.

## Uyku duzeltmesi
BookItem, destek yakalayamadigi uyuyan dinamik kitabi artik WakeUp ile uyandirir.
Onceki erken return uyuyan kitabin yercekimiyle dusmesini baslatmiyordu.
Yerde destekli ve kinematic olarak dondurulmus kitaplar bu yoldan uyandirilmaz.
Kontrol yalnizca offline/host fiziginde calisir; client Rigidbody yetkisi degismez.
Bu, kodda bulunan bir acigi kapatir; bildirilen tum donmalarin tek nedeni oldugu henuz dogrulanmadi.

## Unity dogrulamasi (bu ortamda calistirilamadi)
1. Offline ve host/client olarak hizli birakma, dusuk/tam sarjli Q atisini tekrarla.
2. Havada donan kitap olursa Inspector'da BookItem.IsHeld, currentSlot ve Rigidbody Is Kinematic / Use Gravity durumlarini incele.
3. Zeminde duran kitaplarin Q carpmasiyla yeniden hareket etmedigini; altindaki kitap alininca usttekinin dustugunu kontrol et.
4. Spawn bolgelerini tasiyip dondur, yeniden oturum baslat; tum kitaplarin alanlarda dogdugunu kontrol et.
5. Alan listesini bosaltarak eski akisi ve tum alanlari kapatarak hata durumunu kontrol et.
