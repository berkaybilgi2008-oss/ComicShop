# Logo ile raf yayincisi

Bu sistemde bir kitapligin yayincisi, kare logo alaninda gorunen dokunun
`Assets/Resources/BrandCatalog.asset` icindeki **Logo Texture** eslesmesidir.
Dosya adindan veya resimdeki yazidan tahmin yapilmaz. AXIOM logosu AXIOM'a
kayitliyken, bu logoyu koydugun raf AXIOM kitaplarini kabul eder.

## Bir kez kurulum

1. BrandCatalog.asset'i sec. Her yayincinin Logo Texture alanina o yayincinin
   logo PNG'sini surukle. Ayni dokuyu iki yayinciya atama.
2. Oyun sahnesini ac, Play kapaliyken **ComicShop > Shelf Logos > Connect All Bookcases** calistir.
   Mevcut LOGO_BOS_KARE alanina collider'siz, URP Unlit kare panel eklenir.
   Onceden logo renderer'i varsa korunur. Birden fazla logo alani olan kitaplikta
   uyari verilir; ShelfLogoBinding > Logo Renderer alanindan dogru paneli sec.
3. Kitapligi sec; ShelfLogoBinding altindaki **Logo** alanina gorseli birak.
   Paneldeki gorsel ve kitap kabul ettigi yayinci ayni kaynaktan gelir.
   Her yayinci icin tek kitaplik kullan; fazladan kitapliklarin logosunu bos birak.
4. Sahneyi kaydet. Logo degistirmek icin bundan sonra yalniz Logo alanini degistir.

Logo alaninda material degistirmek veya material'in `_BaseMap` dokusunu degistirmek
de algilanir. Ozel shader'da dogru Texture Property ve Material Index'i sec.
SpriteRenderer kullaniliyorsa Sprite alanini degistir; katalog eslesmesi kaynak
doku uzerindendir (farkli yayincilar ayni sprite atlas dokusunu paylasmamali).
Inspector'daki Logo kisayolu, diger kitapliklar etkilenmesin diye materyali kopyalar.

## Kurallar

- Taninmayan, bos veya gizli logo kitap kabul etmez.
- Ayni yayinci iki aktif kitaplikta bulunursa ikisi de cakisma gosterir ve kabul durur.
- Onceki numarali dagitimdan kalan pasif raf gozleri kurulumda acilir; artik
  hangisinin kitap kabul edecegini logosu belirler. Kitaplik mesh'leri korunur.
- Logo degisikligi bir **sahne duzenleme** islemidir; Play kapaliyken yapilir.
  Oyun sirasinda logo degisirse eski yayinciyla kabul devam etmez; sahneyi yeniden
  baslat. Canli multiplayer logo degistirme RPC'si eklenmemistir. Tum oyuncular
  ayni kayitli sahne/build ile oynamali.
- Kitap ID'leri, tur bazinda tek raf gozu kurali ve goz kapasitesi korunur.
- Logo eslesmeleri **Setup ALL Book Models** yeniden calistirilinca korunur.
- Eski numarali dagitim PR #30 bu kurulum icin gerekli degil.

## Kontrol

**ComicShop > Shelf Logos > Run Checks** gecici bir sahnede logo degisimi,
yanlis yayinci, bos/bilinmeyen logo, cift atama, gizli logo ve material property
block durumlarini kontrol eder. Basarili sonuc Console'da `[SHELF LOGO PASS]`.
Sonra bir host/client oturumunda AXIOM logosu olan rafa AXIOM kitabi koymayi,
farkli yayinci kitabinin reddini ve gec katilimi kontrol et.

GitHub snapshot'inda yayinci logo PNG'leri bulunmadigi icin yukaridaki katalog
eslesmeleri yerel gorsellerle doldurulmalidir; kurulum sahneyi otomatik kaydetmez.
