# Kitap isimleri ve raf flamalari

Unity menusu: **Tools > ComicShop > Kitap Isimleri ve Flamalar**. Play kapali olmali.

## Kitap isimleri

1. Kitap listesini yukle.
2. Marka, ID veya isimle ara. ID butonu prefab'i Project'te gosterir.
3. Isimleri yaz veya gorunen satirlara dosya adindan taslak onerisi olustur.
4. Degisen isimleri kaydet. Kaydet butonu filtre disindaki degisiklikleri de kaydeder.

Ornek: `Book_002_ghost1` -> `Ghost - Cilt 1`. Dosya adindan turetilen isimler taslaktir; gercek kapak basligini sen duzeltebilirsin. ID'ler, yayin evleri ve prefab dosya adlari degismez. Mevcut `BookDisplayName` kullanilir. Sonraki Play'de uretilen kitaplar yeni isimleri kullanir. Kaydedilen prefab isimleri icin Ctrl+Z destegi yoktur; Git'ten geri alinabilir. Kaydetmeden once Geri butonu taslagi sifirlar.

## Flamalar

1. Hierarchy'de cerceveli kitapligi veya `LOGO_BOS_KARE_...m` nesnesini sec.
2. Raf flamalari sekmesinde PNG/JPG dokusunu Flama gorseli alanina surukle. Sprite alt varligi yerine texture dosyasini kullan.
3. Secili raflara yerlestir'e bas. Coklu secim desteklenir.
4. Ctrl+S ile sahneyi kaydet. Ctrl+Z sahnedeki flama degisikligini geri alir.

Kare logo noktasinin olcusu ve yonu kullanilir; resmin orani korunur. Kareyi doldurmak icin kare gorsel kullan. Saydam PNG icin texture import ayarinda kaynak alfa kanalinin korunmasi gerekir. Malzeme duz renkli URP unlit ve alpha clip kullanir.

Marka bazinda toplu uygulama, acik sahnelerdeki tek logo noktasi ve tek marka iceren raflari bulur. Karisik veya belirsiz raflar tahmin edilmez; secerek uygula. Bu islem raflarin BrandID degerlerini degistirmez.

Hazir nokta yoksa mevcut **Tools > ComicShop > Raf Dis Cercevesi** araci noktayi cerceveyle beraber olusturur. Mevcut cerceveyi tekrar ekleme. Nokta elle silinmisse once sahnenin yedeginden geri getir.

Mesh ve malzemeler `Assets/ComicShopIdentityGenerated` altinda saklanir. Sahne, bu klasor ve Unity'nin urettigi meta dosyalarini birlikte commit et. Yeniden uygulama sahnede flamayi degistirir; eski uretilmis asset'ler Undo ve diger sahne referanslarini korumak icin silinmez.

## Dogrulama

Kaynak kod ve mevcut BookDisplayName/BrandCatalog/ShelfSlot API uyumu incelendi. Unity bu ortamda bulunmadigi icin editor derlemesi ve goruntu testi yapilmadi. Unity'de bir sol ve bir sag rafta flama yonunu, Ctrl+Z'yi, sahneyi yeniden acinca kaliciligi ve Play'de degistirdigin bir kitap ismini kontrol et.
