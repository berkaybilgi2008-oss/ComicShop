using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameStats
{
    public static int totalBookTypes;
    public static int copiesPerBook;
    private static Dictionary<int, int> placedPerBook = new Dictionary<int, int>();
    public static int TotalPlaced { get; private set; }
    public static int TotalBooks => totalBookTypes * copiesPerBook;
    public static int CompletedBookGroupCount { get; private set; }
    public static int CompletedSeriesCount => CompletedBookGroupCount;

    public static void Initialize(int bookTypes, int copies)
    {
        var ids = new List<int>(Mathf.Max(0, bookTypes));
        for (int i = 0; i < bookTypes; i++) ids.Add(i);
        Initialize(ids, copies);
    }

    public static void Initialize(IEnumerable<int> bookIds, int copies)
    {
        // BookData IDs are identities, not array offsets. A test catalogue can
        // contain e.g. IDs 45 and 359 without silently losing its progress.
        var next = new Dictionary<int, int>();
        foreach (int id in bookIds)
        {
            if (id < 0 || next.ContainsKey(id))
                throw new ArgumentException("Book catalogue contains a negative or duplicate BookID: " + id);
            next.Add(id, 0);
        }
        placedPerBook = next;
        totalBookTypes = next.Count;
        copiesPerBook = Mathf.Max(1, copies);
        TotalPlaced = 0;
        CompletedBookGroupCount = 0;
    }

    public static void RegisterPlacement(int bookID)
    {
        if (!placedPerBook.TryGetValue(bookID, out int count) || count >= copiesPerBook) return;
        placedPerBook[bookID] = count + 1;
        TotalPlaced++;
        if (count + 1 == copiesPerBook) CompletedBookGroupCount++;
    }

    public static void UnregisterPlacement(int bookID)
    {
        if (!placedPerBook.TryGetValue(bookID, out int count) || count <= 0) return;
        placedPerBook[bookID] = count - 1;
        TotalPlaced--;
        if (count == copiesPerBook) CompletedBookGroupCount--;
    }
}
