# ComicShop — oyun içi düzeltmeler ve kurulum

19 Eylül 2026. Dal: `fix/gameplay-polish-20260919`.

## Tasarım kararı düzeltmesi

Önceki rapordaki “kayıt yok = BLOCKER” sınıflaması bu oyun için geri çekildi. Kullanıcı her oturumun yeni bir tur olmasını istiyor. Kitap düzeni oturum kapanınca sıfırlanmaya devam eder; menü bunu açıkça söyler, host ayrılmadan önce herkesi etkileyeceği uyarısı çıkar. Yalnız kişisel ayarlar kaydedilir. 360 kitap türü/3600 kopya içerik üretimi kullanıcının devam ettireceği iştir; bu değişiklik kataloğu değiştirmez.

Diğer oyunlar tek bir kural izlemiyor. Somut örnek: PowerWash Simulator 2'nin üretici yama notları multiplayer/freeplay ilerlemesinin kaydı ve autosave'den söz ediyor: https://www.futurlab.co.uk/news/clean-up-the-tiny-town-free-caldera-chronicles-level-plus-12-patch-notes . Bu, ComicShop'un tur tasarımını aynı yapmak zorunda olduğu anlamına gelmez.

## Neler değişti?

- **Kurtarma:** ömür boyu üç denemeden sonra kitabı bırakma kaldırıldı. Arka arkaya sorun varsa 30 saniye bekleyip yeniden dener; iki güvenli taramada deneme bütçesi yenilenir. Çıkışta zemin yoksa geçerli spawn alanında güvenli zemin arar; kitap ve oyuncuyu zemin kabul etmez. Manuel çağırma çalışmaya devam eder.
- **Koridorlar:** sahne kökündeki `Book Spawn Corridors` grubunun BoxCollider çocukları, Third Corridor dahil, başlangıçta otomatik kayda alınır. Grubun/alanın pozisyonu değiştirilmez. Her geçerli alana en az bir kitap, kalanlara kullanılabilir alan oranında dağıtım yapılır. Pasif/null/tekrar/çok dar/eğik alan adıyla hata verir. Disabled collider geçerlidir; pasif GameObject geçerli değildir. Prefab/ID kontrolü üretimden önce yapılır; üretim exception'ında oluşturulan kitaplar geri alınır.
- **Eski test:** Play Mode regresyonu artık `ComicShop/ToonLit`, `DepthNormals` ve `ShadowCaster` bekler. Eski `ComicShop/Book Cel` / `_InkWidths` şartı kaldırıldı.
- **Outline opt-out:** ToonLit materyalinin Outline Mask ayarı gerçek `ToonMask` pass'iyle okunur. Ön plandaki yüzeyin maskesi silueti belirler. Everything ve hiçbir opt-out yokken 1x1 beyaz maske yolu korunur. Opt-out veya layer filtresi varsa tam çözünürlüklü maske ve ek geometri çizimleri gerekir; bunun GPU maliyeti ölçülmedi. Materyal değişiklikleri en geç 0,25 saniyede algılanır. Bu ayar ekran çizgisini kontrol eder; ayrıca eklenmiş inverted-hull renderer'ını silmez.
- **Ayarlar:** ana ses, efekt/UI sesi, ortam sesi, hassasiyet, FOV, invert Y, ipuçları, VSync, FPS sınırı, pencere/kenarlıksız ekran ve çözünürlük. Hareket, zıplama/kalkma, koşma, alma, yerleştirme/bırakma, atış, kurtarma tuşları değiştirilebilir; çakışma reddedilir. Esc menü/iptal olarak sabittir. İlk kurulumda mevcut kameranın FOV'u korunur; değiştirirsen tercih saklanır.
- **Görüntü geri alma:** çözünürlük veya ekran modu 15 saniyede onaylanmazsa geri döner. Bunlar Editor Game View'da uygulanmaz, Windows Player'da denenir. Render pipeline, güneş ve lambalar değiştirilmez.
- **Arayüz:** Play'de otomatik oluşturulan ölçeklenebilir Canvas; krem/koyu mürekkep/turuncu tasarım; solo/Relay/IP oda akışı, kopyalanabilir oda kodu, üç ayar sekmesi, yalın ilerleme HUD'ı, eldeki kitap ve bağlamsal öğretici. Alt+Tab imleci serbest bırakır; çevrimiçi oyun durmaz. Yeni arayüz Türkçedir; çok dilli yerelleştirme ve gamepad navigasyon sertifikasyonu bu paket kapsamında yapılmadı.
- **Ses:** özgün, kodla üretilen kağıt/ahşap benzeri stilize efektler; alma, yerleştirme, bırakma/atış, bonk, ret, düğme, tamamlanma, adım ve atılan kitap çarpması. Her cue için dört varyasyon, 16 seslik havuz ve üst üste ses sınırlaması. Hafif havalandırma ortamı bulunur. Bunlar kayıt stüdyosunda çekilmiş foley veya lisanslı müzik değildir; maskot şarkısı eklenmedi. Ağ olaylarının sesi sunucudan duyurulur; late join bütün eski alma/yerleştirme seslerini tekrar oynatmaz. Adımlar yerel oyuncudadır.
- **Ret geri bildirimi:** yarışta alınmış kitap/dolu veya ulaşılamayan raf durumunda komutu yapan oyuncuya mesaj ve kısa ret sesi.
- **Tur bitişi:** son kitap yerleşince host kararı bir kez sabitlenir; süre ve kitap sayısı sonuç ekranında gösterilir. Oyuncu dükkânda kalabilir. Sonradan kitaba dokunmak sonuç ekranını/reward'ı tekrar tetiklemez. Bitmiş odaya katılan oyuncu sonucu alır. Yeni tur yeni oturumla başlar; host migration veya kayıt/devam eklenmedi.

