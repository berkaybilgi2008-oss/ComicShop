using UnityEngine;

public static class GameStats
{
    public static int totalBookTypes;
    public static int copiesPerBook;

    private static int[] placedPerBook;

    public static int TotalPlaced { get; private set; }
    public static int TotalBooks => totalBookTypes * copiesPerBook;
    public static int CompletedBookGroupCount { get; private set; }
    public static int CompletedSeriesCount => CompletedBookGroupCount;

    public static void Initialize(int bookTypes, int copies)
    {
        totalBookTypes = Mathf.Max(0, bookTypes);
        copiesPerBook = Mathf.Max(1, copies);
        placedPerBook = new int[totalBookTypes];
        TotalPlaced = 0;
        CompletedBookGroupCount = 0;
    }

    public static void RegisterPlacement(int bookID)
    {
        if (!IsValidBookID(bookID) || placedPerBook[bookID] >= copiesPerBook)
            return;

        placedPerBook[bookID]++;
        TotalPlaced++;

        if (placedPerBook[bookID] == copiesPerBook)
            CompletedBookGroupCount++;
    }

    public static void UnregisterPlacement(int bookID)
    {
        if (!IsValidBookID(bookID) || placedPerBook[bookID] <= 0)
            return;

        bool wasComplete = placedPerBook[bookID] == copiesPerBook;
        placedPerBook[bookID]--;
        TotalPlaced--;

        if (wasComplete)
            CompletedBookGroupCount--;
    }

    private static bool IsValidBookID(int bookID)
    {
        return placedPerBook != null && bookID >= 0 && bookID < placedPerBook.Length;
    }
}
