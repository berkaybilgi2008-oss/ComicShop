#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs real NGO/UTP host lifecycle and book actions in the open Play Mode scene.</summary>
public static class MultiplayerRegressionChecks
{
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static ConnectionManager connection;
    private static string originalAddress;
    private static float originalTimeout;

    [MenuItem("ComicShop/Tests/Run Multiplayer Regression (Play Mode)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || ConnectionManager.Instance == null)
        {
            Debug.LogError("Test: Assets/Settings/ne.unity sahnesinde Play'e bas, sonra bu testi calistir.");
            return;
        }
        if (steps.Count != 0 || ConnectionManager.Instance.IsRunning)
        {
            Debug.LogError("Testi baslatmadan once oturumdan ayril.");
            return;
        }
        connection = ConnectionManager.Instance;
        originalAddress = connection.address;
        originalTimeout = connection.connectionTimeout;
        steps.Push(Scenarios());
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) { Finish(); return; }
            if (!steps.Peek().MoveNext()) steps.Pop();
            else if (steps.Peek().Current is IEnumerator nested) steps.Push(nested);
            if (steps.Count == 0)
            {
                Debug.Log("[MP TEST PASS] 3 host restarts, pickup claim, release, shelf placement/take, held-book shutdown, client timeout, host after timeout.");
                Finish();
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (connection != null) connection.Disconnect();
            Finish();
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        steps.Clear();
        if (connection != null)
        {
            connection.address = originalAddress;
            connection.connectionTimeout = originalTimeout;
        }
        connection = null;
    }

    private static IEnumerator Scenarios()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Check(connection.StartHost(), "Host start failed, attempt " + (attempt + 1));
            yield return WaitFor(() => connection.State == ConnectionManager.SessionState.Connected &&
                NetworkPlayerSetup.LocalPlayer != null, 10f, "local player spawn");
            yield return null;
            var setup = NetworkPlayerSetup.LocalPlayer;
            var player = setup.GetComponent<PlayerInteraction>();
            Check(player.enabled && setup.playerCamera.enabled && setup.GetComponent<CharacterController>().enabled,
                "Owner controls/camera disabled");
            int cameras = 0;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.enabled && camera.CompareTag("MainCamera")) cameras++;
            Check(cameras == 1, "Expected one local MainCamera");
            var books = Object.FindObjectsByType<NetworkBook>(FindObjectsSortMode.None);
            Check(books.Length == GameStats.TotalBooks && books.Length > 0, "Wrong book count / duplicate spawn");
            var book = books[0];
            var body = book.GetComponent<Rigidbody>();
            body.isKinematic = true;
            book.transform.position = setup.playerCamera.transform.position + setup.playerCamera.transform.forward;
            Physics.SyncTransforms();
            book.PickUpRpc();
            book.PickUpRpc();
            yield return WaitFor(() => player.HeldBooksList.Count == 1, 3f, "pickup acknowledgement");
            Check(book.Holder == NetworkManager.Singleton.LocalClientId, "Pickup holder mismatch");
            yield return WaitFor(() => book.transform.parent == player.rightHandPoint, 3f, "hand parenting");
            // Wait for the pickup animation before issuing a different action.
            double animationEnd = EditorApplication.timeSinceStartup + 0.5;
            yield return WaitFor(() => EditorApplication.timeSinceStartup >= animationEnd, 2f, "pickup animation");

            ShelfSlot target = null;
            foreach (var slot in Object.FindObjectsByType<ShelfSlot>(FindObjectsSortMode.None))
                if (slot.Matches(book.GetComponent<BookItem>()) &&
                    slot.TryGetNextPlacementPose(book.GetComponent<BookItem>(), out _, out _)) { target = slot; break; }
            Check(target != null, "No valid shelf for the test book");
            var box = target.GetComponentInChildren<Collider>();
            Check(box != null, "Shelf collider missing");
            var controller = setup.GetComponent<CharacterController>();
            controller.enabled = false;
            setup.transform.position += box.bounds.center - setup.playerCamera.transform.position;
            Physics.SyncTransforms();
            book.PlaceRpc(target.NetworkKey);
            yield return WaitFor(() => target.FilledCount == 1 && player.HeldBooksList.Count == 0, 3f, "placement");
            Check(GameStats.TotalPlaced == 1, "Placement counted incorrectly");
            book.PickUpRpc();
            yield return WaitFor(() => player.HeldBooksList.Count == 1 && target.FilledCount == 0, 3f, "take from shelf");
            Check(GameStats.TotalPlaced == 0, "Taking from shelf did not update stats");
            yield return null;
            player.enabled = false; // No test/user input or hand coroutine should drive the released transform.
            player.ResetInteraction();
            Vector3 release = setup.playerCamera.transform.position + setup.playerCamera.transform.forward;
            book.ReleaseRpc(release, Quaternion.identity, Vector3.forward * 2, Vector3.zero, 0, false);
            yield return WaitFor(() => book.Holder == NetworkBook.NoHolder, 3f, "release acknowledgement");
            Check(!body.isKinematic, "Released book physics disabled");
            player.enabled = true;
            body.isKinematic = true;
            book.transform.position = release;
            Physics.SyncTransforms();
            book.PickUpRpc();
            yield return WaitFor(() => book.Holder != NetworkBook.NoHolder, 3f, "pickup before disconnect");
            controller.enabled = true;
            connection.Disconnect();
            Check(!connection.StartHost(), "Host restart was accepted during shutdown");
            yield return WaitFor(() => !connection.IsRunning, 10f, "shutdown completion");
            yield return null;
            Check(NetworkPlayerSetup.LocalPlayer == null, "Stale local player after shutdown");
            Check(Object.FindObjectsByType<NetworkBook>(FindObjectsSortMode.None).Length == 0, "Books survived session teardown");
            Check(GameStats.TotalPlaced == 0, "Stale shelf stats after shutdown");
        }
        connection.address = "127.0.0.1";
        connection.connectionTimeout = 2f;
        Check(connection.StartClient(), "Client attempt did not start");
        yield return WaitFor(() => !connection.IsRunning, 10f, "connection timeout cleanup");
        Check(connection.StartHost(), "Host could not restart after client timeout");
        yield return WaitFor(() => NetworkPlayerSetup.LocalPlayer != null, 10f, "host after timeout");
        connection.Disconnect();
        yield return WaitFor(() => !connection.IsRunning, 10f, "final shutdown");
    }

    private static IEnumerator WaitFor(Func<bool> condition, float seconds, string label)
    {
        double deadline = EditorApplication.timeSinceStartup + seconds;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup >= deadline) throw new Exception("[MP TEST FAIL] Timeout: " + label);
            yield return null;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[MP TEST FAIL] " + message);
    }
}
#endif
