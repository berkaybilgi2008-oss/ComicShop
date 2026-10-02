using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum GameLanguage
{
    English, Turkish, German, French, Spanish, PortugueseBR, Italian, Polish,
    Russian, Ukrainian, Japanese, Korean, ChineseSimplified, ChineseTraditional
}

// Oyun ici dil sistemi. Metinler LocTable.cs'te; kitap ve yayinci adlari cevrilmez.
// Secilen dil PlayerPrefs'te saklanir; ilk aciliste isletim sistemi dili kullanilir.
public static partial class Loc
{
    const string PrefKey = "ComicShop.Language";

    // Dil secicide gorunen adlar (her dil kendi yaziliyla).
    public static readonly string[] NativeNames =
    {
        "English", "Türkçe", "Deutsch", "Français", "Español", "Português (BR)", "Italiano", "Polski",
        "Русский", "Українська", "日本語", "한국어", "简体中文", "繁體中文"
    };

    static readonly string[] CultureNames =
        { "en-US", "tr-TR", "de-DE", "fr-FR", "es-ES", "pt-BR", "it-IT", "pl-PL", "ru-RU", "uk-UA", "ja-JP", "ko-KR", "zh-CN", "zh-TW" };

    static Dictionary<string, string[]> table;
    static bool loaded;
    static GameLanguage current;
    static Font latinDisplay, latinBody, cyrillicDisplay, cyrillicBody;
    static readonly Dictionary<GameLanguage, Font> cjkFonts = new Dictionary<GameLanguage, Font>();
    static CultureInfo culture;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false; Changed = null; culture = null;
        latinDisplay = latinBody = cyrillicDisplay = cyrillicBody = null;
        cjkFonts.Clear(); installedFonts = null;
    }

    public static GameLanguage Current { get { EnsureLoaded(); return current; } }
    public static int Count => NativeNames.Length;

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        if (table == null) table = BuildTable();
        int saved = PlayerPrefs.GetInt(PrefKey, -1);
        current = saved >= 0 && saved < NativeNames.Length ? (GameLanguage)saved : FromSystem(Application.systemLanguage);
    }

    public static void Set(GameLanguage language)
    {
        EnsureLoaded();
        if (language == current) return;
        current = language;
        culture = null;
        PlayerPrefs.SetInt(PrefKey, (int)language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static GameLanguage FromSystem(SystemLanguage system)
    {
        switch (system)
        {
            case SystemLanguage.Turkish: return GameLanguage.Turkish;
            case SystemLanguage.German: return GameLanguage.German;
            case SystemLanguage.French: return GameLanguage.French;
            case SystemLanguage.Spanish: return GameLanguage.Spanish;
            case SystemLanguage.Portuguese: return GameLanguage.PortugueseBR;
            case SystemLanguage.Italian: return GameLanguage.Italian;
            case SystemLanguage.Polish: return GameLanguage.Polish;
            case SystemLanguage.Russian: case SystemLanguage.Belarusian: return GameLanguage.Russian;
            case SystemLanguage.Ukrainian: return GameLanguage.Ukrainian;
            case SystemLanguage.Japanese: return GameLanguage.Japanese;
            case SystemLanguage.Korean: return GameLanguage.Korean;
            case SystemLanguage.ChineseTraditional: return GameLanguage.ChineseTraditional;
            case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: return GameLanguage.ChineseSimplified;
            default: return GameLanguage.English;
        }
    }

    // ------------------------------------------------------------------ metin

    public static string T(string key)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(key) || !table.TryGetValue(key, out var values)) return key;
        int index = (int)current;
        string value = index < values.Length ? values[index] : null;
        return string.IsNullOrEmpty(value) ? values[0] : value;
    }

    public static string T(string key, params object[] args)
    {
        string format = T(key);
        try { return string.Format(Culture, format, args); }
        catch (FormatException) { return format; }
    }

    public static bool Has(string key) { EnsureLoaded(); return key != null && table.ContainsKey(key); }

    // Ag uzerinden gelen mesajlar dil anahtari olarak gonderilir: "anahtar" ya da "anahtar|arg1|arg2".
    // Anahtar degilse (eski surum, serbest metin) oldugu gibi gosterilir.
    public static string Resolve(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        int bar = message.IndexOf('|');
        string key = bar < 0 ? message : message.Substring(0, bar);
        if (!Has(key)) return message;
        if (bar < 0) return T(key);
        return T(key, message.Substring(bar + 1).Split('|'));
    }

    public static string Key(string key, params object[] args)
    {
        if (args == null || args.Length == 0) return key;
        var parts = new string[args.Length + 1];
        parts[0] = key;
        for (int i = 0; i < args.Length; i++) parts[i + 1] = Convert.ToString(args[i], CultureInfo.InvariantCulture)?.Replace('|', '/');
        return string.Join("|", parts);
    }

    // Binlik ayiraci dile gore (fontlarda olmayan dar bosluk karakterlerinden kacinmak icin sabit tablo).
    static readonly string[] GroupSeparators = { ",", ".", ".", " ", ".", ".", ".", " ", " ", " ", ",", ",", ",", "," };

    public static string Number(long value)
    {
        string text = value.ToString("N0", CultureInfo.InvariantCulture);
        string separator = GroupSeparators[(int)Current];
        return separator == "," ? text : text.Replace(",", separator);
    }

    public static CultureInfo Culture
    {
        get
        {
            if (culture != null) return culture;
            try { culture = CultureInfo.GetCultureInfo(CultureNames[(int)Current]); }
            catch (Exception) { culture = CultureInfo.InvariantCulture; }
            return culture;
        }
    }

    // Dile uygun buyuk harf (Turkce i/İ dahil). Bangers gibi buyuk harf fontlari icin.
    public static string Upper(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Current == GameLanguage.Turkish) return text.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
        return text.ToUpper(Culture);
    }

    // ------------------------------------------------------------------ fontlar

    public static bool IsCjk => Current == GameLanguage.Japanese || Current == GameLanguage.Korean ||
        Current == GameLanguage.ChineseSimplified || Current == GameLanguage.ChineseTraditional;
    public static bool IsCyrillic => Current == GameLanguage.Russian || Current == GameLanguage.Ukrainian;

    // Basliklar ve sayilar (menudeki cizgi-roman fontu).
    public static Font Display(Font fallback)
    {
        if (IsCjk) return CjkFont(fallback);
        if (IsCyrillic) return Load(ref cyrillicDisplay, "ComicShopHud/RussoOne-Regular", fallback);
        return Load(ref latinDisplay, "ComicShopHud/Bangers", fallback);
    }

    // Okunakli govde metni.
    public static Font Body(Font fallback)
    {
        if (IsCjk) return CjkFont(fallback);
        if (IsCyrillic) return Load(ref cyrillicBody, "ComicShopHud/PT_Sans-Narrow-Web-Bold", fallback);
        return Load(ref latinBody, "ComicShopHud/BarlowSemiCondensed-SemiBold", fallback);
    }

    static Font Load(ref Font cache, string path, Font fallback)
    {
        if (cache == null) cache = Resources.Load<Font>(path);
        return cache != null ? cache : fallback;
    }

    // Cince/Japonca/Korece icin isletim sisteminin kendi fontu kullanilir (dev font dosyasi gommeden).
    static Font CjkFont(Font fallback) => CjkFontFor(Current, fallback);

    // Dil secicide her dilin adi kendi yazisiyla gorunsun diye (secili dilden bagimsiz).
    public static Font NativeFont(GameLanguage language, Font fallback)
    {
        switch (language)
        {
            case GameLanguage.Japanese: case GameLanguage.Korean:
            case GameLanguage.ChineseSimplified: case GameLanguage.ChineseTraditional:
                return CjkFontFor(language, fallback);
            case GameLanguage.Russian: case GameLanguage.Ukrainian:
                return Load(ref cyrillicBody, "ComicShopHud/PT_Sans-Narrow-Web-Bold", fallback);
            default:
                return Load(ref latinBody, "ComicShopHud/BarlowSemiCondensed-SemiBold", fallback);
        }
    }

    static Font CjkFontFor(GameLanguage language, Font fallback)
    {
        if (cjkFonts.TryGetValue(language, out var font) && font != null) return font;
        // Kalin yuzler once: ince (Light/Semilight) yuzler kucuk puntoda soluk gorunuyor.
        string[] names;
        switch (language)
        {
            case GameLanguage.Japanese: names = new[] { "Yu Gothic UI Bold", "Yu Gothic UI Semibold", "Yu Gothic Bold", "Meiryo UI Bold", "Meiryo Bold", "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "MS Gothic", "Hiragino Sans W6", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Noto Sans CJK JP Bold", "Noto Sans CJK JP", "Noto Sans JP" }; break;
            case GameLanguage.Korean: names = new[] { "Malgun Gothic Bold", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR Bold", "Noto Sans CJK KR", "Noto Sans KR", "Gulim" }; break;
            case GameLanguage.ChineseTraditional: names = new[] { "Microsoft JhengHei UI Bold", "Microsoft JhengHei Bold", "Microsoft JhengHei UI", "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC Bold", "Noto Sans CJK TC", "Noto Sans TC", "MingLiU" }; break;
            default: names = new[] { "Microsoft YaHei UI Bold", "Microsoft YaHei Bold", "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC Bold", "Noto Sans CJK SC", "Noto Sans SC", "SimHei" }; break;
        }
        names = InstalledFirst(names);
        try { font = Font.CreateDynamicFontFromOSFont(names, 32); }
        catch (Exception) { font = null; }
        if (font == null) font = Load(ref latinBody, "ComicShopHud/BarlowSemiCondensed-SemiBold", fallback);
        cjkFonts[language] = font;
        return font;
    }

    static string[] installedFonts;

    // Kurulu olanlari tercih sirasini koruyarak one al; hicbiri bulunamazsa liste oldugu gibi kalir.
    static string[] InstalledFirst(string[] preferred)
    {
        if (installedFonts == null)
        {
            try { installedFonts = Font.GetOSInstalledFontNames() ?? new string[0]; }
            catch (Exception) { installedFonts = new string[0]; }
        }
        var installed = new HashSet<string>(installedFonts, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>(preferred.Length);
        foreach (var name in preferred) if (installed.Contains(name)) ordered.Add(name);
        if (ordered.Count == 0) return preferred;
        foreach (var name in preferred) if (!installed.Contains(name)) ordered.Add(name);
        return ordered.ToArray();
    }
}
