using UnityEngine;

/// <summary>
/// Elde tasinan kitaplarin bagli oldugu hand point'i biraz asagi alir.
/// Boylece aktif/en ust kitabin on kapagi kameradan daha rahat gorulur.
/// Sadece elde tasima anchor'ini etkiler; raf ve yere birakma pozlarini degistirmez.
/// </summary>
public class HeldBookHandOffset : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("Elde kitaplarin mevcut konumuna gore asagi offseti.")]
    public float downwardOffset = 0.08f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallOnPlayers()
    {
        PlayerInteraction[] players = Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);

        foreach (PlayerInteraction player in players)
        {
            if (player == null || player.rightHandPoint == null)
                continue;

            HeldBookHandOffset offset = player.GetComponent<HeldBookHandOffset>();
            if (offset == null)
                offset = player.gameObject.AddComponent<HeldBookHandOffset>();

            offset.Apply();
        }
    }

    private bool applied;

    void Awake()
    {
        Apply();
    }

    void Apply()
    {
        if (applied)
            return;

        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.rightHandPoint == null)
            return;

        interaction.rightHandPoint.localPosition += Vector3.down * downwardOffset;
        applied = true;
    }
}
