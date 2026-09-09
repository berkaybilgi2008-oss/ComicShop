#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>Non-mutating integration audit. Run after pickup/drop has settled.</summary>
public static class CharacterBookCarryChecks
{
    [MenuItem("ComicShop/Tests/Audit Character Book Carry (Play Mode)")]
    public static void Audit()
    {
        Require(EditorApplication.isPlaying, "Enter Play Mode first.");
        int checkedPlayers = 0;
        foreach (var bridge in UnityEngine.Object.FindObjectsByType<CharacterBookCarryBridge>(FindObjectsSortMode.None))
        {
            if (!bridge.IsBound) continue;
            var interaction = bridge.GetComponent<PlayerInteraction>();
            var network = bridge.GetComponent<NetworkPlayerSetup>();
            Require(interaction != null && interaction.rightHandPoint != null, "Missing hand point.");
            var anchor = interaction.rightHandPoint;
            Require(interaction.playerCamera == null || !anchor.IsChildOf(interaction.playerCamera.transform),
                "Held books are still parented to the camera.");
            Require(anchor.parent != null && anchor.parent.name == "BookSocket", "Expected BookSocket parent.");
            int expected = 0;
            if (network != null && network.IsSpawned && !network.IsOwner)
                expected = NetworkBook.CountHeldBy(network.OwnerClientId);
            else
                foreach (var book in interaction.HeldBooksList)
                    if (book != null && book.IsHeld) expected++;
            Require(bridge.CarriedCount == expected, "Carry count differs from inventory; wait for animation/network to settle.");
            var carry = anchor.parent.GetComponentsInParent<MonoBehaviour>(true);
            bool found = false;
            foreach (var component in carry)
            {
                if (component == null || component.GetType().Name != "ToastBookCarry") continue;
                var type = component.GetType();
                Require((bool)type.GetField("carryingBook").GetValue(component) == (expected > 0), "Arm state differs from held count.");
                var preview = type.GetField("previewBook").GetValue(component) as GameObject;
                Require(preview == null || !preview.activeInHierarchy, "Demo book is visible alongside real inventory.");
                found = true;
            }
            Require(found, "No ToastBookCarry above BookSocket.");
            if (interaction.enabled && !interaction.IsThrowPoseActive)
                foreach (var book in interaction.HeldBooksList)
                    if (book != null) Require(book.transform.parent == anchor, "Held book detached from the character hand.");
            checkedPlayers++;
        }
        Require(checkedPlayers > 0, "No bound character: put the generated ToastRanger visual under Player first.");
        Debug.Log($"[CARRY AUDIT PASS] {checkedPlayers} player(s). Repeat empty, with 1/2 books, after last drop and on both peers.");
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
#endif
