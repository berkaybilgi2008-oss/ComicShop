using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>Display-only names; never changes BookID, BrandID or asset identities.</summary>
public static class BookNameFormatter
{
    // Full series titles read from the existing covers, scoped by publisher.
    private static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
    {
        { "AXIOM/CALCIFY", "CALCIFY" },
        { "AXIOM/FULL", "FULLBORE" },
        { "AXIOM/GHOST", "GHOSTWIRE" },
        { "AXIOM/MIRROR", "MIRRORCAST" },
        { "AXIOM/TIDE", "TIDEWALKER" },
        { "COLDWIRE/AFTER", "AFTERGLARE" },
        { "COLDWIRE/HOLLOW", "HOLLOWCHIME" },
        { "COLDWIRE/LEECH", "LEECHWORK" },
        { "COLDWIRE/SPLIT", "SPLITSTOCK" },
        { "COLDWIRE/IRONTIDE", "IRONTIDE" },
        { "CROWNLESS/BELL", "BELLWETHER" },
        { "CROWNLESS/HOLLOW", "HOLLOWVINE" },
        { "CROWNLESS/MARCH", "MARCHWARD" },
        { "CROWNLESS/RED", "RED MERCY" },
        { "CROWNLESS/UNMADE", "UNMADE" },
        { "DARKFIELD/CINDER", "CINDERNEL" },
        { "DARKFIELD/HEX", "HEXCHARGE" },
        { "DARKFIELD/LEAD", "LEADFOOT" },
        { "DARKFIELD/RICO", "RICOCHET" },
        { "DARKFIELD/UMBRAL", "UMBRAL" },
        { "ECLIPSE/GRIM", "GRIM METEOR" },
        { "EMBERLINE/GLASS", "GLASS JACKAL" },
        { "EMBERLINE/LETTER", "DEAD LETTER" },
        { "EMBERLINE/MEND", "THE MENDMAN" },
        { "EMBERLINE/RATTLE", "RATTLESAINT" },
        { "EMBERLINE/SHIVER", "SHIVER CROWN" },
        { "FAULTHOUSE/MOTH", "MOTHGLASS" },
        { "FAULTHOUSE/PAPER", "PAPER TIGER" },
        { "FAULTHOUSE/SECOND", "SECOND SUN" },
        { "FAULTHOUSE/SWITCH", "SWITCHBACK" },
        { "FAULTHOUSE/VOID", "VOIDFRAME" },
        { "FORGEPULSE/ECHO", "ECHOFORM" },
        { "FORGEPULSE/LODE", "LODESTONE" },
        { "FORGEPULSE/NULL", "NULLSHADE" },
        { "FORGEPULSE/SWARM", "SWARMBREAKER" },
        { "GHOSTLINE/GRAY", "GRAYMATTER" },
        { "GHOSTLINE/LUCK", "LUCKSTRIKE" },
        { "GHOSTLINE/MEND", "MENDLINE" },
        { "GHOSTLINE/STORM", "STORMWAKE" },
        { "GHOSTLINE/TENS", "TENSILE" },
        { "HARDMILE/BREAK", "BREAKSHIFT" },
        { "HARDMILE/KILN", "KILNHAND" },
        { "HARDMILE/SIXBELL", "SIXBELL" },
        { "MEDIOCRE/AEGIS", "AEGIS" },
        { "MEDIOCRE/CARVER", "CARVER" },
        { "MEDIOCRE/ORBITAL", "ORBITAL REBEL" },
        { "MEDIOCRE/PETE", "PARADOX PETE" },
        { "MEDIOCRE/SENTINEL", "SENTINEL" },
        { "NIGHTSHIFT/BREAK", "BREAKPOINT" },
        { "NIGHTSHIFT/DEAD", "DEADHEAT" },
        { "NIGHTSHIFT/KING", "KING NOTHING" },
        { "NIGHTSHIFT/PALE", "PALE SIGNAL" },
        { "NIGHTSHIFT/VELVET", "VELVETKNIFE" },
        { "ODDSTAR/GRAND", "GRANDMA GRAVES" },
        { "ODDSTAR/KNUCK", "KNUCKLESAINT" },
        { "ODDSTAR/POLAR", "POLAROID" },
        { "ODDSTAR/ROOK", "ROOK & RATTLE" },
        { "ODDSTAR/SUNDAY", "SUNDAY" },
        { "PARLOR 9/BONE", "BONE ORCHARD" },
        { "SURGEHOUSE/FERAL", "FERAL BLOOM" },
        { "SURGEHOUSE/GRAV", "GRAVWELL" },
        { "SURGEHOUSE/LOAD", "LOADSTAR" },
        { "SURGEHOUSE/PRIME", "PRIME MERIDIAN" },
        { "SURGEHOUSE/INK", "INKBLOT" },
        { "VEILCROSS/MIRROR", "MIRRORBRIDGE" },
        { "VEILCROSS/RENEW", "RENEW" },
        { "VERIDIAN/AP", "CHRONOS APE" },
        { "VERIDIAN/CHAIR", "CHAIR WARRIOR" },
        { "VERIDIAN/CHROM", "CHROMA KNIGHT" },
        { "VERIDIAN/VOIDWALKER", "VOIDWALKER" },
        { "DARKFIELD/UMBRAIL", "UMBRAL" },
        { "VERIDIAN/VOID", "VOIDWALKER" },
        { "VERIDIAN/WOOD", "WOODEN GUARDIAN" },
    };
    private static readonly Regex Prefix = new Regex(@"^BOOK_\d+_", RegexOptions.IgnoreCase);
    private static readonly Regex Volume = new Regex(@"^(.*?)\s*#?\s*(\d+)$");

    private static string Normalize(string value) => (value ?? "").Trim()
        .Replace('ı', 'i').Replace('İ', 'I').ToUpperInvariant();

    public static string Format(string publisher, string source)
    {
        string brand = Normalize(publisher);
        string name = Normalize(source).Replace("(CLONE)", "").Trim();
        name = Prefix.Replace(name, "");
        // Already formatted/custom full titles remain idempotent, with current publisher.
        int separator = name.IndexOf(" - ", System.StringComparison.Ordinal);
        if (separator >= 0) name = name.Substring(separator + 3).Trim();
        var match = Volume.Match(name);
        string series = match.Success ? match.Groups[1].Value.Trim().TrimEnd('_', '-', ' ') : name;
        string volume = match.Success ? match.Groups[2].Value : "";
        if (int.TryParse(volume, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            volume = number.ToString(CultureInfo.InvariantCulture);
        if (Titles.TryGetValue(brand + "/" + series, out string title)) series = title;
        else series = series.Replace('_', ' ');
        // Never invent a volume for a source without one.
        return (brand.Length > 0 ? brand + " - " : "") + series + (volume.Length > 0 ? " #" + volume : "");
    }
}
