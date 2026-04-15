using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects contact between water particles and lava particles from two separate
/// SPHFluid2D simulations. When a water particle comes within `contactDistance`
/// of a lava particle, both are removed and a solid black rock is spawned.
/// </summary>
public class LavaWaterInteraction : MonoBehaviour
{
    [Header("Fluid Simulations")]
    [SerializeField] private SPHFluid2D waterFluid;
    [SerializeField] private SPHFluid2D lavaFluid;

    [Header("Contact")]
    [Tooltip("Distance below which a water and lava particle are considered in contact.")]
    [SerializeField] private float contactDistance = 0.25f;

    [Header("Rock Spawning")]
    [Tooltip("Prefab spawned at the contact point. Should have a non-trigger Collider2D.")]
    [SerializeField] private GameObject rockPrefab;
    [Tooltip("Size of the rock when no prefab is provided.")]
    [SerializeField] private float rockSize = 0.22f;
    [Tooltip("Color of the rock when no prefab is provided.")]
    [SerializeField] private Color rockColor = new Color(0.05f, 0.03f, 0.05f, 1f);
    [Tooltip("Minimum distance between spawned rocks so they don't pile up.")]
    [SerializeField] private float minSpawnSpacing = 0.18f;
    [Tooltip("Name of the layer rocks are placed on. Must be included in each fluid's Wall Layer Mask so particles collide with them.")]
    [SerializeField] private string rockLayerName = "Wall";

    private Transform rockParent;
    private readonly List<GameObject> spawnedRocks = new List<GameObject>();
    // Reused each frame to mark removals before applying them
    private readonly HashSet<int> waterToRemove = new HashSet<int>();
    private readonly HashSet<int> lavaToRemove = new HashSet<int>();
    private int resolvedRockLayer = -1;
    private bool loggedFirstRock;

    private void Awake()
    {
        // Create the parent at the SCENE ROOT (no parent) so its scale is
        // guaranteed to be (1,1,1). If we parented it under `transform`,
        // any non-identity scale on this GameObject would poison every rock's
        // collider size and collisions would silently break.
        GameObject parent = new GameObject("LavaWaterRocks");
        rockParent = parent.transform;
        rockParent.position = Vector3.zero;
        rockParent.rotation = Quaternion.identity;
        rockParent.localScale = Vector3.one;

        // Resolve the rock layer and warn loudly if it's misconfigured
        resolvedRockLayer = LayerMask.NameToLayer(rockLayerName);
        if (resolvedRockLayer < 0)
        {
            Debug.LogWarning(
                $"[LavaWaterInteraction] Layer \"{rockLayerName}\" does not exist. " +
                "Rocks will spawn on the Default layer and the fluid will not collide with them. " +
                "Create the layer in Project Settings > Tags and Layers, then add it to each SPHFluid2D's Wall Layer Mask.");
        }
    }

    private void OnDestroy()
    {
        if (rockParent != null)
            Destroy(rockParent.gameObject);
    }

    private void ApplyLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            ApplyLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    private void FixedUpdate()
    {
        if (waterFluid == null || lavaFluid == null)
            return;

        var water = waterFluid.Particles;
        var lava = lavaFluid.Particles;
        if (water.Count == 0 || lava.Count == 0)
            return;

        float contactSqr = contactDistance * contactDistance;

        waterToRemove.Clear();
        lavaToRemove.Clear();

        // Simple O(n*m) check — fine for reasonable particle counts.
        // Each lava particle looks for the nearest water particle within range.
        for (int l = 0; l < lava.Count; l++)
        {
            if (lavaToRemove.Contains(l))
                continue;

            Vector2 lavaPos = lava[l].position;

            for (int w = 0; w < water.Count; w++)
            {
                if (waterToRemove.Contains(w))
                    continue;

                Vector2 diff = water[w].position - lavaPos;
                if (diff.sqrMagnitude <= contactSqr)
                {
                    Vector2 contactPoint = (water[w].position + lavaPos) * 0.5f;
                    SpawnRock(contactPoint);

                    waterToRemove.Add(w);
                    lavaToRemove.Add(l);
                    break; // this lava particle is gone; move on
                }
            }
        }

        // Apply removals backwards so swap-remove doesn't invalidate remaining indices
        if (waterToRemove.Count > 0)
        {
            List<int> sorted = new List<int>(waterToRemove);
            sorted.Sort();
            for (int i = sorted.Count - 1; i >= 0; i--)
                waterFluid.RemoveParticle(sorted[i]);
        }

        if (lavaToRemove.Count > 0)
        {
            List<int> sorted = new List<int>(lavaToRemove);
            sorted.Sort();
            for (int i = sorted.Count - 1; i >= 0; i--)
                lavaFluid.RemoveParticle(sorted[i]);
        }

        // Periodically prune any null entries (rocks destroyed externally)
        if (spawnedRocks.Count > 0 && Time.frameCount % 60 == 0)
            spawnedRocks.RemoveAll(r => r == null);
    }

