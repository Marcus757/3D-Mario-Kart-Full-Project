using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class ItemManager : MonoBehaviour
{
    private ItemInputHandler input;
    private bool usePressedThisFrame;
    private bool useReleasedThisFrame;
    private bool useItemHeldLastFrame;
    private bool bobombTrailingActive;
    private GameObject activeTrailingBobomb;
    [SerializeField] private bool orbitingDebugLogging;

    [System.Serializable]
    private class DebugItemSettings
    {
        public DebugItemSelection selectedItem = DebugItemSelection.None;
    }

    [Header("DEBUG SETTINGS")]
    [SerializeField]
    private DebugItemSettings debugSettings = new DebugItemSettings();

    private ItemCatalog catalog;
    private bool catalogNotFoundLogged;
    private DebugItemSelection lastDebugSelectedItem = DebugItemSelection.None;
    private int debugForcedItemIndex = -1;
    private Coroutine activeRoulette;
    private Coroutine debugAutoRefillRoutine;
    private bool suppressDebugAutoRefill;
    private static readonly HashSet<string> tripleItemNames = new HashSet<string>
    {
        "TripleGreenShells",
        "TripleRedShells",
        "TripleBananas",
        "TripleMushroom"
    };

    private ItemHudPresenter hud;
    private ItemContext itemContext;
    private Dictionary<string, IItemBehaviour> itemBehaviours;
    
    private void Awake()
    {
        input = new ItemInputHandler(new GameControls());
        if (!TryEnsureCatalog())
        {
            enabled = false;
        }
    }
    
    private void OnEnable()
    {
        input.UsePressed += HandleUsePressed;
        input.UseReleased += HandleUseReleased;
        input.Enable();
    }
    
    private void OnDisable()
    {
        input.UsePressed -= HandleUsePressed;
        input.UseReleased -= HandleUseReleased;
        input.Disable();
    }

    private void HandleUsePressed() => usePressedThisFrame = true;

    private void HandleUseReleased()
    {
        useReleasedThisFrame = true;

        if (bobombTrailingActive && current_Item.Equals("Bobomb-Hold"))
        {
            ReleaseBobombTrailing();
        }
    }
    
    private Player player_script;
    private PlayerSounds playersounds;
    bool start_select = false;
    
    public GameObject ItemUI;
    public AudioSource PlaySelectsound;
    public AudioSource Selected;

    [SerializeField]
    private Transform heldItemParent;
    [SerializeField]
    private Transform trailingItemParent;

    private GameObject[] item_gameobjects;
    private Sprite[] itemIcons;
    private string[] itemNames;
    private Dictionary<string, int> itemIndexByName;
    private Dictionary<string, Sprite> iconByName;
    private Dictionary<string, Sprite> iconBySanitizedName;
    private readonly List<GameObject> runtimeHandInstances = new List<GameObject>();
    private readonly List<GameObject> runtimeTrailingInstances = new List<GameObject>();

    public Image your_item;

    [Header("ITEM PREFABS (Runtime)")]
    private GameObject greenShellPrefab;
    private GameObject redShellPrefab;
    private GameObject bananaPrefab;
    private GameObject coinPrefab;
    private GameObject bobombPrefab;
    private GameObject blueShellPrefab;
    public Transform shellSpawnPos;
    public Transform backshellPos; //also for bananas
    public Transform BananaSpawnPos;
    public Transform coinSpawnPos;

    [HideInInspector]
    public int item_index = 0;
    [HideInInspector]
    public int tripleItemCount = 0;
    [HideInInspector]
    public string current_Item;
    bool item_decided = false; //player can only use item once the scroll thingy decides item unless triple
    float GoldenMushroomTimer = 0;
    private bool startMushroomTimer = false;

    //we need to keep track of every self-moving item's waypoints because since we want the item to follow its waypoints from where the player shoots the shell on the track, we have to identify the current waypoint for that shell, or bullet bill, etc
    [HideInInspector]
    public int currentWayPoint = 0;
    [Header("ITEM WAYPOINT SYSTEM")]
    public Transform path;

    public Transform path1;
    public Transform path2;

    [Header("Renderers and Particles For Star Powerup")]
    public Material[] normalMaterials;
    public Renderer[] playerRenderers;
    public Material starMat;
    public GameObject starPS;
    [HideInInspector]
    public bool StarPowerUp;

    [HideInInspector]
    public bool isBullet = false;
    [Header("BulletStuff")]
    public GameObject bulletPlayer;
    public GameObject kart;


    [HideInInspector]
    public GameObject CurrentTrailingItem;

    private readonly Dictionary<string, GameObject> trailingVisuals = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

    public ParticleSystem coinSparkle;

    [HideInInspector]
    public bool canUseBulletAntigravity = true; 



    // Start is called before the first frame update
    void Start()
    {
        player_script = GetComponent<Player>();
        playersounds = GetComponent<PlayerSounds>();
        hud = new ItemHudPresenter(ItemUI, your_item, PlaySelectsound, Selected);
        itemContext = new ItemContext(this, player_script, playersounds);
        SyncItemsFromDefinitions();
        OrbitingItems.SetGlobalDebugLogging(orbitingDebugLogging);
        itemBehaviours = new Dictionary<string, IItemBehaviour>
        {
            { "GreenShell", new ShellItemBehaviour("GreenShell", false) },
            { "RedShell", new ShellItemBehaviour("RedShell", true) },
            { "Banana", new BananaItemBehaviour("Banana") },
            { "TripleGreenShells", new TripleShellBehaviour(false) },
            { "TripleRedShells", new TripleShellBehaviour(true) },
            { "TripleBananas", new TripleBananaBehaviour() },
            { "Mushroom", new MushroomItemBehaviour() },
            { "TripleMushroom", new TripleMushroomBehaviour() },
            { "GoldenMushroom", new GoldenMushroomBehaviour() },
            { "Coin", new CoinItemBehaviour() },
            { "ItemStar", new StarItemBehaviour() },
            { "Bullet", new BulletItemBehaviour() },
            { "BlueShell", new BlueShellItemBehaviour() },
            { "Bobomb-Hold", new BobombItemBehaviour() }
        };
        
        OrbitingItems.SetGlobalDebugLogging(orbitingDebugLogging);

        if (debugSettings.selectedItem != DebugItemSelection.None)
        {
            lastDebugSelectedItem = debugSettings.selectedItem;
            TriggerDebugItem(debugSettings.selectedItem);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Item Cache From Definitions")]
#endif
    public void SyncItemsFromDefinitions()
    {
        CleanupTrailingItem();
        ClearRuntimeHandInstances();
        ClearRuntimeTrailingInstances();
        trailingVisuals.Clear();
        CurrentTrailingItem = null;

        if (!TryEnsureCatalog())
        {
            item_gameobjects = System.Array.Empty<GameObject>();
            itemIcons = System.Array.Empty<Sprite>();
            itemNames = System.Array.Empty<string>();
            itemIndexByName = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            iconByName = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
            iconBySanitizedName = new Dictionary<string, Sprite>();
            return;
        }

        int count = catalog.DefinitionCount;

        item_gameobjects = count > 0 ? new GameObject[count] : System.Array.Empty<GameObject>();
        itemIcons = count > 0 ? new Sprite[count] : System.Array.Empty<Sprite>();
        itemNames = count > 0 ? new string[count] : System.Array.Empty<string>();
        itemIndexByName = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        iconByName = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
        iconBySanitizedName = new Dictionary<string, Sprite>();

        Transform parent = heldItemParent != null ? heldItemParent : transform;

        for (int i = 0; i < count; i++)
        {
            ItemDefinition definition = catalog.GetDefinition(i);
            Sprite icon = catalog.GetIcon(i);
            itemIcons[i] = icon;

            string canonicalName = catalog.GetCanonicalName(definition);
            itemNames[i] = canonicalName;

            if (!string.IsNullOrEmpty(canonicalName))
            {
                if (!itemIndexByName.ContainsKey(canonicalName))
                {
                    itemIndexByName.Add(canonicalName, i);
                }
            }

            if (icon != null && !string.IsNullOrEmpty(canonicalName))
            {
                if (!iconByName.ContainsKey(canonicalName))
                {
                    iconByName.Add(canonicalName, icon);
                }

                string sanitized = SanitizeName(canonicalName);
                if (!string.IsNullOrEmpty(sanitized) && !iconBySanitizedName.ContainsKey(sanitized))
                {
                    iconBySanitizedName.Add(sanitized, icon);
                }
            }

            if (!Application.isPlaying || definition == null)
            {
                continue;
            }

            GameObject instance = catalog.InstantiateHeldVisual(i, parent);
            if (instance == null)
            {
                continue;
            }

            AssignOrbitingOwner(instance);
            instance.SetActive(false);
            item_gameobjects[i] = instance;
            runtimeHandInstances.Add(instance);

            if (!string.IsNullOrEmpty(canonicalName))
            {
                GameObject trailingInstance = catalog.InstantiateTrailingVisual(canonicalName, GetTrailingParent());
                if (trailingInstance != null)
                {
                    AssignOrbitingOwner(trailingInstance);
                    trailingInstance.SetActive(false);
                    trailingVisuals[canonicalName] = trailingInstance;
                    runtimeTrailingInstances.Add(trailingInstance);
                }
            }
        }

        CacheRuntimePrefabs();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

        OrbitingItems.SetGlobalDebugLogging(orbitingDebugLogging);
    }

    private void ClearRuntimeHandInstances()
    {
        if (runtimeHandInstances == null || runtimeHandInstances.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeHandInstances.Count; i++)
        {
            GameObject instance = runtimeHandInstances[i];
            if (instance == null)
            {
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(instance);
            }
            else
#endif
            {
                Destroy(instance);
            }
        }

        runtimeHandInstances.Clear();
    }

    private void ClearRuntimeTrailingInstances()
    {
        if (runtimeTrailingInstances == null || runtimeTrailingInstances.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeTrailingInstances.Count; i++)
        {
            GameObject instance = runtimeTrailingInstances[i];
            if (instance == null)
            {
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(instance);
            }
            else
#endif
            {
                Destroy(instance);
            }
        }

        runtimeTrailingInstances.Clear();
    }

    private void AssignOrbitingOwner(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        OrbitingItems[] orbitingItems = root.GetComponentsInChildren<OrbitingItems>(true);
        for (int i = 0; i < orbitingItems.Length; i++)
        {
            orbitingItems[i].SetOwner(gameObject);
        }
    }

    private Transform GetTrailingParent()
    {
        if (trailingItemParent != null)
        {
            return trailingItemParent;
        }

        if (backshellPos != null)
        {
            return backshellPos;
        }

        return transform;
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
            Debug.LogError("ItemCatalog could not be found in the scene.", this);
            catalogNotFoundLogged = true;
        }

        return false;
    }

    private void CacheRuntimePrefabs()
    {
        greenShellPrefab = GetPrefabFromDefinitions(DebugItemSelection.GreenShell);
        redShellPrefab = GetPrefabFromDefinitions(DebugItemSelection.RedShell);
        bananaPrefab = GetPrefabFromDefinitions(DebugItemSelection.Banana);
        coinPrefab = GetPrefabFromDefinitions(DebugItemSelection.Coin);
        bobombPrefab = GetPrefabFromDefinitions(DebugItemSelection.BobombHold);
        blueShellPrefab = GetPrefabFromDefinitions(DebugItemSelection.BlueShell);
    }

    private GameObject GetPrefabFromDefinitions(DebugItemSelection selection)
    {
        if (!TryEnsureCatalog())
        {
            return null;
        }

        return catalog.GetWorldPrefab(selection);
    }

    private GameObject GetHeldVisual(int index)
    {
        if (item_gameobjects == null || index < 0 || index >= item_gameobjects.Length)
        {
            return null;
        }

        return item_gameobjects[index];
    }

    private GameObject GetHeldVisual(string canonicalName)
    {
        return GetHeldVisual(GetItemIndex(canonicalName));
    }

    private int GetItemIndex(string canonicalName)
    {
        if (string.IsNullOrEmpty(canonicalName) || itemIndexByName == null)
        {
            return -1;
        }

        return itemIndexByName.TryGetValue(canonicalName, out int index) ? index : -1;
    }

    private string GetItemNameByIndex(int index)
    {
        if (itemNames == null || index < 0 || index >= itemNames.Length)
        {
            return null;
        }

        return itemNames[index];
    }

    private void DeactivateHeldVisual(int index)
    {
        GameObject visual = GetHeldVisual(index);
        if (visual != null)
        {
            visual.SetActive(false);
        }
    }

    private void DeactivateHeldVisual(string canonicalName)
    {
        DeactivateHeldVisual(GetItemIndex(canonicalName));
    }

    private GameObject InstantiateItemPrefab(string itemName, GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemManager] Unable to spawn '{itemName}' because its prefab reference is missing.", this);
            return null;
        }

        return parent != null
            ? Instantiate(prefab, position, rotation, parent)
            : Instantiate(prefab, position, rotation);
    }

    private ItemDefinition GetDefinitionByName(string canonicalName)
    {
        if (string.IsNullOrEmpty(canonicalName) || !TryEnsureCatalog())
        {
            return null;
        }

        return catalog.GetDefinition(canonicalName);
    }

