@echo off
cd /d "%~dp0"
echo ============================================================
echo  ComicShop - degisiklikler bu bilgisayarda kaydediliyor (commit)
echo ============================================================
echo.
git add -A -- . ":(exclude)PUSH_TO_GITHUB.bat" ":(exclude)KAYDET.bat"
git diff --cached --quiet
if not errorlevel 1 goto none
git commit -m "Yukleme ekrani, nisangah, kayip kitap ve birinci sahis kamera duzeltmeleri" -m "- Yukleme ekrani ana menu stilinde; kitaplar yere yerlesene kadar acik kalir, sayac sonra baslar" -m "- Yeni cizgi-roman nisangahi ve sarj boncuklari" -m "- Oyun alani dukkani kapsar; kayip kitaplar otomatik dukkana doner" -m "- Kendi kameramizda kendi kafamiz/sapkamiz gorunmez" -m "- 14 dil secenegi ve okunakli arayuz yazilari" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" -m "Claude-Session: https://claude.ai/code/session_01DZrDVreGSj97dggUCNnFqw"
echo.
echo *** TAMAM - kaydedildi. GitHub'a gondermek icin PUSH_TO_GITHUB.bat ***
goto end
:none
echo *** Kaydedilecek yeni degisiklik yok ***
:end
echo.
pause
