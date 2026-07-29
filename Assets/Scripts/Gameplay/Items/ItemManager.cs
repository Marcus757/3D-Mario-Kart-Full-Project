using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class ItemManager : MonoBehaviour, IItemDriver
{
    private ItemInputHandler input;
    private bool usePressedThisFrame;
    private bool useReleasedThisFrame;
    private bool useItemHeldLastFrame;
    private bool bobombTrailingActive;
    private Bobomb activeTrailingBobomb;
    [SerializeField] private bool orbitingDebugLogging;

    private Transform itemsRuntimeParent;
    private ItemService itemService;
    
    private GreenShell activeGreenShell;
    [SerializeField, Tooltip("Duration in seconds before a held green shell begins trailing when the use button is held.")]
    private float greenShellHoldToTrailTime = 0.25f;
    private bool awaitingGreenShellHoldDecision;
    private float greenShellHoldStartTime;
    
    private RedShell activeRedShell;
    [SerializeField, Tooltip("Duration in seconds before a held red shell begins trailing when the use button is held.")]
    private float redShellHoldToTrailTime = 0.25f;
    private bool awaitingRedShellHoldDecision;
    private float redShellHoldStartTime;
    
    private Banana activeBanana;
    private Bobomb activeBobomb;

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
        itemService = ItemService.Instance;
        if (!TryEnsureCatalog())
        {
            enabled = false;
        }
    }

    // IItemDriver implementation
    public Transform ForwardSpawn => shellSpawnPos != null ? shellSpawnPos : transform;
    public Transform BackSpawn => backshellPos != null ? backshellPos : transform;
    public Transform HeldParent => heldItemParent != null ? heldItemParent : transform;
    public Transform TrailingParent => trailingItemParent != null ? trailingItemParent : (backshellPos != null ? backshellPos : transform);
    public Transform ItemsStorage => GetItemsParent();
    public int CurrentWaypoint => currentWayPoint;
    public bool IsStarActive => StarPowerUp;
    public bool IsAntiGravity => player_script != null && player_script.antiGravity;
    public string DriverName => gameObject.name;
    public GameObject DriverGameObject => gameObject;

    public void TriggerThrowForward()
    {
        if (player_script != null && player_script.Driver != null)
        {
            player_script.Driver.SetTrigger("ThrowForward");
        }
    }

    public void TriggerThrowBackward()
    {
        if (player_script != null && player_script.Driver != null)
        {
            player_script.Driver.SetTrigger("ThrowBackward");
        }
    }

    public void SetHasItem(bool hasItem)
    {
        if (player_script != null && player_script.Driver != null)
        {
            player_script.Driver.SetBool("hasItem", hasItem);
        }
    }

    public Rigidbody GetRigidbody()
    {
        return GetComponent<Rigidbody>();
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
        activeGreenShell = null;
        activeRedShell = null;
        activeBanana = null;
        activeBobomb = null;

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
        coinPrefab = GetPrefabFromDefinitions(DebugItemSelection.Coin);
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

        ProcessGreenShellHold(useItemHeldNow);
        ProcessRedShellHold(useItemHeldNow);
        useItemHeldLastFrame = useItemHeldNow;
    }

    internal void StartTrailingItemIfNeeded(string canonicalName)
    {
        if (IsGreenShellName(canonicalName))
        {
            GreenShell shell = activeGreenShell != null && activeGreenShell.IsAvailable() ? activeGreenShell : itemService.GetAvailableGreenShell(ItemsStorage);
            if (shell != null)
            {
                shell.Initialize(this);
                shell.EnterTrailing(GetTrailingParent());
                activeGreenShell = shell;
                CurrentTrailingItem = shell.gameObject;
            }

            SetHasItem(false);
            return;
        }

        if (IsRedShellName(canonicalName))
        {
            RedShell shell = activeRedShell != null && activeRedShell.IsAvailable() ? activeRedShell : itemService.GetAvailableRedShell(ItemsStorage);
            if (shell != null)
            {
                shell.Initialize(this);
                shell.EnterTrailing(GetTrailingParent());
                activeRedShell = shell;
                CurrentTrailingItem = shell.gameObject;
            }

            SetHasItem(false);
            return;
        }

        if (string.Equals(canonicalName, "Banana", System.StringComparison.OrdinalIgnoreCase))
        {
            Banana banana = activeBanana != null && activeBanana.IsAvailable() ? activeBanana : itemService.GetAvailableBanana(ItemsStorage);
            if (banana != null)
            {
                banana.Initialize(this);
                banana.EnterTrailing(GetTrailingParent());
                activeBanana = banana;
                CurrentTrailingItem = banana.gameObject;
            }

            SetHasItem(false);
            GameObject heldItem = GetHeldVisual(canonicalName);
            if (heldItem != null)
            {
                heldItem.SetActive(false);
            }
            return;
        }

        if (string.Equals(canonicalName, "Bobomb-Hold", System.StringComparison.OrdinalIgnoreCase))
        {
            Bobomb bobomb = activeBobomb != null && activeBobomb.IsAvailable() ? activeBobomb : itemService.GetAvailableBobomb(ItemsStorage);
            if (bobomb != null)
            {
                bobomb.Initialize(this);
                bobomb.EnterTrailing(GetTrailingParent());
                activeBobomb = bobomb;
                activeTrailingBobomb = bobomb;
                CurrentTrailingItem = bobomb.gameObject;
                bobombTrailingActive = true;
            }

            SetHasItem(false);
            GameObject heldItem = GetHeldVisual(canonicalName);
            if (heldItem != null)
            {
                heldItem.SetActive(false);
            }
            return;
        }

        if (trailingVisuals == null || !trailingVisuals.TryGetValue(canonicalName, out GameObject trailing) || trailing == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (trailing != null)
        {
            Debug.Log($"[ItemManager] Attempting to start trailing item {canonicalName} active {trailing.activeSelf} localPos {trailing.transform.localPosition} worldPos {trailing.transform.position}", trailing);
        }
#endif

        if (CurrentTrailingItem == trailing && trailing.activeSelf)
        {
            return;
        }

        if (CurrentTrailingItem != null && CurrentTrailingItem != trailing)
        {
            CurrentTrailingItem.SetActive(false);
        }

        CurrentTrailingItem = trailing;

        SetHasItem(false);

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

    internal void HandleGreenShellUsePressed(bool aimBackwardHeld)
    {
        if (!IsGreenShellName(current_Item))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            awaitingGreenShellHoldDecision = false;
            return;
        }

        awaitingGreenShellHoldDecision = true;
        greenShellHoldStartTime = Time.time;
    }

    internal void HandleRedShellUsePressed(bool aimBackwardHeld)
    {
        if (!IsRedShellName(current_Item))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            awaitingRedShellHoldDecision = false;
            return;
        }

        awaitingRedShellHoldDecision = true;
        redShellHoldStartTime = Time.time;
    }

    internal void HandleGreenShellRelease(bool aimBackwardHeld)
    {
        if (!IsGreenShellName(current_Item))
        {
            return;
        }

        awaitingGreenShellHoldDecision = false;

        GreenShell shell = activeGreenShell != null ? activeGreenShell : itemService.GetAvailableGreenShell(ItemsStorage);
        if (shell == null)
        {
            Debug.LogWarning("[ItemManager] Green shell instance unavailable during launch.", this);
            return;
        }

        Transform spawnTransform = aimBackwardHeld ? BackSpawn : ForwardSpawn;
        Vector3 spawnPosition = spawnTransform.position;
        Quaternion spawnRotation = spawnTransform.rotation;

        CapsuleCollider playerCollider = GetComponent<CapsuleCollider>();
        if (playerCollider != null)
        {
            float directionSign = aimBackwardHeld ? -1f : 1f;
            Vector3 forwardDir = transform.forward.normalized;
            if (forwardDir.sqrMagnitude > Mathf.Epsilon)
            {
                float offsetDistance = playerCollider.radius + 0.5f;
                Vector3 offset = forwardDir * directionSign * offsetDistance;
                float heightOffset = (playerCollider.height * 0.5f) * Mathf.Clamp(forwardDir.y, -0.5f, 0.5f);
                spawnPosition = playerCollider.bounds.center + offset + (Vector3.up * heightOffset);
            }
        }

        float launchSpeed = aimBackwardHeld ? 3500f : 6000f;

        if (aimBackwardHeld)
        {
            TriggerThrowBackward();
            StartCoroutine(HandleBackwardThrowFaces());
        }
        else
        {
            TriggerThrowForward();
        }

        GreenShell heldShell = activeGreenShell != null && activeGreenShell.IsAvailable() ? activeGreenShell : null;

        CleanupTrailingItem();

        if (heldShell != null)
        {
            heldShell.ReturnToPool();
            if (heldShell == activeGreenShell)
            {
                activeGreenShell = null;
            }
        }

        if (aimBackwardHeld)
        {
            StartCoroutine(ItemLaunchers.LaunchGreenShellBackward(this, shell, spawnPosition, spawnRotation, launchSpeed));
        }
        else
        {
            StartCoroutine(ItemLaunchers.LaunchGreenShellForward(this, shell, spawnPosition, spawnRotation, launchSpeed));
        }

        CurrentTrailingItem = null;
        activeGreenShell = null;
        current_Item = string.Empty;
        used_Item_Done();
    }

    internal void HandleRedShellRelease(bool aimBackwardHeld)
    {
        if (!IsRedShellName(current_Item))
        {
            return;
        }

        awaitingRedShellHoldDecision = false;

        RedShell shell = activeRedShell != null ? activeRedShell : itemService.GetAvailableRedShell(ItemsStorage);
        if (shell == null)
        {
            Debug.LogWarning("[ItemManager] Red shell instance unavailable during launch.", this);
            return;
        }

        Transform spawnTransform = aimBackwardHeld ? BackSpawn : ForwardSpawn;
        Vector3 spawnPosition = spawnTransform.position;
        Quaternion spawnRotation = spawnTransform.rotation;

        CapsuleCollider playerCollider = GetComponent<CapsuleCollider>();
        if (playerCollider != null)
        {
            float directionSign = aimBackwardHeld ? -1f : 1f;
            Vector3 forwardDir = transform.forward.normalized;
            if (forwardDir.sqrMagnitude > Mathf.Epsilon)
            {
                float offsetDistance = playerCollider.radius + 0.5f;
                Vector3 offset = forwardDir * directionSign * offsetDistance;
                float heightOffset = (playerCollider.height * 0.5f) * Mathf.Clamp(forwardDir.y, -0.5f, 0.5f);
                spawnPosition = playerCollider.bounds.center + offset + (Vector3.up * heightOffset);
            }
        }

        if (aimBackwardHeld)
        {
            TriggerThrowBackward();
            StartCoroutine(HandleBackwardThrowFaces());
        }
        else
        {
            TriggerThrowForward();
        }

        RedShell heldShell = activeRedShell != null && activeRedShell.IsAvailable() ? activeRedShell : null;

        CleanupTrailingItem();

        if (heldShell != null)
        {
            heldShell.ReturnToPool();
            if (heldShell == activeRedShell)
            {
                activeRedShell = null;
            }
        }

        if (aimBackwardHeld)
        {
            StartCoroutine(ItemLaunchers.LaunchRedShellBackward(this, shell, spawnPosition, spawnRotation));
        }
        else
        {
            shell.Initialize(this);
            shell.EnterProjectile(spawnPosition, spawnRotation, currentWayPoint, IsAntiGravity, gameObject.name);
        }

        CurrentTrailingItem = null;
        activeRedShell = null;
        current_Item = string.Empty;
        used_Item_Done();
    }

    internal void HandleBananaRelease(bool aimBackwardHeld)
    {
        if (!string.Equals(current_Item, "Banana", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Banana banana = activeBanana != null ? activeBanana : itemService.GetAvailableBanana(ItemsStorage);
        if (banana == null)
        {
            Debug.LogWarning("[ItemManager] Banana instance unavailable during launch.", this);
            return;
        }

        banana.Initialize(this);
        banana.DetachFromParent();
        CurrentTrailingItem = null;
        activeBanana = null;

        if (aimBackwardHeld)
        {
            TriggerThrowBackward();
            StartCoroutine(ItemLaunchers.LaunchBananaBackward(this, banana, BackSpawn.position, BananaSpawnPos != null ? BananaSpawnPos.rotation : transform.rotation, false));
        }
        else
        {
            TriggerThrowForward();
            StartCoroutine(ItemLaunchers.LaunchBananaForward(this, banana, BananaSpawnPos != null ? BananaSpawnPos.position : transform.position, BananaSpawnPos != null ? BananaSpawnPos.rotation : transform.rotation, false));
        }

        current_Item = string.Empty;
        used_Item_Done();
    }

    private void CleanupTrailingItem()
    {
        if (CurrentTrailingItem == null)
        {
            return;
        }

        GreenShell greenShellComponent = CurrentTrailingItem.GetComponent<GreenShell>();
        if (greenShellComponent != null)
        {
            greenShellComponent.ReturnToPool();
            if (greenShellComponent == activeGreenShell)
            {
                activeGreenShell = null;
            }
            CurrentTrailingItem = null;
            return;
        }

        RedShell redShellComponent = CurrentTrailingItem.GetComponent<RedShell>();
        if (redShellComponent != null)
        {
            redShellComponent.ReturnToPool();
            if (redShellComponent == activeRedShell)
            {
                activeRedShell = null;
            }
            CurrentTrailingItem = null;
            return;
        }

        Banana bananaComponent = CurrentTrailingItem.GetComponent<Banana>();
        if (bananaComponent != null)
        {
            bananaComponent.ReturnToPool();
            if (bananaComponent == activeBanana)
            {
                activeBanana = null;
            }
            CurrentTrailingItem = null;
            return;
        }

        Bobomb bobombComponent = CurrentTrailingItem.GetComponent<Bobomb>();
        if (bobombComponent != null)
        {
            bobombComponent.ReturnToPool();
            if (bobombComponent == activeBobomb)
            {
                activeBobomb = null;
            }
            if (bobombComponent == activeTrailingBobomb)
            {
                activeTrailingBobomb = null;
                bobombTrailingActive = false;
            }
            CurrentTrailingItem = null;
            return;
        }

        CurrentTrailingItem.SetActive(false);
        CurrentTrailingItem = null;
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
        activeGreenShell = null;
        activeRedShell = null;
        activeBanana = null;
        activeBobomb = null;

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

        Banana banana = itemService.GetAvailableBanana(ItemsStorage);
        if (banana == null)
        {
            Debug.LogWarning("[ItemManager] Banana instance unavailable during triple launch.", this);
            return;
        }

        banana.Initialize(this);
        if (aimBackwardHeld)
        {
            banana.DetachFromParent();
            StartCoroutine(ItemLaunchers.LaunchBananaBackward(this, banana, BackSpawn.position, BananaSpawnPos != null ? BananaSpawnPos.rotation : transform.rotation, true));
            TriggerThrowBackward();
            if (!StarPowerUp)
            {
                player_script.current_face_material = player_script.faces[1];
            }
        }
        else
        {
            TriggerThrowForward();
            banana.DetachFromParent();
            StartCoroutine(ItemLaunchers.LaunchBananaForward(this, banana, BananaSpawnPos != null ? BananaSpawnPos.position : transform.position, BananaSpawnPos != null ? BananaSpawnPos.rotation : transform.rotation, true));
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
        string selectedItemName = GetItemNameByIndex(item_index) ?? heldVisual?.name;

        if (IsGreenShellName(selectedItemName))
        {
            if (heldVisual != null)
            {
                heldVisual.SetActive(false);
            }

            GreenShell shell = itemService.GetAvailableGreenShell(ItemsStorage);
            if (shell != null)
            {
                shell.Initialize(this);
                shell.EnterHeld(HeldParent);
                shell.gameObject.SetActive(true);
                activeGreenShell = shell;
            }
        }
        else if (IsRedShellName(selectedItemName))
        {
            if (heldVisual != null)
            {
                heldVisual.SetActive(false);
            }

            RedShell shell = itemService.GetAvailableRedShell(ItemsStorage);
            if (shell != null)
            {
                shell.Initialize(this);
                shell.EnterHeld(HeldParent);
                shell.gameObject.SetActive(true);
                activeRedShell = shell;
            }
        }
        else if (string.Equals(selectedItemName, "Banana", System.StringComparison.OrdinalIgnoreCase))
        {
            if (heldVisual != null)
            {
                heldVisual.SetActive(false);
            }

            Banana banana = itemService.GetAvailableBanana(ItemsStorage);
            if (banana != null)
            {
                banana.Initialize(this);
                banana.EnterHeld(HeldParent);
                banana.gameObject.SetActive(true);
                activeBanana = banana;
            }
        }
        else if (heldVisual != null)
        {
            heldVisual.SetActive(true);
        }

        bool isTripleSelection = IsTripleItem(selectedItemName);
        bool treatAsTriple = isTripleSelection || (heldVisual != null && heldVisual.CompareTag("Non-Hold-Item"));

        if (!treatAsTriple)
        {
            SetHasItem(true);
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
    IEnumerator spawnShell(Transform position, int direction)
    {
        GreenShell shell = itemService.GetAvailableGreenShell(ItemsStorage);
        if (shell == null)
        {
            yield break;
        }

        shell.Initialize(this);
        if (direction == 1)
        {
            yield return StartCoroutine(ItemLaunchers.LaunchGreenShellForward(this, shell, position.position, position.rotation, 6000f));
            DeactivateHeldVisual("GreenShell");
        }
        else
        {
            yield return StartCoroutine(ItemLaunchers.LaunchGreenShellBackward(this, shell, position.position, position.rotation, 3500f));
            DeactivateHeldVisual("GreenShell");
        }
    }

    IEnumerator spawnRedShell(Transform position, int direction)
    {
        RedShell shell = itemService.GetAvailableRedShell(ItemsStorage);
        if (shell == null)
        {
            yield break;
        }

        shell.Initialize(this);
        if (direction == 1)
        {
            yield return StartCoroutine(ItemLaunchers.LaunchRedShellForward(this, shell, position.position, position.rotation));
            DeactivateHeldVisual("RedShell");
        }
        else if (direction == -1)
        {
            yield return StartCoroutine(ItemLaunchers.LaunchRedShellBackward(this, shell, position.position, position.rotation));
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
        Bobomb bobomb = itemService.GetAvailableBobomb(ItemsStorage);
        if (bobomb == null)
        {
            yield break;
        }

        bobomb.Initialize(this);
        DeactivateHeldVisual(item_index);

        if (direction == 1)
        {
            yield return StartCoroutine(ItemLaunchers.LaunchBobombForward(this, bobomb, BananaSpawnPos.position, BananaSpawnPos.rotation));
        }
        else if (direction == -1)
        {
            yield return StartCoroutine(ItemLaunchers.LaunchBobombBackward(this, bobomb, BackSpawn.position, BananaSpawnPos.rotation));
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
        
        awaitingGreenShellHoldDecision = false;
        awaitingRedShellHoldDecision = false;
        player_script.hasitem = false;
        player_script.has_item_hold = false;
        item_decided = false;
        start_select = false;
        hud.StopRoulette();
        SetHasItem(false);

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
        Bobomb bobomb = activeBobomb != null && activeBobomb.IsAvailable() ? activeBobomb : itemService.GetAvailableBobomb(ItemsStorage);
        if (bobomb == null)
        {
            bobombTrailingActive = false;
            return;
        }

        bobomb.Initialize(this);
        bobomb.EnterTrailing(GetTrailingParent());
        activeBobomb = bobomb;
        activeTrailingBobomb = bobomb;
        CurrentTrailingItem = bobomb.gameObject;
        bobombTrailingActive = true;

        SetHasItem(false);
        DeactivateHeldVisual(item_index);
    }

    private void ReleaseBobombTrailing()
    {
        bobombTrailingActive = false;

        if (activeTrailingBobomb != null)
        {
            activeTrailingBobomb.ReleaseHeldAsMine();
            ReleaseBobombAsMine(activeTrailingBobomb.gameObject, -transform.forward);
            activeTrailingBobomb = null;
        }

        if (activeBobomb != null)
        {
            activeBobomb = null;
        }

        CurrentTrailingItem = null;
        TriggerThrowBackward();
        used_Item_Done();
        current_Item = "";
    }

    internal void OnBobombTrailingExploded(Bobomb bobomb)
    {
        bobombTrailingActive = false;

        if (bobomb != null)
        {
            bobomb.ReturnToPool();
            if (bobomb == activeBobomb)
            {
                activeBobomb = null;
            }
            if (bobomb == activeTrailingBobomb)
            {
                activeTrailingBobomb = null;
            }
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

        Bobomb bombScript = bombObject.GetComponent<Bobomb>();
        if (bombScript != null)
        {
            bombScript.EnterMine(bombObject.transform.position, bombObject.transform.rotation, gameObject.name);
            Rigidbody rb = bombObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(forwardDirection.normalized * 15f, ForceMode.VelocityChange);
            }
        }
    }

    private Transform GetItemsParent()
    {
        if (itemsRuntimeParent != null)
        {
            if (heldItemParent != null && itemsRuntimeParent != heldItemParent)
            {
                itemsRuntimeParent = heldItemParent;
                if (itemService != null)
                {
                    itemService.ReparentAllPools(itemsRuntimeParent);
                }
#if UNITY_EDITOR
                Debug.Log($"[ItemManager] Items parent switched to heldItemParent {itemsRuntimeParent.name}.", itemsRuntimeParent);
#endif
            }
            return itemsRuntimeParent;
        }

        if (heldItemParent != null)
        {
            itemsRuntimeParent = heldItemParent;
            if (itemService != null)
            {
                itemService.ReparentAllPools(itemsRuntimeParent);
            }
#if UNITY_EDITOR
            Debug.Log($"[ItemManager] Using heldItemParent {itemsRuntimeParent.name} as items parent.", itemsRuntimeParent);
#endif
            return itemsRuntimeParent;
        }

        Transform existing = transform.Find("ItemsParent");
        if (existing != null)
        {
            itemsRuntimeParent = existing;
#if UNITY_EDITOR
            Debug.Log($"[ItemManager] Found ItemsParent child {itemsRuntimeParent.name}.", itemsRuntimeParent);
#endif
            return itemsRuntimeParent;
        }

        itemsRuntimeParent = transform;
        if (itemService != null)
        {
            itemService.ReparentAllPools(itemsRuntimeParent);
        }
#if UNITY_EDITOR
        Debug.Log("[ItemManager] Falling back to kart transform for items parent.", this);
#endif
        return itemsRuntimeParent;
    }

    internal Transform GreenShellStorage => GetItemsParent();

    private static bool IsGreenShellName(string itemName)
    {
        return !string.IsNullOrEmpty(itemName) && string.Equals(itemName, "GreenShell", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedShellName(string itemName)
    {
        return !string.IsNullOrEmpty(itemName) && string.Equals(itemName, "RedShell", StringComparison.OrdinalIgnoreCase);
    }

    private void ProcessGreenShellHold(bool useItemHeldNow)
    {
        if (!awaitingGreenShellHoldDecision)
        {
            return;
        }

        if (!player_script.hasitem || !IsGreenShellName(current_Item))
        {
            awaitingGreenShellHoldDecision = false;
            return;
        }

        if (!useItemHeldNow)
        {
            awaitingGreenShellHoldDecision = false;
            return;
        }

        float elapsed = Time.time - greenShellHoldStartTime;
        if (elapsed < greenShellHoldToTrailTime)
        {
            return;
        }

        awaitingGreenShellHoldDecision = false;
#if UNITY_EDITOR
        Debug.Log($"[ItemManager] Green shell hold threshold met after {elapsed:F3}s. Entering trailing state.", this);
#endif
        StartTrailingItemIfNeeded("GreenShell");
    }

    private void ProcessRedShellHold(bool useItemHeldNow)
    {
        if (!awaitingRedShellHoldDecision)
        {
            return;
        }

        if (!player_script.hasitem || !IsRedShellName(current_Item))
        {
            awaitingRedShellHoldDecision = false;
            return;
        }

        if (!useItemHeldNow)
        {
            awaitingRedShellHoldDecision = false;
            return;
        }

        float elapsed = Time.time - redShellHoldStartTime;
        if (elapsed < redShellHoldToTrailTime)
        {
            return;
        }

        awaitingRedShellHoldDecision = false;
#if UNITY_EDITOR
        Debug.Log($"[ItemManager] Red shell hold threshold met after {elapsed:F3}s. Entering trailing state.", this);
#endif
        StartTrailingItemIfNeeded("RedShell");
    }

    internal void OnGreenShellTrailingConsumed(GreenShell shell)
    {
        awaitingGreenShellHoldDecision = false;
        if (shell != null)
        {
            shell.ReturnToPool();
            if (shell == activeGreenShell)
            {
                activeGreenShell = null;
            }
        }

        current_Item = string.Empty;
        CurrentTrailingItem = null;
        used_Item_Done();
    }

    internal void OnGreenShellReturned(GreenShell shell)
    {
        awaitingGreenShellHoldDecision = false;
        if (shell == activeGreenShell)
        {
            activeGreenShell = null;
        }
    }

    internal void OnRedShellTrailingConsumed(RedShell shell)
    {
        if (shell != null)
        {
            shell.ReturnToPool();
        }

        current_Item = string.Empty;
        CurrentTrailingItem = null;
        used_Item_Done();
    }

    internal void OnRedShellReturned(RedShell shell)
    {
        awaitingRedShellHoldDecision = false;
        if (shell == activeRedShell)
        {
            activeRedShell = null;
        }
    }

    internal void OnBananaTrailingConsumed(Banana banana)
    {
        if (banana != null)
        {
            banana.ReturnToPool();
            if (banana == activeBanana)
            {
                activeBanana = null;
            }
        }

        current_Item = string.Empty;
        CurrentTrailingItem = null;
        used_Item_Done();
    }

    internal void OnBananaReturned(Banana banana)
    {
        if (banana == activeBanana)
        {
            activeBanana = null;
        }
    }

    private IEnumerator HandleBackwardThrowFaces()
    {
        if (StarPowerUp || player_script == null || player_script.faces == null || player_script.faces.Length == 0)
        {
            yield break;
        }

        if (player_script.faces.Length > 1)
        {
            player_script.current_face_material = player_script.faces[1];
            player_script.SpecialFace = true;
        }

        yield return new WaitForSeconds(0.75f);

        if (player_script.faces.Length > 2)
        {
            player_script.current_face_material = player_script.faces[2];
        }

        yield return new WaitForSeconds(0.1f);

        if (player_script.faces.Length > 0)
        {
            player_script.current_face_material = player_script.faces[0];
            player_script.SpecialFace = false;
        }
    }
}