    private void SpawnRock(Vector2 position)
    {
        // Reject a new rock if any live rock is too close. We check live
        // positions (not a ring buffer) so rocks that already exist in the
        // world are always respected.
        float minSqr = minSpawnSpacing * minSpawnSpacing;
        for (int i = 0; i < spawnedRocks.Count; i++)
        {
            if (spawnedRocks[i] == null) continue;
            Vector2 p = spawnedRocks[i].transform.position;
            if ((p - position).sqrMagnitude < minSqr)
                return;
        }

        // Also reject if the contact point is already inside a wall collider
        // (prevents duplicate rocks forming on top of existing level walls).
        if (resolvedRockLayer >= 0)
        {
            Collider2D existing = Physics2D.OverlapPoint(position, 1 << resolvedRockLayer);
            if (existing != null)
                return;
        }

        GameObject rock;
        if (rockPrefab != null)
        {
            rock = Instantiate(rockPrefab, position, Quaternion.identity, rockParent);
            rock.transform.localScale = Vector3.one * rockSize;

            Collider2D[] cols = rock.GetComponentsInChildren<Collider2D>();

            // If the prefab has no collider at all, add one so the fluid can collide
            if (cols.Length == 0)
            {
                BoxCollider2D added = rock.AddComponent<BoxCollider2D>();
                added.size = Vector2.one;
                added.isTrigger = false;
            }
            else
            {
                for (int i = 0; i < cols.Length; i++)
                    cols[i].isTrigger = false;
            }
        }
        else
        {
            rock = new GameObject("Rock");
            rock.transform.SetParent(rockParent, false);
            rock.transform.position = position;
            rock.transform.localScale = Vector3.one * rockSize;

            SpriteRenderer sr = rock.AddComponent<SpriteRenderer>();
            sr.sprite = GetSquareSprite();
            sr.color = rockColor;
            sr.sortingOrder = 5;

            BoxCollider2D col = rock.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = false;
        }

        ApplyLayerRecursive(rock, resolvedRockLayer);
        spawnedRocks.Add(rock);

        // One-time diagnostic so you can verify setup at runtime
        if (!loggedFirstRock)
        {
            loggedFirstRock = true;
            Collider2D c = rock.GetComponentInChildren<Collider2D>();
            string layerName = LayerMask.LayerToName(rock.layer);
            Debug.Log(
                $"[LavaWaterInteraction] First rock spawned. " +
                $"Layer={layerName} ({rock.layer}), " +
                $"HasCollider={c != null}, " +
                $"IsTrigger={(c != null ? c.isTrigger.ToString() : "n/a")}, " +
                $"WorldScale={rock.transform.lossyScale}. " +
                $"Make sure both fluids' Wall Layer Mask includes this layer!");
        }
    }

    public void ClearRocks()
    {
        for (int i = 0; i < spawnedRocks.Count; i++)
        {
            if (spawnedRocks[i] != null)
                Destroy(spawnedRocks[i]);
        }
        spawnedRocks.Clear();
        loggedFirstRock = false;
    }

    private static Sprite cachedSquare;
    private static Sprite GetSquareSprite()
    {
        if (cachedSquare != null)
            return cachedSquare;

        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        cachedSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        return cachedSquare;
    }
}