#if UNITY_EDITOR
    [ContextMenu("Clear Runtime Item Cache")]
    public void ClearLegacyItemArrays()
    {
        ClearRuntimeHandInstances();
        item_gameobjects = System.Array.Empty<GameObject>();
        itemIcons = System.Array.Empty<Sprite>();
        itemNames = System.Array.Empty<string>();
        itemIndexByName = null;
        iconByName = null;
        iconBySanitizedName = null;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // Update is called once per frame
    void Update()
    {

        wayPointSystemCurrent();

        input.Update();

        bool useItemPressedThisFrame = usePressedThisFrame;
        bool useItemHeldNow = input.UseHeld;
        bool useItemReleasedThisFrame = useReleasedThisFrame || (useItemHeldLastFrame && !useItemHeldNow);
        usePressedThisFrame = false;
        useReleasedThisFrame = false;
 
        if (debugSettings.selectedItem != lastDebugSelectedItem)
        {
            lastDebugSelectedItem = debugSettings.selectedItem;
            if (debugSettings.selectedItem != DebugItemSelection.None)
            {
                TriggerDebugItem(debugSettings.selectedItem);
            }
        }
 

        if (player_script.hasitem)  //player has collided with an itembox and needs an item
        {
            if (!start_select) //this ensures item select process does not begin until player has used up curret item
            {
                start_select = true;
                if (activeRoulette != null)
                {
                    StopCoroutine(activeRoulette);
                }
                activeRoulette = StartCoroutine(Item_Select());
            }

            if(GoldenMushroomTimer > 0 && startMushroomTimer)
            {
                GoldenMushroomTimer -= Time.deltaTime;
            }


            if (useItemPressedThisFrame && item_decided && !player_script.HitByBanana_ && !player_script.HitByShell_) //if item array order changes, change the indexes of utility methods and these if statements
            {
            if (itemBehaviours != null && itemBehaviours.TryGetValue(current_Item, out var behaviour))
            {
                behaviour.OnUse(itemContext, input.AimBackwardHeld, useItemHeldNow, useItemPressedThisFrame);
            }
            }

            if (useItemReleasedThisFrame && item_decided && !player_script.HitByBanana_ && !player_script.HitByShell_)
            {
            if (itemBehaviours != null && itemBehaviours.TryGetValue(current_Item, out var behaviour))
            {
                behaviour.OnRelease(itemContext, input.AimBackwardHeld);
            }
            }
        }

        useItemHeldLastFrame = useItemHeldNow;
    }

    internal void StartTrailingItemIfNeeded(string canonicalName)
    {
        if (string.IsNullOrEmpty(canonicalName))
        {
            return;
        }

        if (!trailingVisuals.TryGetValue(canonicalName, out GameObject trailing) || trailing == null)
        {
            return;
        }

        if (CurrentTrailingItem == trailing && trailing.activeSelf)
        {
            return;
        }

        if (CurrentTrailingItem != null && CurrentTrailingItem != trailing)
        {
            CurrentTrailingItem.SetActive(false);
        }

        CurrentTrailingItem = trailing;

        if (player_script != null && player_script.Driver != null)
        {
            player_script.Driver.SetBool("hasItem", false);
        }

        if (!trailing.activeSelf)
        {
            trailing.SetActive(true);
        }

        GameObject heldItem = GetHeldVisual(canonicalName);
        if (heldItem != null)
        {
            heldItem.SetActive(false);
        }
    }

    internal void HandleGreenShellRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("GreenShell"))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            CleanupTrailingItem();
            ActivateHeldVisual("GreenShell");
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowBackward");
            }
            StartCoroutine(spawnShell(backshellPos, -1));
            if (!StarPowerUp && player_script != null && player_script.faces != null && player_script.faces.Length > 1)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            CleanupTrailingItem();
            ActivateHeldVisual("GreenShell");
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowForward");
            }
            StartCoroutine(spawnShell(shellSpawnPos, 1));
        }

        current_Item = "";
        used_Item_Done();
    }

    internal void HandleRedShellRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("RedShell"))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            CleanupTrailingItem();
            ActivateHeldVisual("RedShell");
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowBackward");
            }
            StartCoroutine(spawnRedShell(backshellPos, -1));
            if (!StarPowerUp && player_script != null && player_script.faces != null && player_script.faces.Length > 1)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            CleanupTrailingItem();
            ActivateHeldVisual("RedShell");
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowForward");
            }
            StartCoroutine(spawnRedShell(shellSpawnPos, 1));
        }

        current_Item = "";
        used_Item_Done();
    }

    internal void HandleBananaRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("Banana"))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            StartCoroutine(spawnBanana(-1));
            CleanupTrailingItem();
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowBackward");
            }
            if (!StarPowerUp && player_script != null && player_script.faces != null && player_script.faces.Length > 1)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            CleanupTrailingItem();
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowForward");
            }
            StartCoroutine(spawnBanana(1));
        }

        current_Item = "";
        used_Item_Done();
    }

    private void CleanupTrailingItem()
    {
        if (CurrentTrailingItem != null)
        {
            CurrentTrailingItem.SetActive(false);
            CurrentTrailingItem = null;
        }
    }

    private void ActivateHeldVisual(string canonicalName)
    {
        GameObject target = GetHeldVisual(canonicalName);
        if (target != null)
        {
            target.SetActive(true);
        }
    }

    private void ClearCurrentItemVisuals()
    {
        CleanupTrailingItem();

        if (item_gameobjects == null || item_gameobjects.Length == 0)
        {
            return;
        }

        for (int i = 0; i < item_gameobjects.Length; i++)
        {
            GameObject obj = item_gameobjects[i];
            if (obj == null)
            {
                continue;
            }

            if (IsTripleItem(obj.name))
            {
                ResetTripleChildren(obj);
            }

            obj.SetActive(false);
        }

        current_Item = "";
        tripleItemCount = 0;
        item_index = -1;
    }

    private static void ResetTripleChildren(GameObject tripleObject)
    {
        if (tripleObject == null)
        {
            return;
        }

        for (int i = 0; i < tripleObject.transform.childCount; i++)
        {
            tripleObject.transform.GetChild(i).gameObject.SetActive(true);
        }
    }

    private static bool IsTripleItem(string itemName)
    {
        return !string.IsNullOrEmpty(itemName) && tripleItemNames.Contains(itemName);
    }

    internal void HandleTripleShellUse(bool isRedShell, bool aimBackwardHeld)
    {
        string expectedName = isRedShell ? "TripleRedShells" : "TripleGreenShells";
        if (!current_Item.Equals(expectedName) || tripleItemCount <= 0)
        {
            return;
        }

        player_script.Driver.SetTrigger(aimBackwardHeld ? "ThrowBackward" : "ThrowForward");
        int shellIndex = GetItemIndex(isRedShell ? "RedShell" : "GreenShell");
        GameObject shellVisual = GetHeldVisual(shellIndex);
        if (shellVisual != null)
        {
            shellVisual.SetActive(true);
        }

        if (aimBackwardHeld)
        {
            if (isRedShell)
            {
                StartCoroutine(spawnRedShell(backshellPos, -1));
            }
            else
            {
                StartCoroutine(spawnShell(backshellPos, -1));
            }
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            if (isRedShell)
            {
                StartCoroutine(spawnRedShell(shellSpawnPos, 1));
            }
            else
            {
                StartCoroutine(spawnShell(shellSpawnPos, 1));
            }
        }

        tripleItemCount--;
        GameObject tripleVisual = GetHeldVisual(item_index);
        if (tripleVisual != null && tripleVisual.transform.childCount > tripleItemCount)
        {
            tripleVisual.transform.GetChild(tripleItemCount).gameObject.SetActive(false);
        }

        if (tripleItemCount < 1)
        {
            ResetTripleItemHolder();
        }
    }

    internal void HandleTripleBananaUse(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("TripleBananas") || tripleItemCount <= 0)
        {
            return;
        }

        if (aimBackwardHeld)
        {
            StartCoroutine(spawnBanana(-1));
            player_script.Driver.SetTrigger("ThrowBackward");
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            player_script.Driver.SetTrigger("ThrowForward");
            StartCoroutine(spawnBanana(1));
        }

        tripleItemCount--;
        GameObject tripleVisual = GetHeldVisual(item_index);
        if (tripleVisual != null && tripleVisual.transform.childCount > tripleItemCount)
        {
            tripleVisual.transform.GetChild(tripleItemCount).gameObject.SetActive(false);
        }

        if (tripleItemCount < 1)
        {
            ResetTripleItemHolder();
        }
    }

    private void ResetTripleItemHolder()
    {
        current_Item = "";
        GameObject tripleObject = GetHeldVisual(item_index);
        if (tripleObject != null)
        {
            tripleObject.SetActive(false);
            ResetTripleChildren(tripleObject);
        }
        tripleItemCount = 0;
        used_Item_Done();
    }

    public void HandleOrbitingItemConsumed(GameObject orbitingItem)
    {
        if (orbitingItem != null)
        {
            orbitingItem.SetActive(false);
        }

        int previousCount = tripleItemCount;
        if (previousCount <= 0)
        {
            ResetTripleItemHolder();
            return;
        }

        tripleItemCount = Mathf.Max(0, tripleItemCount - 1);

        GameObject tripleVisual = GetHeldVisual(item_index);
        if (tripleVisual != null && tripleVisual.transform.childCount > tripleItemCount)
        {
            tripleVisual.transform.GetChild(tripleItemCount).gameObject.SetActive(false);
        }

        if (tripleItemCount < 1)
        {
            ResetTripleItemHolder();
        }
    }

    internal void HandleMushroomUse()
    {
        if (!current_Item.Equals("Mushroom"))
        {
            return;
        }

        player_script.Boost_time = 2f;
        PlayBoostEffects();
        DeactivateHeldVisual(item_index);
        current_Item = "";
        used_Item_Done();
    }

    internal void HandleTripleMushroomUse(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("TripleMushroom") || tripleItemCount <= 0 || aimBackwardHeld)
        {
            return;
        }

        player_script.Boost_time = 2.5f;
        tripleItemCount--;
        PlayBoostEffects();
        GameObject tripleVisual = GetHeldVisual(item_index);
        if (tripleVisual != null && tripleVisual.transform.childCount > tripleItemCount)
        {
            tripleVisual.transform.GetChild(tripleItemCount).gameObject.SetActive(false);
        }
        if (tripleItemCount < 1)
        {
            ResetTripleItemHolder();
        }
    }

    private void PlayBoostEffects()
    {
        for (int i = 0; i < player_script.BoostBurstPS.transform.childCount; i++)
        {
            player_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play();
        }
        if (playersounds.Check_if_playing())
        {
            playersounds.Mario_Boost_Sounds[playersounds.sound_count].Play();
            playersounds.sound_count++;
        }
    }

    internal void HandleGoldenMushroomUse(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("GoldenMushroom") || aimBackwardHeld)
        {
            return;
        }

        startMushroomTimer = true;
        player_script.Boost_time = 2f;
        PlayBoostEffects();
        if (GoldenMushroomTimer < 0)
        {
            DeactivateHeldVisual(item_index);
            current_Item = "";
            used_Item_Done();
            startMushroomTimer = false;
        }
    }

    internal void HandleCoinUse()
    {
        if (!current_Item.Equals("Coin"))
        {
            return;
        }

        StartCoroutine(UseCoin());
        current_Item = "";
        used_Item_Done();
    }

    internal void HandleStarUse()
    {
        if (!current_Item.Equals("ItemStar"))
        {
            return;
        }

        current_Item = "";
        used_Item_Done();
        StartCoroutine(UseStar());
    }

    internal void HandleBulletUse(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("Bullet") || aimBackwardHeld || player_script.JUMP_PANEL)
        {
            return;
        }

        if (!player_script.antiGravity || canUseBulletAntigravity)
        {
            current_Item = "";
            StartCoroutine(UseBullet());
        }
    }

    internal void HandleBlueShellUse()
    {
        if (!current_Item.Equals("BlueShell"))
        {
            return;
        }

        player_script.Driver.SetTrigger("ThrowForward");
        StartCoroutine(useBlueShell());
        used_Item_Done();
        current_Item = "";
    }


    IEnumerator Item_Select()
    {
        ClearCurrentItemVisuals();

        int resolvedIndex = GetComponent<ItemDistributionManager>().getItemNumber();
        if (debugForcedItemIndex >= 0)
        {
            resolvedIndex = debugForcedItemIndex;
            debugForcedItemIndex = -1;
        }

        //random or forced index
        item_index = resolvedIndex;

        Sprite spinningSprite = null;
        string spinningSource = "icon-cache";

        if (itemIcons != null && item_index >= 0 && item_index < itemIcons.Length)
        {
            spinningSprite = itemIcons[item_index];
        }

        if (spinningSprite == null && TryEnsureCatalog() && item_index >= 0)
        {
            Sprite catalogSprite = catalog.GetIcon(item_index);
            if (catalogSprite != null)
            {
                spinningSprite = catalogSprite;
                spinningSource = "catalog";
            }
        }

        if (spinningSprite == null)
        {
            spinningSource = "lookup";
            string canonicalName = GetItemNameByIndex(item_index);
            spinningSprite = FindSpriteForItem(canonicalName);
        }

        Debug.Log($"[ItemManager] Item_Select -> index {item_index} spriteSource {spinningSource} spriteName {(spinningSprite != null ? spinningSprite.name : "null")}");

        hud.SetItemSprite(spinningSprite);
        hud.StartRoulette();
        
        // Minimum roulette time before player can stop it
        float minimumRouletteTime = 1.5f;
        float maxRouletteTime = 4f;
        float elapsedTime = 0f;
        
        // Wait for minimum time, then allow early stopping
        while (elapsedTime < maxRouletteTime)
        {
            elapsedTime += Time.deltaTime;
            
            // Check if player pressed item button after minimum time
            if (elapsedTime >= minimumRouletteTime && input.WasPressedThisFrame)
            {
                break; // Stop the roulette early
            }
            
            yield return null;
        }
        
        GameObject heldVisual = GetHeldVisual(item_index);
        if (heldVisual != null)
        {
            heldVisual.SetActive(true);
        }

        string selectedItemName = GetItemNameByIndex(item_index) ?? heldVisual?.name;
        bool isTripleSelection = IsTripleItem(selectedItemName);
        bool treatAsTriple = isTripleSelection || (heldVisual != null && heldVisual.CompareTag("Non-Hold-Item"));

        if (!treatAsTriple)
        {
            player_script.Driver.SetBool("hasItem", true);
            player_script.has_item_hold = true;
            tripleItemCount = 0;

            if (!string.IsNullOrEmpty(selectedItemName) &&
                string.Equals(selectedItemName, "GoldenMushroom", System.StringComparison.OrdinalIgnoreCase))
            {
                GoldenMushroomTimer = 10f;
            }
        }
        else
        {
            tripleItemCount = 3;
        }

        current_Item = selectedItemName ?? string.Empty;

        hud.PlayLocked();
        item_decided = true;
        activeRoulette = null;
    }

    //SPAWN FUNCTIONS
    IEnumerator spawnShell(Transform position, int direction) //spawns a green shell when shot
    {

        yield return new WaitForSeconds(0.15f);
        GameObject clone = InstantiateItemPrefab("GreenShell", greenShellPrefab, position.position, position.rotation);
        if (clone == null)
        {
            yield break;
        }

        GreenShell greenShell = clone.GetComponent<GreenShell>();
        if (greenShell == null)
        {
            Debug.LogWarning("[ItemManager] Spawned GreenShell prefab is missing GreenShell component.", clone);
            yield break;
        }

        greenShell.who_threw_shell = gameObject.name;

        if (direction == 1) //backwards or forwards -1 and 1 respectively
        {
            greenShell.myVelocity = transform.forward.normalized;
            greenShell.velocityMagOriginal = 6000;
            greenShell.AntiGravity = player_script.antiGravity;
            greenShell.lifetime = 0;

            yield return new WaitForSeconds(0.25f);
            DeactivateHeldVisual("GreenShell");

        }
        
        if (direction == -1)
        {
            greenShell.myVelocity = -transform.forward.normalized;
            greenShell.velocityMagOriginal = 3500;
            greenShell.AntiGravity = player_script.antiGravity;


            
            yield return new WaitForSeconds(0.25f);
            DeactivateHeldVisual("GreenShell");
            for (int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                {
                    player_script.current_face_material = player_script.faces[1]; //look left
                    player_script.SpecialFace = true;
                }
                yield return new WaitForSeconds(0.01f);
            }
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[2]; //blink
                player_script.SpecialFace = true;
            }
            yield return new WaitForSeconds(0.1f);
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[0];//normal
                player_script.SpecialFace = false;
            }
        }
        
        




    }
    IEnumerator spawnRedShell(Transform position, int direction)
    {
        yield return new WaitForSeconds(0.15f);
        GameObject clone = InstantiateItemPrefab("RedShell", redShellPrefab, position.position, position.rotation);
        if (clone == null)
        {
            yield break;
        }

        RedShell redShell = clone.GetComponent<RedShell>();
        if (redShell == null)
        {
            Debug.LogWarning("[ItemManager] Spawned RedShell prefab is missing RedShell component.", clone);
            yield break;
        }

        redShell.who_threw_shell = gameObject.name;
        redShell.AntiGravity = player_script.antiGravity;

        if (direction == 1)
        {
            clone.SetActive(true);
            redShell.current_node = currentWayPoint;
            yield return new WaitForSeconds(0.25f);
            DeactivateHeldVisual("RedShell");
        }
        else if (direction == -1)
        {
            clone.SetActive(false);
            redShell.enabled = false;

            GreenShell tempShell = clone.GetComponent<GreenShell>();
            if (tempShell == null)
            {
                tempShell = clone.AddComponent<GreenShell>();
            }

            tempShell.lifetime = 0;
            tempShell.myVelocity = -transform.forward.normalized;
            tempShell.velocityMagOriginal = 3500;
            tempShell.AntiGravity = player_script.antiGravity;
            tempShell.who_threw_shell = gameObject.name;

            clone.SetActive(true);

            yield return new WaitForSeconds(0.25f);
            DeactivateHeldVisual("RedShell");

            for (int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                {
                    player_script.SpecialFace = true;
                    player_script.current_face_material = player_script.faces[1];
                }
                yield return new WaitForSeconds(0.01f);
            }

            if (!StarPowerUp)
            {
                player_script.SpecialFace = true;
                player_script.current_face_material = player_script.faces[2];
            }

            yield return new WaitForSeconds(0.1f);

            if (!StarPowerUp)
            {
                player_script.SpecialFace = false;
                player_script.current_face_material = player_script.faces[0];
            }
        }
    }
    IEnumerator useBobomb(int direction)
    {
        if(direction == 1)
        {
            yield return new WaitForSeconds(0.1f);
            DeactivateHeldVisual(item_index);

            GameObject clone = InstantiateItemPrefab("Bobomb-Hold", bobombPrefab, BananaSpawnPos.position, BananaSpawnPos.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.SetActive(true);
            var cloneBomb = clone.GetComponent<Bobomb>();
            if (cloneBomb != null)
            {
                cloneBomb.bomb_thrown(transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity).z * 400);
            }
            clone.GetComponent<AudioSource>().enabled = true;

            if (cloneBomb != null)
            {
                for (int i = 0; i < cloneBomb.renderers.Length; i++)
                {
                    cloneBomb.renderers[i].enabled = true;
            }
                for (int i = 0; i < cloneBomb.spark.Length; i++)
            {
                    cloneBomb.spark[i].SetActive(true);
            }
                cloneBomb.whoThrewBomb = gameObject.name;
            }

        }
        if(direction == -1)
        {
            yield return new WaitForSeconds(0.1f);
            DeactivateHeldVisual(item_index);

            GameObject clone = InstantiateItemPrefab("Bobomb-Hold", bobombPrefab, backshellPos.position, BananaSpawnPos.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.SetActive(true);
            var cloneBomb = clone.GetComponent<Bobomb>();
            if (cloneBomb != null)
            {
                cloneBomb.bounce_count = 4;

                for (int i = 0; i < cloneBomb.renderers.Length; i++)
                {
                    cloneBomb.renderers[i].enabled = true;
                }
                for (int i = 0; i < cloneBomb.spark.Length; i++)
                {
                    cloneBomb.spark[i].SetActive(true);
                }
                cloneBomb.whoThrewBomb = gameObject.name;
            }
        }
    }
    IEnumerator spawnBanana(int direction)
    {
        GameObject clone;
        if(direction == 1)//forward
        {
            yield return new WaitForSeconds(0.1f);
            DeactivateHeldVisual("Banana");
            clone = InstantiateItemPrefab("Banana", bananaPrefab, BananaSpawnPos.position, BananaSpawnPos.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.GetComponent<Banana>().Banana_thrown(transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity).z * 200);
            clone.GetComponent<Banana>().whoThrewBanana = gameObject.name;
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
            clone = InstantiateItemPrefab("Banana", bananaPrefab, backshellPos.position, BananaSpawnPos.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.GetComponent<Banana>().whoThrewBanana = gameObject.name;
            for (int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                {
                    player_script.current_face_material = player_script.faces[1]; //make sure it is not changed, by repeating in for loop
                    player_script.SpecialFace = true;
                }
                yield return new WaitForSeconds(0.01f);
            }
            DeactivateHeldVisual("Banana");
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[2]; //blink
                player_script.SpecialFace = true;
            }
            yield return new WaitForSeconds(0.1f);
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[0];//normal
                player_script.SpecialFace = false;
            }

        }


    }
    IEnumerator UseCoin()
    {
        GameObject clone = InstantiateItemPrefab("Coin", coinPrefab, coinSpawnPos.position, coinSpawnPos.rotation);
        if (clone == null)
        {
            yield break;
        }
        clone.transform.SetParent(transform);
        DeactivateHeldVisual(item_index);
        GetComponent<ScoreCount>().COINCOUNT+=2;

        yield return new WaitForSeconds(0.3f);
        playersounds.effectSounds[9].Play();
        coinSparkle.Play();

    }
    IEnumerator UseStar()
    {
        float volume = GameObject.FindGameObjectWithTag("CourseMusic").GetComponent<AudioSource>().volume;
        float volume2 = GameObject.FindGameObjectWithTag("CourseMusic").transform.parent.GetComponent<AudioSource>().volume;

        DeactivateHeldVisual(item_index);
        StarPowerUp = true;
        for(int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].material = starMat;
            playerRenderers[i].sharedMaterial = starMat;
        }
        GameObject.FindGameObjectWithTag("CourseMusic").GetComponent<AudioSource>().volume = 0;
        GameObject.FindGameObjectWithTag("CourseMusic").transform.parent.GetComponent<AudioSource>().volume = 0;

        GameObject.Find("StarMusic").GetComponent<AudioSource>().Play();
        for(int i = 0; i < starPS.transform.childCount; i++)
        {
            starPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play();
        }

        if (playersounds.Check_if_playing())
        {
            playersounds.MarioStarSounds[playersounds.star_count_sound].Play();
            playersounds.star_count_sound++;
            if(playersounds.star_count_sound > 2)
            {
                playersounds.star_count_sound = 0;
            }
        }


        yield return new WaitForSeconds(7.5f);

        GameObject.FindGameObjectWithTag("CourseMusic").GetComponent<AudioSource>().volume = volume;
        GameObject.FindGameObjectWithTag("CourseMusic").transform.parent.GetComponent<AudioSource>().volume = volume2;
        GameObject.Find("StarMusic").GetComponent<AudioSource>().Stop();
        StarPowerUp = false;
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].material = normalMaterials[i];
            playerRenderers[i].sharedMaterial = normalMaterials[i];
        }
        for (int i = 0; i < starPS.transform.childCount; i++)
        {
            starPS.transform.GetChild(i).GetComponent<ParticleSystem>().Stop();
        }


    }
    IEnumerator UseBullet()
    {
        DeactivateHeldVisual(item_index);
        isBullet = true;
        bulletPlayer.SetActive(true);
        for(int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].enabled = false;
        }
        player_script.drifting = false;
        playersounds.effectSounds[0].Stop(); //drifting  noise
        playersounds.effectSounds[1].Stop(); //drifting spark noise
        player_script.Boost_time = 0;
        playersounds.BulletSounds[1].Play();
        playersounds.BulletSounds[0].Play();

        yield return new WaitForSeconds(11);
        used_Item_Done();


        isBullet = false;
        bulletPlayer.SetActive(false);
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].enabled = true;
        }
        player_script.currentspeed = 70;
        playersounds.BulletSounds[2].Play();
        playersounds.BulletSounds[0].Stop();


    }

    IEnumerator useBlueShell()
    {
        yield return new WaitForSeconds(0.15f);
        GameObject clone = InstantiateItemPrefab("BlueShell", blueShellPrefab, shellSpawnPos.position, shellSpawnPos.transform.rotation);
        if (clone == null)
        {
            yield break;
        }
        clone.SetActive(true);
        clone.GetComponent<BlueShell>().current_node = currentWayPoint;
        clone.GetComponent<BlueShell>().AntiGravity = player_script.antiGravity;
        DeactivateHeldVisual(item_index);
        clone.GetComponent<BlueShell>().who_threw_shell = gameObject.name;
    }



    public void used_Item_Done() //resets the ui and bools
    {
        // For testing we keep the UI visible and skip the auto-refill.
        
        player_script.hasitem = false;
        player_script.has_item_hold = false;
        item_decided = false;
        start_select = false;
        hud.StopRoulette();

        if (player_script != null && player_script.Driver != null)
        {
        player_script.Driver.SetBool("hasItem", false);
        }

        if (!suppressDebugAutoRefill && debugSettings.selectedItem != DebugItemSelection.None)
        {
            if (debugAutoRefillRoutine != null)
            {
                StopCoroutine(debugAutoRefillRoutine);
            }

            debugAutoRefillRoutine = StartCoroutine(DebugAutoRefillRoutine());
        }
    }
    
    private int ResolveDebugItemIndex(DebugItemSelection selection)
    {
        string targetName = GetDebugItemName(selection);
        if (!string.IsNullOrEmpty(targetName))
        {
            int directIndex = GetItemIndex(targetName);
            if (directIndex != -1)
            {
                return directIndex;
        }

        string sanitizedTarget = SanitizeName(targetName);
            int sanitizedIndex = FindIndexBySanitizedName(sanitizedTarget);
            if (sanitizedIndex != -1)
            {
                return sanitizedIndex;
            }
        }

        int keywordIndex = ResolveDebugItemIndexByKeywords(selection);
        if (keywordIndex != -1)
        {
            return keywordIndex;
        }

        return -1;
    }

    private string GetDebugItemName(DebugItemSelection itemSelection)
    {
        switch (itemSelection)
        {
            case DebugItemSelection.GreenShell:
                return "GreenShell";
            case DebugItemSelection.TripleGreenShells:
                return "TripleGreenShells";
            case DebugItemSelection.RedShell:
                return "RedShell";
            case DebugItemSelection.TripleRedShells:
                return "TripleRedShells";
            case DebugItemSelection.Mushroom:
                return "Mushroom";
            case DebugItemSelection.TripleMushroom:
                return "TripleMushroom";
            case DebugItemSelection.Banana:
                return "Banana";
            case DebugItemSelection.TripleBananas:
                return "TripleBananas";
            case DebugItemSelection.GoldenMushroom:
                return "GoldenMushroom";
            case DebugItemSelection.Coin:
                return "Coin";
            case DebugItemSelection.ItemStar:
                return "ItemStar";
            case DebugItemSelection.Bullet:
                return "Bullet";
            case DebugItemSelection.BobombHold:
                return "Bobomb-Hold";
            case DebugItemSelection.BlueShell:
                return "BlueShell";
            case DebugItemSelection.None:
            default:
                return null;
        }
    }

    private int ResolveDebugItemIndexByKeywords(DebugItemSelection itemSelection)
    {
        switch (itemSelection)
        {
            case DebugItemSelection.GreenShell:
                return FindIndexByKeywords("green", "shell");
            case DebugItemSelection.TripleGreenShells:
                return FindIndexByKeywords("triple", "green", "shell");
            case DebugItemSelection.RedShell:
                return FindIndexByKeywords("red", "shell");
            case DebugItemSelection.TripleRedShells:
                return FindIndexByKeywords("triple", "red", "shell");
            case DebugItemSelection.Mushroom:
                return FindIndexByKeywords("mushroom");
            case DebugItemSelection.TripleMushroom:
                return FindIndexByKeywords("triple", "mushroom");
            case DebugItemSelection.Banana:
                return FindIndexByKeywords("banana");
            case DebugItemSelection.TripleBananas:
                return FindIndexByKeywords("triple", "banana");
            case DebugItemSelection.GoldenMushroom:
                return FindIndexByKeywords("gold", "mushroom");
            case DebugItemSelection.Coin:
                return FindIndexByKeywords("coin");
            case DebugItemSelection.ItemStar:
                return FindIndexByKeywords("star");
            case DebugItemSelection.Bullet:
                return FindIndexByKeywords("bullet");
            case DebugItemSelection.BobombHold:
                return FindIndexByKeywords("bobomb", "hold");
            case DebugItemSelection.BlueShell:
                return FindIndexByKeywords("blue", "shell");
            default:
                return -1;
        }
    }

    private int FindIndexByKeywords(params string[] keywords)
    {
        if (keywords == null || keywords.Length == 0 || itemNames == null)
        {
            return -1;
        }

        string[] sanitizedKeywords = new string[keywords.Length];
        for (int k = 0; k < keywords.Length; k++)
        {
            sanitizedKeywords[k] = SanitizeName(keywords[k]);
        }

        for (int i = 0; i < itemNames.Length; i++)
        {
            string candidate = itemNames[i];
            if (string.IsNullOrEmpty(candidate) && item_gameobjects != null && i < item_gameobjects.Length)
        {
            var obj = item_gameobjects[i];
                if (obj != null)
                {
                    candidate = obj.name;
                }
            }

            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            string sanitizedObject = SanitizeName(candidate);
            if (string.IsNullOrEmpty(sanitizedObject))
            {
                continue;
            }

            bool allMatch = true;
            for (int k = 0; k < sanitizedKeywords.Length; k++)
            {
                if (string.IsNullOrEmpty(sanitizedKeywords[k]))
                {
                    continue;
                }

                if (!sanitizedObject.Contains(sanitizedKeywords[k]))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindIndexBySanitizedName(string sanitizedName)
    {
        if (string.IsNullOrEmpty(sanitizedName))
        {
            return -1;
        }

        if (itemNames != null)
        {
            for (int i = 0; i < itemNames.Length; i++)
            {
                string name = itemNames[i];
                if (!string.IsNullOrEmpty(name) && SanitizeName(name) == sanitizedName)
                {
                    return i;
                }
            }
        }

        if (item_gameobjects != null)
        {
            for (int i = 0; i < item_gameobjects.Length; i++)
            {
                var obj = item_gameobjects[i];
                if (obj == null)
                {
                    continue;
                }

                if (SanitizeName(obj.name) == sanitizedName)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    private Sprite FindSpriteForItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        if (iconByName != null && iconByName.TryGetValue(itemName, out var directSprite) && directSprite != null)
        {
            return directSprite;
        }

        string sanitizedName = SanitizeName(itemName);
        if (!string.IsNullOrEmpty(sanitizedName) &&
            iconBySanitizedName != null &&
            iconBySanitizedName.TryGetValue(sanitizedName, out var sanitizedSprite) &&
            sanitizedSprite != null)
        {
            return sanitizedSprite;
        }

        if (TryEnsureCatalog())
        {
            Sprite catalogSprite = catalog.GetIcon(itemName);
            if (catalogSprite != null)
            {
                return catalogSprite;
            }
        }
 
        return null;
    }

    private void TriggerDebugItem(DebugItemSelection selection)
    {
        if (selection == DebugItemSelection.None || player_script == null)
        {
            return;
        }

        int index = ResolveDebugItemIndex(selection);
        if (index < 0)
        {
            return;
        }

        if (activeRoulette != null)
        {
            StopCoroutine(activeRoulette);
            activeRoulette = null;
        }

        if (debugAutoRefillRoutine != null)
        {
            StopCoroutine(debugAutoRefillRoutine);
            debugAutoRefillRoutine = null;
        }

        bool previousSuppress = suppressDebugAutoRefill;
        suppressDebugAutoRefill = true;
        used_Item_Done();
        suppressDebugAutoRefill = previousSuppress;

        debugForcedItemIndex = index;
        current_Item = string.Empty;

        if (PlaySelectsound != null)
        {
            PlaySelectsound.Play();
        }

        player_script.hasitem = true;
    }

    private IEnumerator DebugAutoRefillRoutine()
    {
        yield return null;
        debugAutoRefillRoutine = null;
        TriggerDebugItem(debugSettings.selectedItem);
    }

    public int wayPointSystemCurrent()
    {
        return currentWayPoint;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (path1.GetChild(currentWayPoint) == other.transform  || path2.GetChild(currentWayPoint) == other.transform)
        {
            if (currentWayPoint == path.childCount - 1) //if last node, set the next node to first
            {
                currentWayPoint = 0;
            }
            else
            {
                currentWayPoint++;
            }

        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        /*
        string name = collision.gameObject.name;
        for(int i = 0; i < 3; i++)
        {
            if(item_gameobjects[7].transform.GetChild(i).name == name)
            {
                Physics.IgnoreCollision(collision.collider, transform.GetComponent<SphereCollider>());
            }
        }
        */
    }


    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.name.Equals("ItemPathColliderPath1"))
        {
            path = path1;
        }
        if (other.gameObject.name.Equals("ItemPathColliderPath2"))
        {
            path = path2;
        }
    }

    public bool IsBobombTrailingActive => bobombTrailingActive;

    public void HandleBobombForwardUse()
    {
        if (!current_Item.Equals("Bobomb-Hold"))
        {
            return;
        }

        player_script.Driver.SetTrigger("ThrowForward");
        StartCoroutine(useBobomb(1));
        used_Item_Done();
        current_Item = "";
    }

    public void HandleBobombStartTrailing()
    {
        if (!current_Item.Equals("Bobomb-Hold"))
        {
            return;
        }

        StartBobombTrailing();
    }

    public void HandleBobombRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("Bobomb-Hold"))
        {
            return;
        }

        ReleaseBobombTrailing();
    }

    private void StartBobombTrailing()
    {
        bobombTrailingActive = true;
        player_script.Driver.SetBool("hasItem", false);
        DeactivateHeldVisual(item_index);

        activeTrailingBobomb = InstantiateItemPrefab("Bobomb-Hold", bobombPrefab, backshellPos.position, backshellPos.rotation, backshellPos);
        if (activeTrailingBobomb == null)
        {
            bobombTrailingActive = false;
            return;
        }

        var bombScript = activeTrailingBobomb.GetComponent<Bobomb>();
        if (bombScript != null)
        {
            bombScript.whoThrewBomb = gameObject.name;
            bombScript.BeginHeld(OnBobombHeldExplosion);
        }
        else
        {
        }

        CurrentTrailingItem = activeTrailingBobomb;
    }

    private void ReleaseBobombTrailing()
    {

        bobombTrailingActive = false;

        if (activeTrailingBobomb != null)
        {
            var bombScript = activeTrailingBobomb.GetComponent<Bobomb>();
            if (bombScript != null)
            {
                bombScript.ReleaseHeldAsMine();
            }
            ReleaseBobombAsMine(activeTrailingBobomb, -transform.forward);
            activeTrailingBobomb = null;
        }

        CurrentTrailingItem = null;
        player_script.Driver.SetTrigger("ThrowBackward");
        used_Item_Done();
        current_Item = "";
    }

    private void OnBobombHeldExplosion()
    {
        bobombTrailingActive = false;

        if (activeTrailingBobomb != null)
        {
            activeTrailingBobomb.transform.SetParent(null, true);
            activeTrailingBobomb = null;
        }

        CurrentTrailingItem = null;
        used_Item_Done();
        current_Item = "";
    }

    private void ReleaseBobombAsMine(GameObject bombObject, Vector3 forwardDirection)
    {
        if (bombObject == null)
        {
            return;
        }

        bombObject.transform.SetParent(null, true);
        bombObject.SetActive(true);

        var rb = bombObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(forwardDirection.normalized * 15f, ForceMode.VelocityChange);
        }

        var bombScript = bombObject.GetComponent<Bobomb>();
        if (bombScript != null)
        {
            bombScript.enabled = true;
            bombScript.whoThrewBomb = gameObject.name;
        }

        var audio = bombObject.GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
        }
    }
}
