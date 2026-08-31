// Bu sinif "static" -- yani sahnede bir GameObject'e eklemene GEREK YOK.
// BookSpawner.cs baslangicta GameStats.Initialize(...) cagirir,
// ShelfSlot.cs her dogru yerlestirmede GameStats.RegisterPlacement(...) cagirir,
// GameHUD.cs da bu degerleri okuyup ekranda gosterir.
public static class GameStats
{
    public static int totalHeroes;
    public static int volumesPerHero;
    public static int copiesPerVolume;

    private static int[] placedPerHero;

    public static int TotalPlaced { get; private set; }
    public static int TotalBooks => totalHeroes * volumesPerHero * copiesPerVolume;
    public static int CompletedSeriesCount { get; private set; }

    // BookSpawner, kac kahraman/cilt/kopya oldugunu buraya bildirir.
    public static void Initialize(int heroes, int volumes, int copies)
    {
        totalHeroes = heroes;
        volumesPerHero = volumes;
        copiesPerVolume = copies;
        placedPerHero = new int[heroes];
        TotalPlaced = 0;
        CompletedSeriesCount = 0;
    }

    // ShelfSlot, DOGRU bir kitap yerlestigi her seferinde bunu cagirir.
    public static void RegisterPlacement(int heroID)
    {
        if (placedPerHero == null || heroID < 0 || heroID >= placedPerHero.Length) return;

        placedPerHero[heroID]++;
        TotalPlaced++;

        int neededForComplete = volumesPerHero * copiesPerVolume;
        if (placedPerHero[heroID] == neededForComplete)
        {
            CompletedSeriesCount++;
        }
    }

    // Oyuncu raftaki bir kitabi geri elin aldiginda bunu cagirir --
    // RegisterPlacement'in tam tersi, sayaclari geri duzeltir.
    public static void UnregisterPlacement(int heroID)
    {
        if (placedPerHero == null || heroID < 0 || heroID >= placedPerHero.Length) return;
        if (placedPerHero[heroID] <= 0) return; // zaten 0'sa daha geri alacak bir sey yok

        int neededForComplete = volumesPerHero * copiesPerVolume;
        bool wasComplete = placedPerHero[heroID] == neededForComplete;

        placedPerHero[heroID]--;
        TotalPlaced--;

        if (wasComplete)
        {
            CompletedSeriesCount--;
        }
    }
}