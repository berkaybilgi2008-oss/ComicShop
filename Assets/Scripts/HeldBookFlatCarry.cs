using UnityEngine;

// ToastBookCarry updates the socket at 100; NetworkBook publishes at 300.
// Correct only normal carry orientation after the arm, before network publication.
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class HeldBookFlatCarry : MonoBehaviour
{
    private PlayerInteraction interaction;

    private void Awake() => interaction = GetComponent<PlayerInteraction>();

    private void LateUpdate()
    {
        if (interaction == null || !interaction.enabled || interaction.rightHandPoint == null ||
            interaction.IsThrowPoseActive) return;
        foreach (var book in interaction.HeldBooksList)
        {
            if (book == null || !book.IsHeld || book.currentSlot != null ||
                book.transform.parent != interaction.rightHandPoint) continue;
            var network = book.GetComponent<NetworkBook>();
            if (network != null && network.IsSpawned && !network.HeldByLocal) continue;
            book.transform.rotation = interaction.GetFlatCarryRotation(book);
        }
    }
}
