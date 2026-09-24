using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ShelfLogoChecks
{
    [MenuItem("ComicShop/Shelf Logos/Run Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit is required.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var catalog = ScriptableObject.CreateInstance<BrandCatalog>();
        var axiom = new Texture2D(2, 2);
        var veridian = new Texture2D(2, 2);
        var unknown = new Texture2D(2, 2);
        var firstMaterial = new Material(shader);
        var secondMaterial = new Material(shader);
        try
        {
            catalog.brands = new[] {
                new BrandCatalog.Entry { brandID = 0, brandName = "AXIOM", logoTexture = axiom },
                new BrandCatalog.Entry { brandID = 16, brandName = "VERIDIAN", logoTexture = veridian }
            };
            var first = Bookcase("First", catalog, firstMaterial, axiom, out var slot);
            var book = new GameObject("Book").AddComponent<BookItem>();
            book.bookID = 135; book.brandID = 16;
            ShelfLogoBinding.RefreshAll();
            Check(!slot.Matches(book), "AXIOM shelf accepted VERIDIAN book");
            book.brandID = 0;
            Check(slot.Matches(book), "AXIOM shelf rejected AXIOM book");
            firstMaterial.SetTexture("_BaseMap", veridian);
            // A changed image must never continue accepting the previous publisher.
            Check(first.PublisherID == -1, "Changed image kept its stale identity");
            ShelfLogoBinding.RefreshAll();
            Check(first.PublisherID == 16 && !slot.Matches(book), "Logo swap did not change publisher");
            book.brandID = 16;
            Check(slot.Matches(book), "Swapped shelf rejected new publisher");
            firstMaterial.SetTexture("_BaseMap", unknown);
            ShelfLogoBinding.RefreshAll();
            Check(!slot.Matches(book), "Unknown image accepted a book");
            firstMaterial.SetTexture("_BaseMap", veridian);
            var second = Bookcase("Second", catalog, secondMaterial, veridian, out var secondSlot);
            ShelfLogoBinding.RefreshAll();
            Check(first.PublisherID == -1 && second.PublisherID == -1, "Duplicate publisher not rejected on both shelves");
            secondMaterial.SetTexture("_BaseMap", axiom);
            ShelfLogoBinding.RefreshAll();
            Check(first.PublisherID == 16 && second.PublisherID == 0, "Duplicate resolution failed");
            first.logoRenderer.enabled = false;
            Check(!slot.Matches(book), "Invisible sign accepted a book");
            first.logoRenderer.enabled = true;
            var block = new MaterialPropertyBlock();
            block.SetTexture("_BaseMap", unknown);
            first.logoRenderer.SetPropertyBlock(block, 0);
            ShelfLogoBinding.RefreshAll();
            Check(first.PublisherID == -1, "Visible property-block override was ignored");
            first.logoRenderer.SetPropertyBlock(null, 0);
            catalog.brands[0].logoTexture = veridian;
            Check(catalog.GetBrandForLogo(veridian) == -1, "Ambiguous catalogue logo was accepted");
            Debug.Log("[SHELF LOGO PASS] publisher match, logo swap, stale identity, unknown, duplicate/recovery, hidden image, property block, ambiguous catalogue.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(axiom);
            UnityEngine.Object.DestroyImmediate(veridian);
            UnityEngine.Object.DestroyImmediate(unknown);
            UnityEngine.Object.DestroyImmediate(firstMaterial);
            UnityEngine.Object.DestroyImmediate(secondMaterial);
            ShelfLogoBinding.RefreshAll();
        }
    }

    static ShelfLogoBinding Bookcase(string name, BrandCatalog catalog, Material material, Texture texture, out ShelfSlot slot)
    {
        var root = new GameObject(name);
        var binding = root.AddComponent<ShelfLogoBinding>();
        binding.catalog = catalog;
        var sign = new GameObject("Logo");
        sign.transform.SetParent(root.transform);
        binding.logoRenderer = sign.AddComponent<MeshRenderer>();
        material.SetTexture("_BaseMap", texture);
        binding.logoRenderer.sharedMaterial = material;
        var slotObject = new GameObject("Slot");
        slotObject.transform.SetParent(root.transform);
        slot = slotObject.AddComponent<ShelfSlot>();
        slot.publisherLogo = binding;
        return binding;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[SHELF LOGO FAIL] " + message);
    }
}
