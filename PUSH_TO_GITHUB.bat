@echo off
cd /d "%~dp0"
echo ============================================================
echo  ComicShop - tum degisiklikler GitHub'a gonderiliyor
echo ============================================================
echo.
git add -A -- . ":(exclude)PUSH_TO_GITHUB.bat" ":(exclude)KAYDET.bat"
git diff --cached --quiet
if errorlevel 1 git commit -m "Yukleme ekrani, nisangah, kayip kitap ve birinci sahis kamera duzeltmeleri" -m "- Yukleme ekrani ana menu stilinde; kitaplar yere yerlesene kadar acik kalir, sayac sonra baslar" -m "- Yeni cizgi-roman nisangahi ve sarj boncuklari" -m "- Oyun alani dukkani kapsar; kayip kitaplar otomatik dukkana doner" -m "- Kendi kameramizda kendi kafamiz/sapkamiz gorunmez" -m "- 14 dil secenegi ve okunakli arayuz yazilari" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_01DZrDVreGSj97dggUCNnFqw"
echo.
echo main dalina gonderiliyor...
git push origin HEAD:main
if not errorlevel 1 goto ok
echo.
echo ------------------------------------------------------------
echo  GitHub'daki main dalinda senin bilgisayarinda olmayan baska
echo  degisiklikler var (arkadasinin commit'leri). Onlarin ustune
echo  yazmamak icin senin surumun ayri bir dala gonderiliyor:
echo      tuna-guncel
echo ------------------------------------------------------------
git push origin HEAD:refs/heads/tuna-guncel
if errorlevel 1 goto fail
echo.
echo *** TAMAM - her sey GitHub'da "tuna-guncel" dalinda ***
echo *** (main ile birlestirmek icin GitHub'da Pull Request acabilirsin) ***
goto end
:ok
echo.
echo *** TAMAM - her sey GitHub'a (main) gonderildi ***
goto end
:fail
echo.
echo *** PUSH BASARISIZ - bu penceredeki hatayi Claude'a gonder ***
:end
echo.
pause
