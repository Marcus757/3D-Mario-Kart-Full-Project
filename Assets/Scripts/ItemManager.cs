using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class ItemManager : MonoBehaviour
{
    private GameControls controls;
    
    private bool UseItemTriggered => controls.Gameplay.UseItem.triggered;
    private bool UseItemHeld => controls.Gameplay.UseItem.IsPressed();
    private bool AimBackwardHeld => controls.Gameplay.AimBackward.IsPressed();
    private bool UseItemReleased => controls.Gameplay.UseItem.WasReleasedThisFrame();
    private bool useItemCancelThisFrame;
    private bool useItemHeldLastFrame;
    private bool bobombTrailingActive;
    private GameObject activeTrailingBobomb;
    [SerializeField]
    private float bobombHeldFuseDuration = 2f;
    [SerializeField]
    private bool bobombDebugLogging;

    private void LogBobombDebug(string message)
    {
        if (!bobombDebugLogging)
        {
            return;
        }

        Debug.Log($"[BobombDebug][{name}] {message}");
    }

    [System.Serializable]
    private class DebugBobombSettings
    {
        public bool overrideThrowSettings;
        [Range(0.1f, 4f)] public float throwForceMultiplier = 1f;
        public bool autoCalibrate;
        public bool matchArcAngle;
        [Range(0f, 80f)] public float desiredArcAngleDegrees = 30f;
    }

    [System.Serializable]
    private class DebugItemSettings
    {
        public DebugItemSelection selectedItem = DebugItemSelection.None;
        [Header("Bobomb Throw Overrides")]
        public DebugBobombSettings bobomb = new DebugBobombSettings();
    }

    [Header("DEBUG SETTINGS")]
    [SerializeField]
    private DebugItemSettings debugSettings = new DebugItemSettings();

    private ItemDefinition[] itemDefinitions;

    private DebugItemSelection lastDebugSelectedItem = DebugItemSelection.None;
    private int debugForcedItemIndex = -1;
    private Coroutine activeRoulette;
    private Coroutine debugAutoRefillRoutine;
    private bool suppressDebugAutoRefill;
    
    private void Awake()
    {
        controls = new GameControls();
        LogBobombDebug("Awake initialized GameControls instance");
    }
    
    private void OnEnable()
    {
        controls.Gameplay.Enable();
        controls.Gameplay.UseItem.canceled += OnUseItemCanceled;
        LogBobombDebug("Item controls enabled");
    }
    
    private void OnDisable()
    {
        controls.Gameplay.UseItem.canceled -= OnUseItemCanceled;
        LogBobombDebug("Item controls disabled");
        controls.Gameplay.Disable();
    }

    private void OnUseItemCanceled(InputAction.CallbackContext context)
    {
        useItemCancelThisFrame = true;
        LogBobombDebug($"UseItem canceled event received. phase={context.phase}");

        if (bobombTrailingActive && current_Item.Equals("Bobomb-Hold"))
        {
            LogBobombDebug("UseItem canceled while trailing; releasing immediately from canceled callback");
            ReleaseBobombTrailing();
        }
    }
    
    private Player player_script;
    private PlayerSounds playersounds;
    bool start_select = false;
    
    public GameObject ItemUI;
    public AudioSource PlaySelectsound;
    public AudioSource Selected;

    public Sprite[] items_possible;
    public GameObject[] item_gameobjects;
    public Image your_item;

    [Header("ITEMS")]
    public GameObject shell;
    public GameObject redShell;
    public GameObject banana;
    public GameObject coin;
    public GameObject bobomb;
    public GameObject BlueShell;
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


    public GameObject CurrentTrailingItem;
    public GameObject[] trailingItems;

    public ParticleSystem coinSparkle;

    [HideInInspector]
    public bool canUseBulletAntigravity = true; 



    // Start is called before the first frame update
    void Start()
    {
        player_script = GetComponent<Player>();
        playersounds = GetComponent<PlayerSounds>();
        
        LogBobombDebug("Start called, player and sound components cached");
        if (debugSettings.selectedItem != DebugItemSelection.None)
        {
            lastDebugSelectedItem = debugSettings.selectedItem;
            TriggerDebugItem(debugSettings.selectedItem);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Sync Item Arrays From Definitions")]
    public void SyncItemsFromDefinitions()
    {
        if (itemDefinitions == null || itemDefinitions.Length == 0)
        {
            return;
        }

        // Resize items_possible array if needed
        if (items_possible == null || items_possible.Length < itemDefinitions.Length)
        {
            System.Array.Resize(ref items_possible, itemDefinitions.Length);
        }

        for (int i = 0; i < itemDefinitions.Length; i++)
        {
            var def = itemDefinitions[i];
            items_possible[i] = def != null ? def.icon : null;
        }

        // Resize item_gameobjects array if needed
        if (item_gameobjects == null || item_gameobjects.Length < itemDefinitions.Length)
        {
            System.Array.Resize(ref item_gameobjects, itemDefinitions.Length);
        }

        for (int i = 0; i < itemDefinitions.Length; i++)
        {
            var def = itemDefinitions[i];
            item_gameobjects[i] = def != null ? (def.handPrefab != null ? def.handPrefab : def.prefab) : null;
        }

        shell = FindDefinitionPrefab(DebugItemSelection.GreenShell);
        redShell = FindDefinitionPrefab(DebugItemSelection.RedShell);
        banana = FindDefinitionPrefab(DebugItemSelection.Banana);
        coin = FindDefinitionPrefab(DebugItemSelection.Coin);
        bobomb = FindDefinitionPrefab(DebugItemSelection.BobombHold);
        BlueShell = FindDefinitionPrefab(DebugItemSelection.BlueShell);

        // optional: directly assign single-item references when debugSelection matches
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private GameObject FindDefinitionPrefab(DebugItemSelection debugSelection)
    {
        if (itemDefinitions == null)
        {
            return null;
        }

        for (int i = 0; i < itemDefinitions.Length; i++)
        {
            var def = itemDefinitions[i];
            if (def == null)
            {
                continue;
            }

            if (def.debugSelection == debugSelection)
            {
                return def.handPrefab != null ? def.handPrefab : def.prefab;
            }
        }

        return null;
    }

    [ContextMenu("Clear Legacy Item Arrays")]
    public void ClearLegacyItemArrays()
    {
        items_possible = System.Array.Empty<Sprite>();
        item_gameobjects = System.Array.Empty<GameObject>();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // Update is called once per frame
    void Update()
    {

        wayPointSystemCurrent();

        bool useItemPressedThisFrame = UseItemTriggered;
        bool useItemHeldNow = UseItemHeld;
        bool useItemReleasedThisFrame = useItemCancelThisFrame || UseItemReleased || (useItemHeldLastFrame && !useItemHeldNow);
        useItemCancelThisFrame = false;
 
        if (debugSettings.selectedItem != lastDebugSelectedItem)
        {
            lastDebugSelectedItem = debugSettings.selectedItem;
            if (debugSettings.selectedItem != DebugItemSelection.None)
            {
                TriggerDebugItem(debugSettings.selectedItem);
            }
        }
 
        if (bobombDebugLogging)
        {
            LogBobombDebug($"Inputs: AimBackwardHeld={AimBackwardHeld}, UseItemHeld={useItemHeldNow}, UseItemTriggered={UseItemTriggered}, UseItemReleased={useItemReleasedThisFrame}, TrailingActive={bobombTrailingActive}");
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


            if (UseItemTriggered && item_decided && !player_script.HitByBanana_ && !player_script.HitByShell_) //if item array order changes, change the indexes of utility methods and these if statements
            {
                if (current_Item.Equals("GreenShell")) //.Equals, not ==
                {
                    if (!AimBackwardHeld) // Hold to trail when stick neutral/up
                    {
                        StartTrailingItemIfNeeded(0);
                    }
                }
                else if (current_Item.Equals("TripleGreenShells") && tripleItemCount > 0)
                {

                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Driver.SetTrigger("ThrowForward");
                        item_gameobjects[2].SetActive(true);
                        StartCoroutine(spawnShell(shellSpawnPos, 1));
                        tripleItemCount--;
                    }
                    else // Backward (holding brake)
                    {
                        player_script.Driver.SetTrigger("ThrowBackward");
                        item_gameobjects[2].SetActive(true);
                        StartCoroutine(spawnShell(backshellPos, -1));
                        tripleItemCount--;
                        if (!StarPowerUp)
                            player_script.current_face_material = player_script.faces[1]; //look left
                    }

                    item_gameobjects[item_index].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the 3 shells. Index is valid as we subtracted 1 before

                    if (tripleItemCount < 1) //if you used up all of triple shells, reset everything
                    {
                        current_Item = "";
                        item_gameobjects[item_index].SetActive(false);
                        item_gameobjects[item_index].transform.GetChild(0).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(1).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(2).gameObject.SetActive(true);
                        used_Item_Done();
                    }
                } //THIS IS FOR TRIPLE GREEEN SHELLS
                else if (current_Item.Equals("RedShell"))
                {
                    if (!AimBackwardHeld) // Hold to trail when stick neutral/up
                    {
                        StartTrailingItemIfNeeded(1);
                    }
                }
                else if (current_Item.Equals("TripleRedShells") && tripleItemCount > 0)
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Driver.SetTrigger("ThrowForward");
                        item_gameobjects[4].SetActive(true);
                        StartCoroutine(spawnRedShell(shellSpawnPos, 1));
                        tripleItemCount--;
                    }
                    else // Backward (holding brake)
                    {
                        player_script.Driver.SetTrigger("ThrowBackward");
                        item_gameobjects[4].SetActive(true);
                        StartCoroutine(spawnRedShell(backshellPos, -1));
                        tripleItemCount--;
                        if (!StarPowerUp)
                            player_script.current_face_material = player_script.faces[1]; //look left
                    }
                    item_gameobjects[item_index].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the 3 shells. Index is valid as we subtracted 1 before

                    if (tripleItemCount < 1) //if you used up all of triple shells, reset everything
                    {
                        current_Item = "";
                        item_gameobjects[item_index].SetActive(false);
                        item_gameobjects[item_index].transform.GetChild(0).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(1).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(2).gameObject.SetActive(true);
                        used_Item_Done();
                    }
                }
                else if (current_Item.Equals("Mushroom"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Boost_time = 2f;
                        for (int i = 0; i < player_script.BoostBurstPS.transform.childCount; i++) //boost burst
                        {
                            player_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                        }
                        if (playersounds.Check_if_playing())
                        {
                            playersounds.Mario_Boost_Sounds[playersounds.sound_count].Play();
                            playersounds.sound_count++;
                        }
                        item_gameobjects[item_index].SetActive(false);
                        current_Item = ""; //1 use only
                        used_Item_Done();
                    }
                }
                else if (current_Item.Equals("TripleMushroom") && tripleItemCount > 0)
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Boost_time = 2.5f;
                        tripleItemCount--;
                        for (int i = 0; i < player_script.BoostBurstPS.transform.childCount; i++)
                        {
                            player_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                        }
                        item_gameobjects[item_index].transform.GetChild(tripleItemCount).gameObject.SetActive(false);
                        if (playersounds.Check_if_playing())
                        {
                            playersounds.Mario_Boost_Sounds[playersounds.sound_count].Play();
                            playersounds.sound_count++;
                        }
                        if (tripleItemCount < 1) //if you used up all of triple mushrooms, reset everything
                        {
                            current_Item = "";
                            item_gameobjects[item_index].SetActive(false);
                            item_gameobjects[item_index].transform.GetChild(0).gameObject.SetActive(true);
                            item_gameobjects[item_index].transform.GetChild(1).gameObject.SetActive(true);
                            item_gameobjects[item_index].transform.GetChild(2).gameObject.SetActive(true);
                            used_Item_Done();
                        }
                    }
                }
                else if (current_Item.Equals("Banana"))
                {
                    if (!AimBackwardHeld) // Hold to trail when stick neutral/up
                    {
                        StartTrailingItemIfNeeded(2);
                    }
                }
                else if (current_Item.Equals("TripleBananas"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Driver.SetTrigger("ThrowForward");
                        StartCoroutine(spawnBanana(1));
                    }
                    else // Backward (holding brake)
                    {
                        StartCoroutine(spawnBanana(-1));
                        
                        player_script.Driver.SetTrigger("ThrowBackward");
                        if (!StarPowerUp)
                            player_script.current_face_material = player_script.faces[1]; //look left
                    }
                    tripleItemCount--;
                    item_gameobjects[item_index].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the 3 shells. Index is valid as we subtracted 1 before


                    if (tripleItemCount < 1) //if you used up all of triple shells, reset everything
                    {
                        current_Item = "";
                        item_gameobjects[item_index].SetActive(false);
                        item_gameobjects[item_index].transform.GetChild(0).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(1).gameObject.SetActive(true);
                        item_gameobjects[item_index].transform.GetChild(2).gameObject.SetActive(true);
                        used_Item_Done();
                    }
                }
                else if (current_Item.Equals("GoldenMushroom"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        startMushroomTimer = true;
                        player_script.Boost_time = 2f;
                        for (int i = 0; i < player_script.BoostBurstPS.transform.childCount; i++) //boost burst
                        {
                            player_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                        }
                        if (playersounds.Check_if_playing())
                        {
                            playersounds.Mario_Boost_Sounds[playersounds.sound_count].Play();
                            playersounds.sound_count++;
                        }
                        if (GoldenMushroomTimer < 0)
                        {
                            item_gameobjects[item_index].SetActive(false);
                            current_Item = ""; 
                            used_Item_Done();
                                startMushroomTimer = false;
                        }
                    }

                    
                }
                else if (current_Item.Equals("Coin"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        StartCoroutine(UseCoin());
                            current_Item = ""; //1 use only
                            used_Item_Done();
                        }

                    
                }
                else if (current_Item.Equals("ItemStar"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        current_Item = ""; //1 use only
                        used_Item_Done();
                        StartCoroutine(UseStar());

                    }

                }
                else if (current_Item.Equals("Bullet"))
                {
                    if (!AimBackwardHeld && !player_script.JUMP_PANEL) // Bullet only works forward
                    {
                            if (!player_script.antiGravity)
                            {
                                current_Item = "";
                                StartCoroutine(UseBullet());
                            }
                            else
                            {
                                if (canUseBulletAntigravity)
                                {
                                    current_Item = "";
                                    StartCoroutine(UseBullet());
                                }
                            }
                       
                    }
                }
                else if (current_Item.Equals("Bobomb-Hold"))
                {
                    if (!AimBackwardHeld && UseItemTriggered && !bobombTrailingActive) // Forward quick tap
                    {
                        player_script.Driver.SetTrigger("ThrowForward");
                        StartCoroutine(useBobomb(1));
                        used_Item_Done();
                        current_Item = "";
                    }
                    else
                    {
                        if (!bobombTrailingActive && AimBackwardHeld && useItemHeldNow)
                        {
                            LogBobombDebug($"Attempting StartBobombTrailing: AimBackwardHeld={AimBackwardHeld}, UseItemHeldNow={useItemHeldNow}, UseItemTriggered={UseItemTriggered}, useItemHeldLastFrame={useItemHeldLastFrame}");
                            StartBobombTrailing();
                        }

                        if (bobombTrailingActive && useItemReleasedThisFrame)
                        {
                            LogBobombDebug($"Detected release input while trailing: AimBackwardHeld={AimBackwardHeld}, UseItemHeldNow={useItemHeldNow}, useItemReleasedThisFrame={useItemReleasedThisFrame}");
                            ReleaseBobombTrailing();
                        }
                    }
                }
                else if (current_Item.Equals("BlueShell"))
                {
                    if (!AimBackwardHeld) // Forward
                    {
                        player_script.Driver.SetTrigger("ThrowForward");
                        StartCoroutine(useBlueShell());
                        used_Item_Done();
                        current_Item = "";
                    }
                }
                else
                {
                    used_Item_Done();
                    for(int i = 0; i < item_gameobjects.Length; i++)
                    {
                        item_gameobjects[i].SetActive(false);
                    }
                } //for now, since we only have green shells working, everything else just turns off when you use the item
            }

            if (useItemReleasedThisFrame && item_decided && !player_script.HitByBanana_ && !player_script.HitByShell_)
            {
                if (current_Item.Equals("GreenShell"))
                {
                    HandleGreenShellRelease(AimBackwardHeld);
                }
                else if (current_Item.Equals("RedShell"))
                {
                    HandleRedShellRelease(AimBackwardHeld);
                }
                else if (current_Item.Equals("Banana"))
                {
                    HandleBananaRelease(AimBackwardHeld);
                }
            }
        }

        useItemHeldLastFrame = useItemHeldNow;
    }

    private void StartTrailingItemIfNeeded(int trailingIndex)
    {
        if (trailingItems == null || trailingIndex < 0 || trailingIndex >= trailingItems.Length)
        {
            return;
        }

        GameObject trailing = trailingItems[trailingIndex];
        if (trailing == null)
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

        if (item_gameobjects != null && item_index >= 0 && item_index < item_gameobjects.Length)
        {
            GameObject heldItem = item_gameobjects[item_index];
            if (heldItem != null)
            {
                heldItem.SetActive(false);
            }
        }
    }

    private void HandleGreenShellRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("GreenShell"))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            CleanupTrailingItem();
            ActivateItemGameObject(2);
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
            ActivateItemGameObject(2);
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowForward");
            }
            StartCoroutine(spawnShell(shellSpawnPos, 1));
        }

        current_Item = "";
        used_Item_Done();
    }

    private void HandleRedShellRelease(bool aimBackwardHeld)
    {
        if (!current_Item.Equals("RedShell"))
        {
            return;
        }

        if (aimBackwardHeld)
        {
            CleanupTrailingItem();
            ActivateItemGameObject(4);
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
            ActivateItemGameObject(4);
            if (player_script != null && player_script.Driver != null)
            {
                player_script.Driver.SetTrigger("ThrowForward");
            }
            StartCoroutine(spawnRedShell(shellSpawnPos, 1));
        }

        current_Item = "";
        used_Item_Done();
    }

    private void HandleBananaRelease(bool aimBackwardHeld)
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

    private void ActivateItemGameObject(int index)
    {
        if (item_gameobjects == null || index < 0 || index >= item_gameobjects.Length)
        {
            return;
        }

        GameObject target = item_gameobjects[index];
        if (target != null)
        {
            target.SetActive(true);
        }
    }


    IEnumerator Item_Select()
    {
        int resolvedIndex = GetComponent<ItemDistributionManager>().getItemNumber();
        if (debugForcedItemIndex >= 0)
        {
            resolvedIndex = debugForcedItemIndex;
            debugForcedItemIndex = -1;
        }

        //random or forced index
        item_index = resolvedIndex;

        Sprite spinningSprite = null;
        string spinningSource = "definition";
        if (itemDefinitions != null && item_index >= 0 && item_index < itemDefinitions.Length)
        {
            spinningSprite = itemDefinitions[item_index]?.icon;
        }

        if (spinningSprite == null && items_possible != null && item_index >= 0 && item_index < items_possible.Length)
        {
            spinningSprite = items_possible[item_index];
            spinningSource = "legacy";
        }

        Debug.Log($"[ItemManager] Item_Select -> index {item_index} spriteSource {spinningSource} spriteName {(spinningSprite != null ? spinningSprite.name : "null")}");

        if (your_item != null)
        {
            your_item.sprite = spinningSprite;
        }

        ItemUI.GetComponent<Animator>().SetBool("StartSelecting", true);
        ItemUI.transform.GetChild(0).GetChild(0).GetComponent<Animator>().SetBool("Scroll", true);
        
        // Minimum roulette time before player can stop it
        float minimumRouletteTime = 1.5f;
        float maxRouletteTime = 4f;
        float elapsedTime = 0f;
        
        // Wait for minimum time, then allow early stopping
        while (elapsedTime < maxRouletteTime)
        {
            elapsedTime += Time.deltaTime;
            
            // Check if player pressed item button after minimum time
            if (elapsedTime >= minimumRouletteTime && UseItemTriggered)
            {
                break; // Stop the roulette early
            }
            
            yield return null;
        }
        
        item_gameobjects[item_index].SetActive(true); //show the gameobject
        if (item_gameobjects[item_index].tag != "Non-Hold-Item")
        {
            player_script.Driver.SetBool("hasItem", true);
            player_script.has_item_hold = true;
            tripleItemCount = 0;

            if(item_gameobjects[item_index].name == "GoldenMushroom")
            {
                GoldenMushroomTimer = 10f;
            }
        }
        else
        {
            tripleItemCount = 3; //triple item
        }

        current_Item = item_gameobjects[item_index].name;

        PlaySelectsound.Stop();
        Selected.Play();
        item_decided = true;
        activeRoulette = null;
    }

    //SPAWN FUNCTIONS
    IEnumerator spawnShell(Transform position, int direction) //spawns a green shell when shot
    {

        yield return new WaitForSeconds(0.15f);
        GameObject clone = Instantiate(shell, position.position, position.rotation);
        clone.GetComponent<GreenShell>().who_threw_shell = gameObject.name;

        if (direction == 1) //backwards or forwards -1 and 1 respectively
        {
            clone.GetComponent<GreenShell>().myVelocity = transform.forward.normalized;
            clone.GetComponent<GreenShell>().velocityMagOriginal = 6000;
            clone.GetComponent<GreenShell>().AntiGravity = player_script.antiGravity;

            clone.GetComponent<GreenShell>().lifetime = 0;

            yield return new WaitForSeconds(0.25f);
            item_gameobjects[2].SetActive(false); //hand shell

        }
        
        if (direction == -1)
        {
            clone.GetComponent<GreenShell>().myVelocity = -transform.forward.normalized;
            clone.GetComponent<GreenShell>().velocityMagOriginal = 3500;
            clone.GetComponent<GreenShell>().AntiGravity = player_script.antiGravity;


            
            yield return new WaitForSeconds(0.25f);
            item_gameobjects[2].SetActive(false); //hand shell
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

        if(direction == 1)
        {
            yield return new WaitForSeconds(0.15f);
            GameObject clone = Instantiate(redShell, position.position, position.rotation);
            clone.GetComponent<RedShell>().who_threw_shell = gameObject.name;
            clone.GetComponent<RedShell>().AntiGravity = player_script.antiGravity;


            clone.SetActive(true);
            clone.GetComponent<RedShell>().current_node = currentWayPoint; //currentWayPoint
            yield return new WaitForSeconds(0.25f);
            item_gameobjects[4].SetActive(false); //hand shell
        }
        else if(direction == -1)
        {
            yield return new WaitForSeconds(0.15f);
            GameObject clone = Instantiate(redShell, position.position, position.rotation);
            clone.SetActive(false);
            clone.GetComponent<RedShell>().who_threw_shell = gameObject.name;

            clone.GetComponent<RedShell>().enabled = false;
            clone.AddComponent<GreenShell>();
            clone.GetComponent<GreenShell>().lifetime = 0;

            clone.SetActive(true);

            clone.GetComponent<GreenShell>().myVelocity = -transform.forward.normalized;
            clone.GetComponent<GreenShell>().velocityMagOriginal = 3500;
            clone.GetComponent<GreenShell>().AntiGravity = player_script.antiGravity;

            clone.GetComponent<GreenShell>().who_threw_shell = gameObject.name;


            yield return new WaitForSeconds(0.25f);
            item_gameobjects[4].SetActive(false); //hand shell
            for(int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                {
                    player_script.SpecialFace = true;
                    player_script.current_face_material = player_script.faces[1]; //make sure it is not changed, by repeating in for loop
                }
                yield return new WaitForSeconds(0.01f);
            }
            if (!StarPowerUp)
            {
                player_script.SpecialFace = true;
                player_script.current_face_material = player_script.faces[2]; //blink
            }
            yield return new WaitForSeconds(0.1f);
            if (!StarPowerUp)
            {
                player_script.SpecialFace = false;
                player_script.current_face_material = player_script.faces[0];//normal
            }
        }
        

    }
    IEnumerator useBobomb(int direction)
    {
        if(direction == 1)
        {
            yield return new WaitForSeconds(0.1f);
            item_gameobjects[item_index].SetActive(false);

            GameObject clone = Instantiate(bobomb, BananaSpawnPos.position, BananaSpawnPos.rotation);
            clone.SetActive(true);
            var cloneBomb = clone.GetComponent<Bobomb>();
            ApplyBobombDebugSettings(cloneBomb);
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
            item_gameobjects[item_index].SetActive(false);

            GameObject clone = Instantiate(bobomb, backshellPos.position, BananaSpawnPos.rotation);
            clone.SetActive(true);
            var cloneBomb = clone.GetComponent<Bobomb>();
            ApplyBobombDebugSettings(cloneBomb);
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
            item_gameobjects[1].SetActive(false);
            clone = Instantiate(banana, BananaSpawnPos.position, BananaSpawnPos.rotation);
            clone.GetComponent<Banana>().Banana_thrown(transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity).z * 200);
            clone.GetComponent<Banana>().whoThrewBanana = gameObject.name;
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
            clone = Instantiate(banana, backshellPos.position, BananaSpawnPos.rotation);
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
            item_gameobjects[1].SetActive(false);
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
        GameObject clone = Instantiate(coin, coinSpawnPos.position, coinSpawnPos.rotation);
        clone.transform.SetParent(transform);
        item_gameobjects[item_index].SetActive(false);
        GetComponent<ScoreCount>().COINCOUNT+=2;

        yield return new WaitForSeconds(0.3f);
        playersounds.effectSounds[9].Play();
        coinSparkle.Play();

    }
    IEnumerator UseStar()
    {
        float volume = GameObject.FindGameObjectWithTag("CourseMusic").GetComponent<AudioSource>().volume;
        float volume2 = GameObject.FindGameObjectWithTag("CourseMusic").transform.parent.GetComponent<AudioSource>().volume;

        item_gameobjects[item_index].SetActive(false);
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
        item_gameobjects[item_index].SetActive(false);
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
        GameObject clone = Instantiate(BlueShell, shellSpawnPos.position, shellSpawnPos.transform.rotation);
        clone.SetActive(true);
        clone.GetComponent<BlueShell>().current_node = currentWayPoint;
        clone.GetComponent<BlueShell>().AntiGravity = player_script.antiGravity;
        item_gameobjects[item_index].SetActive(false); //hand shell
        clone.GetComponent<BlueShell>().who_threw_shell = gameObject.name;
    }



    public void used_Item_Done() //resets the ui and bools
    {
        // For testing we keep the UI visible and skip the auto-refill.
        
        player_script.hasitem = false;
        player_script.has_item_hold = false;
        item_decided = false;
        start_select = false;
        if (ItemUI != null)
        {
            var uiAnimator = ItemUI.GetComponent<Animator>();
            if (uiAnimator != null)
            {
                uiAnimator.SetBool("StartSelecting", false);
            }

            if (ItemUI.transform.childCount > 0)
            {
                Transform child = ItemUI.transform.GetChild(0);
                if (child != null && child.childCount > 0)
                {
                    var scrollAnimator = child.GetChild(0).GetComponent<Animator>();
                    if (scrollAnimator != null)
                    {
                        scrollAnimator.SetBool("Scroll", false);
                    }
                }
            }
        }

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
        if (item_gameobjects == null)
        {
            return -1;
        }

        string targetName = GetDebugItemName(selection);
        if (string.IsNullOrEmpty(targetName))
        {
            return -1;
        }

        string sanitizedTarget = SanitizeName(targetName);
        int fallbackIndex = -1;

        for (int i = 0; i < item_gameobjects.Length; i++)
        {
            var obj = item_gameobjects[i];
            if (obj == null)
            {
                continue;
            }

            string objectName = obj.name;
            if (string.Equals(objectName, targetName, System.StringComparison.Ordinal))
            {
                return i;
            }

            if (fallbackIndex == -1)
            {
                string sanitizedObject = SanitizeName(objectName);
                if (!string.IsNullOrEmpty(sanitizedObject) && !string.IsNullOrEmpty(sanitizedTarget))
                {
                    if (sanitizedObject == sanitizedTarget ||
                        sanitizedObject.Contains(sanitizedTarget) ||
                        sanitizedTarget.Contains(sanitizedObject))
                    {
                        fallbackIndex = i;
                    }
                }
            }
        }

        if (fallbackIndex != -1)
        {
            return fallbackIndex;
        }

        int keywordIndex = ResolveDebugItemIndexByKeywords(selection);
        if (keywordIndex != -1)
        {
            return keywordIndex;
        }

        int referenceIndex = ResolveDebugItemIndexFromReference(selection);
        if (referenceIndex != -1)
        {
            return referenceIndex;
        }

        LogBobombDebug($"Debug item '{targetName}' could not be matched to item array, even after keyword/reference fallbacks.");
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

    private int ResolveDebugItemIndexFromReference(DebugItemSelection itemSelection)
    {
        switch (itemSelection)
        {
            case DebugItemSelection.GreenShell:
            case DebugItemSelection.TripleGreenShells:
                return FindIndexByReference(shell);
            case DebugItemSelection.RedShell:
            case DebugItemSelection.TripleRedShells:
                return FindIndexByReference(redShell);
            case DebugItemSelection.Banana:
            case DebugItemSelection.TripleBananas:
                return FindIndexByReference(banana);
            case DebugItemSelection.Coin:
                return FindIndexByReference(coin);
            case DebugItemSelection.BobombHold:
                return FindIndexByReference(bobomb);
            case DebugItemSelection.BlueShell:
                return FindIndexByReference(BlueShell);
            default:
                return -1;
        }
    }

    private int FindIndexByReference(GameObject reference)
    {
        if (reference == null || item_gameobjects == null)
        {
            return -1;
        }

        for (int i = 0; i < item_gameobjects.Length; i++)
        {
            if (item_gameobjects[i] == reference)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindIndexByKeywords(params string[] keywords)
    {
        if (keywords == null || keywords.Length == 0 || item_gameobjects == null)
        {
            return -1;
        }

        string[] sanitizedKeywords = new string[keywords.Length];
        for (int k = 0; k < keywords.Length; k++)
        {
            sanitizedKeywords[k] = SanitizeName(keywords[k]);
        }

        for (int i = 0; i < item_gameobjects.Length; i++)
        {
            var obj = item_gameobjects[i];
            if (obj == null)
            {
                continue;
            }

            string sanitizedObject = SanitizeName(obj.name);
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
        if (items_possible == null || items_possible.Length == 0)
        {
            return null;
        }

        string canonical = itemName ?? string.Empty;
        string sanitizedName = SanitizeName(canonical);
        if (string.IsNullOrEmpty(sanitizedName))
        {
            return null;
        }

        for (int i = 0; i < items_possible.Length; i++)
        {
            var sprite = items_possible[i];
            if (sprite == null)
            {
                continue;
            }

            string sanitizedSprite = SanitizeName(sprite.name);
            if (sanitizedSprite == sanitizedName)
            {
                return sprite;
            }

            if (!string.IsNullOrEmpty(sanitizedSprite) &&
                (sanitizedSprite.Contains(sanitizedName) || sanitizedName.Contains(sanitizedSprite)))
            {
                return sprite;
            }
        }
 
        return null;
    }

    private void ApplyBobombDebugSettings(Bobomb bombScript)
    {
        if (bombScript == null)
        {
            return;
        }

        if (debugSettings.bobomb.overrideThrowSettings)
        {
            bombScript.ApplyDebugThrowSettings(
                debugSettings.bobomb.throwForceMultiplier,
                debugSettings.bobomb.matchArcAngle,
                debugSettings.bobomb.desiredArcAngleDegrees,
                debugSettings.bobomb.autoCalibrate);
        }
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
            LogBobombDebug($"Debug selection {selection} could not be matched to an item index.");
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

    private void StartBobombTrailing()
    {
        LogBobombDebug($"StartBobombTrailing entered: bobombTrailingActive(before)={bobombTrailingActive}, currentItem={current_Item}, backshellPos={backshellPos.position}");

        bobombTrailingActive = true;
        player_script.Driver.SetBool("hasItem", false);
        item_gameobjects[item_index].SetActive(false);

        activeTrailingBobomb = Instantiate(bobomb, backshellPos.position, backshellPos.rotation, backshellPos);
        LogBobombDebug($"Instantiated trailing Bobomb '{activeTrailingBobomb?.name}' at {backshellPos.position}");
        var bombScript = activeTrailingBobomb.GetComponent<Bobomb>();
        if (bombScript != null)
        {
            bombScript.whoThrewBomb = gameObject.name;
            ApplyBobombDebugSettings(bombScript);
            bombScript.BeginHeld(bobombHeldFuseDuration, OnBobombHeldExplosion);
            LogBobombDebug($"BeginHeld invoked on Bobomb with fuse={bobombHeldFuseDuration}s");
        }
        else
        {
            LogBobombDebug("Warning: trailing Bobomb missing Bobomb component");
        }

        CurrentTrailingItem = activeTrailingBobomb;
        LogBobombDebug("StartBobombTrailing completed");
    }

    private void ReleaseBobombTrailing()
    {
        LogBobombDebug($"ReleaseBobombTrailing entered: bobombTrailingActive(before)={bobombTrailingActive}, activeBomb={(activeTrailingBobomb != null ? activeTrailingBobomb.name : "null")}, kartForward={transform.forward}");

        bobombTrailingActive = false;

        if (activeTrailingBobomb != null)
        {
            var bombScript = activeTrailingBobomb.GetComponent<Bobomb>();
            if (bombScript != null)
            {
                bombScript.ReleaseHeldAsMine();
                LogBobombDebug("ReleaseHeldAsMine called on trailing Bobomb");
            }
            ReleaseBobombAsMine(activeTrailingBobomb, -transform.forward);
            activeTrailingBobomb = null;
        }

        CurrentTrailingItem = null;
        player_script.Driver.SetTrigger("ThrowBackward");
        used_Item_Done();
        current_Item = "";
        LogBobombDebug("ReleaseBobombTrailing finished cleanup");
    }

    private void OnBobombHeldExplosion()
    {
        LogBobombDebug("OnBobombHeldExplosion invoked (held fuse expired)");
        bobombTrailingActive = false;

        if (activeTrailingBobomb != null)
        {
            activeTrailingBobomb.transform.SetParent(null, true);
            activeTrailingBobomb = null;
        }

        CurrentTrailingItem = null;
        used_Item_Done();
        current_Item = "";
        LogBobombDebug("OnBobombHeldExplosion cleanup finished");
    }

    private void ReleaseBobombAsMine(GameObject bombObject, Vector3 forwardDirection)
    {
        if (bombObject == null)
        {
            LogBobombDebug("ReleaseBobombAsMine called with null bombObject");
            return;
        }

        bombObject.transform.SetParent(null, true);
        bombObject.SetActive(true);
        LogBobombDebug($"Bobomb released as mine at position={bombObject.transform.position}, forwardDirection={forwardDirection}");

        var rb = bombObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(forwardDirection.normalized * 15f, ForceMode.VelocityChange);
            LogBobombDebug($"Applied release impulse. Dir={forwardDirection.normalized}, magnitude=15, resultingVelocity={rb.velocity}");
        }

        var bombScript = bombObject.GetComponent<Bobomb>();
        if (bombScript != null)
        {
            bombScript.enabled = true;
            bombScript.whoThrewBomb = gameObject.name;
            LogBobombDebug("Bobomb script re-enabled and whoThrewBomb set");
        }

        var audio = bombObject.GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
            LogBobombDebug("Played Bobomb audio on release");
        }
    }
}
