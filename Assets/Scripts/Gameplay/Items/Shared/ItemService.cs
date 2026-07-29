using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared service for item pooling, state transitions, and common item operations.
/// Used by both ItemManager (player) and OpponentItemManager (AI).
/// </summary>
public class ItemService : MonoBehaviour
{
    private static ItemService instance;
    public static ItemService Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject serviceObj = new GameObject("ItemService");
                instance = serviceObj.AddComponent<ItemService>();
                DontDestroyOnLoad(serviceObj);
            }
            return instance;
        }
    }

    private ItemCatalog catalog;
    private bool catalogNotFoundLogged;

    // Pools
    private readonly List<GreenShell> greenShellPool = new List<GreenShell>();
    private readonly List<RedShell> redShellPool = new List<RedShell>();
    private readonly List<Banana> bananaPool = new List<Banana>();
    private readonly List<Bobomb> bobombPool = new List<Bobomb>();

    // Prefab cache
    private GameObject greenShellPrefab;
    private GameObject redShellPrefab;
    private GameObject bananaPrefab;
    private GameObject bobombPrefab;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePrefabs();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void InitializePrefabs()
    {
        if (!TryEnsureCatalog())
        {
            return;
        }

        greenShellPrefab = catalog.GetWorldPrefab(DebugItemSelection.GreenShell);
        redShellPrefab = catalog.GetWorldPrefab(DebugItemSelection.RedShell);
        bananaPrefab = catalog.GetWorldPrefab(DebugItemSelection.Banana);
        bobombPrefab = catalog.GetWorldPrefab(DebugItemSelection.BobombHold);
    }

    private bool TryEnsureCatalog()
    {
        if (catalog != null)
        {
            catalog.Initialize();
            catalogNotFoundLogged = false;
            return true;
        }

        catalog = ItemCatalog.Instance;
        if (catalog == null)
        {
            catalog = FindObjectOfType<ItemCatalog>(true);
        }

        if (catalog != null)
        {
            catalog.Initialize();
            catalogNotFoundLogged = false;
            return true;
        }

        if (!catalogNotFoundLogged)
        {
            Debug.LogError("[ItemService] ItemCatalog could not be found in the scene.", this);
            catalogNotFoundLogged = true;
        }

        return false;
    }

    // Green Shell Pooling
    public GreenShell GetAvailableGreenShell(Transform storageParent)
    {
        for (int i = 0; i < greenShellPool.Count; i++)
        {
            GreenShell shell = greenShellPool[i];
            if (shell != null && shell.IsAvailable())
            {
                if (shell.transform.parent == storageParent)
                {
                    shell.transform.localPosition = Vector3.zero;
                    shell.transform.localRotation = Quaternion.identity;
                }
                return shell;
            }
        }

        if (greenShellPrefab == null)
        {
            Debug.LogWarning("[ItemService] Green shell prefab not configured.", this);
            return null;
        }

        GameObject shellObject = Instantiate(greenShellPrefab, storageParent.position, storageParent.rotation, storageParent);
        shellObject.transform.localPosition = Vector3.zero;
        shellObject.transform.localRotation = Quaternion.identity;
        GreenShell newShell = shellObject.GetComponent<GreenShell>();
        if (newShell == null)
        {
            Debug.LogError("[ItemService] Instantiated green shell is missing GreenShell component.", shellObject);
            Destroy(shellObject);
            return null;
        }

        greenShellPool.Add(newShell);
        return newShell;
    }

    // Red Shell Pooling
    public RedShell GetAvailableRedShell(Transform storageParent)
    {
        for (int i = 0; i < redShellPool.Count; i++)
        {
            RedShell shell = redShellPool[i];
            if (shell != null && shell.IsAvailable())
            {
                if (shell.transform.parent == storageParent)
                {
                    shell.transform.localPosition = Vector3.zero;
                    shell.transform.localRotation = Quaternion.identity;
                }
                return shell;
            }
        }

        if (redShellPrefab == null)
        {
            Debug.LogWarning("[ItemService] Red shell prefab not configured.", this);
            return null;
        }

        GameObject shellObject = Instantiate(redShellPrefab, storageParent.position, storageParent.rotation, storageParent);
        shellObject.transform.localPosition = Vector3.zero;
        shellObject.transform.localRotation = Quaternion.identity;
        RedShell newShell = shellObject.GetComponent<RedShell>();
        if (newShell == null)
        {
            Debug.LogError("[ItemService] Instantiated red shell is missing RedShell component.", shellObject);
            Destroy(shellObject);
            return null;
        }

        redShellPool.Add(newShell);
        return newShell;
    }

    // Banana Pooling
    public Banana GetAvailableBanana(Transform storageParent)
    {
        for (int i = 0; i < bananaPool.Count; i++)
        {
            Banana banana = bananaPool[i];
            if (banana != null && banana.IsAvailable())
            {
                if (banana.transform.parent == storageParent)
                {
                    banana.transform.localPosition = Vector3.zero;
                    banana.transform.localRotation = Quaternion.identity;
                }
                return banana;
            }
        }

        if (bananaPrefab == null)
        {
            Debug.LogWarning("[ItemService] Banana prefab not configured.", this);
            return null;
        }

        GameObject bananaObject = Instantiate(bananaPrefab, storageParent.position, storageParent.rotation, storageParent);
        bananaObject.transform.localPosition = Vector3.zero;
        bananaObject.transform.localRotation = Quaternion.identity;
        Banana newBanana = bananaObject.GetComponent<Banana>();
        if (newBanana == null)
        {
            Debug.LogError("[ItemService] Instantiated banana is missing Banana component.", bananaObject);
            Destroy(bananaObject);
            return null;
        }

        bananaPool.Add(newBanana);
        return newBanana;
    }

    // Bobomb Pooling
    public Bobomb GetAvailableBobomb(Transform storageParent)
    {
        for (int i = 0; i < bobombPool.Count; i++)
        {
            Bobomb bobomb = bobombPool[i];
            if (bobomb != null && bobomb.IsAvailable())
            {
                if (bobomb.transform.parent == storageParent)
                {
                    bobomb.transform.localPosition = Vector3.zero;
                    bobomb.transform.localRotation = Quaternion.identity;
                }
                return bobomb;
            }
        }

        if (bobombPrefab == null)
        {
            Debug.LogWarning("[ItemService] Bobomb prefab not configured.", this);
            return null;
        }

        GameObject bobombObject = Instantiate(bobombPrefab, storageParent.position, storageParent.rotation, storageParent);
        bobombObject.transform.localPosition = Vector3.zero;
        bobombObject.transform.localRotation = Quaternion.identity;
        Bobomb newBobomb = bobombObject.GetComponent<Bobomb>();
        if (newBobomb == null)
        {
            Debug.LogError("[ItemService] Instantiated bobomb is missing Bobomb component.", bobombObject);
            Destroy(bobombObject);
            return null;
        }

        bobombPool.Add(newBobomb);
        return newBobomb;
    }

    // Reparent pools when storage changes
    public void ReparentGreenShellPool(Transform targetParent)
    {
        if (targetParent == null || greenShellPool == null)
        {
            return;
        }

        for (int i = 0; i < greenShellPool.Count; i++)
        {
            GreenShell shell = greenShellPool[i];
            if (shell == null)
            {
                continue;
            }

            Transform shellTransform = shell.transform;
            if (shellTransform.parent == targetParent)
            {
                continue;
            }

            shellTransform.SetParent(targetParent, false);
            shellTransform.localPosition = Vector3.zero;
            shellTransform.localRotation = Quaternion.identity;
        }
    }

    public void ReparentRedShellPool(Transform targetParent)
    {
        if (targetParent == null || redShellPool == null)
        {
            return;
        }

        for (int i = 0; i < redShellPool.Count; i++)
        {
            RedShell shell = redShellPool[i];
            if (shell == null)
            {
                continue;
            }

            Transform shellTransform = shell.transform;
            if (shellTransform.parent == targetParent)
            {
                continue;
            }

            shellTransform.SetParent(targetParent, false);
            shellTransform.localPosition = Vector3.zero;
            shellTransform.localRotation = Quaternion.identity;
        }
    }

    public void ReparentBananaPool(Transform targetParent)
    {
        if (targetParent == null || bananaPool == null)
        {
            return;
        }

        for (int i = 0; i < bananaPool.Count; i++)
        {
            Banana banana = bananaPool[i];
            if (banana == null)
            {
                continue;
            }

            Transform bananaTransform = banana.transform;
            if (bananaTransform.parent == targetParent)
            {
                continue;
            }

            bananaTransform.SetParent(targetParent, false);
            bananaTransform.localPosition = Vector3.zero;
            bananaTransform.localRotation = Quaternion.identity;
        }
    }

    public void ReparentBobombPool(Transform targetParent)
    {
        if (targetParent == null || bobombPool == null)
        {
            return;
        }

        for (int i = 0; i < bobombPool.Count; i++)
        {
            Bobomb bobomb = bobombPool[i];
            if (bobomb == null)
            {
                continue;
            }

            Transform bobombTransform = bobomb.transform;
            if (bobombTransform.parent == targetParent)
            {
                continue;
            }

            bobombTransform.SetParent(targetParent, false);
            bobombTransform.localPosition = Vector3.zero;
            bobombTransform.localRotation = Quaternion.identity;
        }
    }

    public void ReparentAllPools(Transform targetParent)
    {
        ReparentGreenShellPool(targetParent);
        ReparentRedShellPool(targetParent);
        ReparentBananaPool(targetParent);
        ReparentBobombPool(targetParent);
    }
}

























