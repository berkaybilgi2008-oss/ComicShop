# Otomatik kitap adlari

HUD ve eldeki kitap listesi artik MARKA - KITAP ISMI #CILT bicimini kullanir.
Ornek: SURGEHOUSE - PRIME MERIDIAN #2, ODDSTAR - KNUCKLESAINT #1.
Mevcut 150 prefab yeniden uretilmeden calisir. Setup ALL Book Models yeni
kitaplarda da ayni bicimi kaydeder. Kitap/marka ID, GUID ve dosya adlari degismez.

Cilt dosya adinin sonundaki sayidir; kitap ID'si veya kopya sirasi degildir.
ODDSTAR knuck1 mevcut oldugu icin #1 gorunur; ileride knuck3 eklendiginde #3 olur.
Tam seri isimleri BookNameFormatter.Titles icinde marka/kisaltilmis isim ile
eslestirilir. Yeni kisaltilmis seriler icin buraya bir eslesme ekleyin veya
BookDisplayName alanina tam seri adini ve #cilt yazin. Bilinmeyen seri adi
buyuk harfe cevrilir; bulunmayan cilt numarasi uydurulmaz.

Unity kontrolu: ComicShop > Book Names > Run Checks. Ardindan Play'de PRIME #2,
KNUCKLESAINT #1 ve farkli markalardaki HOLLOW kitaplarini HUD'da kontrol edin.
Unity derlemesi ve Play Mode bu calisma ortaminda calistirilamadi.