## Kurulum — ışık menülerini yeniden çalıştırma

Unity'de Play'i durdur, sahneni Ctrl+S ile kaydet ve Unity'yi kapat. PowerShell:

```powershell
cd C:\UnityProjects\ComicShop
git status
```

Yerel değişikliklerin varsa önce kendi dalında yedekle:

```powershell
git add Assets Packages ProjectSettings
git commit -m "Backup current scene before gameplay polish"
```

`nothing to commit` normaldir. Sonra:

```powershell
git fetch origin fix/gameplay-polish-20260919
if ($LASTEXITCODE -ne 0) { throw "Fetch failed" }
git merge --no-edit FETCH_HEAD
if ($LASTEXITCODE -ne 0) { throw "Merge conflict: do not restore or overwrite the scene" }
```

Çakışma varsa rastgele `ours/theirs` seçme; `git status` ve çakışan dosyayı incele. Bu dal `feat/two-corridor-spawn` üzerine kuruludur; hiçbir `.unity`, ışık asset'i veya atış hız/poz kalibrasyonu değiştirmez. Son güneş düzeltmen kendi dalında kalır.

Unity'yi aç, derleme tamamlanınca `ne.unity` içinde Play'e bas. **Yeni UI/ses/tur sistemi otomatik kurulur; sahneye script sürüklemek gerekmez.** “Tek başına başla” internet servisi açmadan yerel host başlatır. Arkadaşların aynı güncel buildi kullanmalı: ağ protokolü 4 oldu.

Third Corridor mevcut `Book Spawn Corridors` grubunun altındaysa otomatik alınır. Sahnedeki atamaları kalıcı görmek için isteğe bağlı:
`Tools > ComicShop > Books > Register and Validate All Corridors (Undo)` → Ctrl+S.
Alan aktif, yatay ve X/Z dünya boyutları padding sonrası pozitif olmalı; `.35` padding için iki boyut da `.70 m`'den büyük olmalıdır. Alan GameObject'inin aktif olması gerekir, BoxCollider.enabled=false kalabilir. Grubun dışında bir alan kullanıyorsan BookSpawner.corridorAreas'a açıkça ata; başka sahne collider'ları tahminle seçilmez.

## Doğrulama durumu

Burada çalıştırılanlar:

- Gerçek `RecoveryBudget.cs` .NET 8 ile derlenip çalıştırıldı: 100 bağımsız kayıp/güvenli dönüş, art arda deneme, bekleme ve bekleme sonrası tekrar koşulları geçti.
- 82 C# dosyası Roslyn C#9 ile parse edildi; CS0136/CS0128/CS0102/CS0111/CS0101 çakışma kontrolleri hatasız. Toplam 207 kontrol.
- Yeni RPC izinleri projenin **NGO 2.13.2** paketinin `RpcAttributes.cs` kaynağıyla doğrulandı: ses/ret RPC'leri `RpcInvokePermission.Server`, sahip komutları `Owner` kullanır.
- `git diff --check` ve yeni .meta GUID kontrolleri yapıldı.

**Unity Editor/Player burada kurulu değil. Tam Unity derlemesi, NGO IL post-processing, shader GPU derlemesi, menünün görsel çıktısı, ses dinleme ve WAN co-op çalıştırılmadı. “Kusursuz/testleri tamamen geçti” sertifikası değildir.** Roslyn kontrolü Unity DLL'lerine karşı tam semantic compile yerine geçmez.

Tekrar çalıştırılabilir yerel kaynak kontrolü (.NET 8 SDK):

```powershell
dotnet run --project Tools/Validation/Check.csproj -- Assets
```

Unity'deki kontroller:

1. Play kapalı: `ComicShop > Tests > Run Gameplay Polish Checks (Edit Mode)`. Geçersiz üçüncü alan, alan başına kota (20 seed), kurtarma bütçesi, bozuk ayarlar ve shader pass sözleşmesini denetler. Geçici additive sahne kullanır; sahneni veya PlayerPrefs'i kaydetmez.
2. Play kapalı: `ComicShop > Tests > Run Gameplay QA Checks (Edit Mode)`.
3. Ayrı test oturumunda Play: `ComicShop > Tests > Run Multiplayer Regression (Play Mode)`. Önce mevcut odadan ayrıl; test oturum açıp kapatır.
4. Manuel: üç alanın her birinde kitap; aynı kitabı dört ayrı kez kaybetme; alanı pasifleştirince adıyla hata; yanlış yayıncı ve iki kişinin aynı kitaba uzanması.
5. Windows build: çözünürlük değiştirip 15 saniye onay verme → geri dönmeli. Tuş/hassasiyet/ses değiştirip uygulamayı kapat/aç → korunmalı. Kitap düzeni korunmamalı.
6. İki gerçek cihaz: bir kitap eylemi başına tek cue; host son kitabı yerleştirince iki tarafta sonuç; bitmiş odaya late join; dükkânda kalınca sonucu tekrar açmama; host ayrılınca temiz menüye dönüş.
7. Outline: yan yana iki ToonLit materyal; birinde Outline Mask=0. İç normal kenarları ve dış siluet kapanmalı. Layer mask filtresini ayrıca dene. Maskeli yolun Draw/SetPass maliyetini Frame Debugger'da ölç.

3600 kitap/farklı ağlarda dört kişi FPS ve uzun oturum testi hâlâ gereklidir. Bu paket içerik üretimini veya performans sertifikasını tamamlamaz.
