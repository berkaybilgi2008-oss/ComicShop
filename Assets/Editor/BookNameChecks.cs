using System;
using UnityEditor;
using UnityEngine;

public static class BookNameChecks
{
    [MenuItem("ComicShop/Book Names/Run Checks")]
    public static void Run()
    {
        Check("SURGEHOUSE", "Book_124_prıme2(Clone)", "SURGEHOUSE - PRIME MERIDIAN #2");
        Check("ODDSTAR", "knuck3", "ODDSTAR - KNUCKLESAINT #3");
        Check("CROWNLESS", "hollow1", "CROWNLESS - HOLLOWVINE #1");
        Check("COLDWİRE", "hollow1", "COLDWIRE - HOLLOWCHIME #1");
        Check("VERIDIAN", "voıdwalker3", "VERIDIAN - VOIDWALKER #3");
        Check("DARKFIELD", "umbraıl1", "DARKFIELD - UMBRAL #1");
        Check("ODDSTAR", "NEW_SERIES12", "ODDSTAR - NEW SERIES #12");
        Check("ODDSTAR", "UNKNOWN", "ODDSTAR - UNKNOWN");
        Check("SURGEHOUSE", "SURGEHOUSE - PRIME MERIDIAN #2", "SURGEHOUSE - PRIME MERIDIAN #2");
        Debug.Log("[BOOK NAMES PASS] 9 naming checks passed.");
    }

    static void Check(string brand, string source, string expected)
    {
        string actual = BookNameFormatter.Format(brand, source);
        if (actual != expected) throw new InvalidOperationException(source + ": " + actual + " != " + expected);
    }
}
