using System.Collections.Generic;
using UnityEngine;

public static class GameStats
{
    public static int totalBookTypes;
    public static int copiesPerBook;

    private static Dictionary<int, int> placedPerBook;

    public static int TotalPlaced { get; private set; }
    public static int TotalBooks => totalBookTypes * copiesPerBook;
    public static int CompletedBookGroupCount { get; private set; }
    public static int CompletedSeriesCount => CompletedBookGroupCount;

    public static void Initialize(int bookTypes, int copies)
    {
        totalBookTypes = Mathf.Max(0, bookTypes);
        copiesPerBook = Mathf.Max(1, copies);
        placedPerBook = new Dictionary<int, int>(totalBookTypes);
        TotalPlaced = 0;
        CompletedBookGroupCount = 0;
    }

    public static void RegisterPlacement(int bookID)
    {
        if (!IsKnownBookID(bookID))
            return;

        placedPerBook.TryGetValue(bookID, out int placed);
        if (placed >= copiesPerBook)
            return;

        placed++;
        placedPerBook[bookID] = placed;
        TotalPlaced++;

        if (placed == copiesPerBook)
            CompletedBookGroupCount++;
    }

    public static void UnregisterPlacement(int bookID)
    {
        if (!IsKnownBookID(bookID) || !placedPerBook.TryGetValue(bookID, out int placed) || placed <= 0)
            return;

        bool wasComplete = placed == copiesPerBook;
        placed--;
        placedPerBook[bookID] = placed;
        TotalPlaced--;

        if (wasComplete)
            CompletedBookGroupCount--;
    }

    private static bool IsKnownBookID(int bookID)
    {
        return placedPerBook != null && bookID >= 0 && placedPerBook.ContainsKey(bookID);
    }

    public static void RegisterBookID(int bookID)
    {
        if (placedPerBook != null && bookID >= 0 && !placedPerBook.ContainsKey(bookID))
            placedPerBook.Add(bookID, 0);
    }
}
