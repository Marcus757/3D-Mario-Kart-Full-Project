using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpponentItemManager : MonoBehaviour
{
    private ComputerDriver ai_script;
    private LapCounter lap_counter;
    private ComputerDriverSounds comp_sounds;



    [Header("Held Item Settings")]
    [SerializeField]
    private Transform heldItemParent;
    private GameObject[] ItemsPossible;
    private string[] itemNames;
    private readonly List<GameObject> runtimeHeldVisuals = new List<GameObject>();
    private ItemCatalog catalog;
    private bool catalogNotFoundLogged;
    private static readonly HashSet<string> tripleItemNames = new HashSet<string>
    {
        "TripleGreenShells",
        "TripleRedShells",
        "TripleBananas",
        "TripleMushroom"
    };

    private static readonly Dictionary<string, string> tripleToSingleMap = new Dictionary<string, string>
    {
        { "TripleGreenShells", "GreenShell" },
        { "TripleRedShells", "RedShell" },
        { "TripleBananas", "Banana" },
        { "TripleMushroom", "Mushroom" }
    };

    [HideInInspector]
    public float item_select_time = 0;
    bool already_has_item = false;

    private string current_item = "";
    [HideInInspector]
    public int itemIndex;
    private float timeHavingItem = 0;
    private float tripleitemActionTime = 0;
    private float goldenMushroomTimer = 0;

    [HideInInspector]
    public bool HitByShell_ = false;
    public bool HitByBanana_ = false;
    public bool invincible = false;
    public float invincibleTime = 0;

    public Transform shellPos;
    public Transform shellposBack;
    public Transform bananaPos;
    public Transform coinPos;

    public LayerMask mask;
    [HideInInspector]
    public bool isBullet;

    //we need to keep track of every self-moving item's waypoints because since we want the item to follow its waypoints from where the player shoots the shell on the track, we have to identify the current waypoint for that shell, or bullet bill, etc
    [HideInInspector]
    public int currentWayPoint = 0;
    [Header("ITEM WAYPOINT SYSTEM")]
    public Transform path;
    public Transform path2;

    [Header("BulletStuff")]
    public GameObject bulletPlayer;
    public GameObject kart;

    [Header("Star Stuff")]
    public Material[] normalMaterials;
    public Renderer[] playerRenderers;
    public Material starMat;
    public GameObject starPS;
    [HideInInspector]
    public bool StarPowerUp;

    private Transform player;
    private bool closeToPlayer = false;

    [HideInInspector]
    public int tripleItemCount = 0;

    private RACE_MANAGER rm;



    // Start is called before the first frame update
    void Start()
    {
        ai_script = GetComponent<ComputerDriver>();
        lap_counter = GetComponent<LapCounter>();
        comp_sounds = GetComponent<ComputerDriverSounds>();

        player = GameObject.FindGameObjectWithTag("Player").transform;
        rm = GameObject.Find("RaceManager").GetComponent<RACE_MANAGER>();

        BuildItemCache();
    }

    // Update is called once per frame
    void Update()
    {
        invincibleTime -= Time.deltaTime;
        if(invincibleTime > 0)
        {
            invincible = true;
        }
        else
        {
            invincible = false;
            HitByShell_ = false;
            invincible = false;
            HitByBanana_ = false;
        }

        //item stuff
        if (already_has_item && !StarPowerUp)
        {
            usingItem();
        }

        if(StarPowerUp && !closeToPlayer && Vector3.Distance(transform.position, player.position) < 100)
        {
            closeToPlayer = true;
            StartCoroutine(rm.warningStar(transform));
        }

        if(isBullet && !closeToPlayer && Vector3.Distance(transform.position, player.position) < 100)
        {
            closeToPlayer = true;
            StartCoroutine(rm.warningBullet(transform));
        }
    }

    private void BuildItemCache()
    {
        ClearRuntimeHeldVisuals();

        if (!TryEnsureCatalog())
        {
            ItemsPossible = System.Array.Empty<GameObject>();
            itemNames = System.Array.Empty<string>();
            return;
        }

        int count = catalog.DefinitionCount;
        ItemsPossible = count > 0 ? new GameObject[count] : System.Array.Empty<GameObject>();
        itemNames = count > 0 ? new string[count] : System.Array.Empty<string>();

        Transform parent = heldItemParent != null ? heldItemParent : transform;

        for (int i = 0; i < count; i++)
        {
            ItemDefinition definition = catalog.GetDefinition(i);
            string canonicalName = catalog.GetCanonicalName(definition);
            itemNames[i] = canonicalName;

            GameObject visual = catalog.InstantiateHeldVisual(i, parent);
            if (visual != null)
            {
                AssignOrbitingOwner(visual);
                visual.SetActive(false);
                ItemsPossible[i] = visual;
                runtimeHeldVisuals.Add(visual);
            }
        }
    }

    private void ClearRuntimeHeldVisuals()
    {
        if (runtimeHeldVisuals.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeHeldVisuals.Count; i++)
        {
            GameObject visual = runtimeHeldVisuals[i];
            if (visual == null)
            {
                continue;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(visual);
            }
            else
#endif
            {
                Destroy(visual);
            }
        }

        runtimeHeldVisuals.Clear();
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
            Debug.LogError("[OpponentItemManager] ItemCatalog could not be found in the scene.", this);
            catalogNotFoundLogged = true;
        }

        return false;
    }

    private GameObject GetHeldVisual(int index)
    {
        if (ItemsPossible == null || index < 0 || index >= ItemsPossible.Length)
        {
            return null;
        }

        return ItemsPossible[index];
    }

    private void SetHeldVisualActive(int index, bool active)
    {
        var visual = GetHeldVisual(index);
        if (visual != null)
        {
            visual.SetActive(active);
        }
    }

    private bool HeldVisualHasTag(int index, string tag)
    {
        var visual = GetHeldVisual(index);
        return visual != null && visual.CompareTag(tag);
    }

    private string GetItemName(int index)
    {
        if (itemNames == null || index < 0 || index >= itemNames.Length)
        {
            return null;
        }

        return itemNames[index];
    }

    private static bool IsTripleItem(string itemName)
    {
        return !string.IsNullOrEmpty(itemName) && tripleItemNames.Contains(itemName);
    }

    private void ResetTripleChildren(int index)
    {
        var visual = GetHeldVisual(index);
        if (visual == null)
        {
            return;
        }

        for (int i = 0; i < visual.transform.childCount; i++)
        {
            visual.transform.GetChild(i).gameObject.SetActive(true);
        }
    }

    private static string NormalizeWorldItemName(string canonicalName)
    {
        if (string.IsNullOrEmpty(canonicalName))
        {
            return canonicalName;
        }

        if (tripleToSingleMap.TryGetValue(canonicalName, out string single))
        {
            return single;
        }

        switch (canonicalName)
        {
            case "Bobomb-Hold":
            case "Banana":
            case "GreenShell":
            case "RedShell":
            case "BlueShell":
            case "Coin":
            case "Mushroom":
            case "GoldenMushroom":
            case "ItemStar":
            case "Bullet":
                return canonicalName;
            case "ItemCoin":
                return "Coin";
            case "BulletBillPlayer":
                return "Bullet";
            default:
                return canonicalName;
        }
    }

    private GameObject GetWorldPrefab(string canonicalName)
    {
        if (!TryEnsureCatalog())
        {
            return null;
        }

        string lookup = NormalizeWorldItemName(canonicalName);
        GameObject prefab = catalog.GetWorldPrefab(lookup);
        if (prefab == null && lookup != canonicalName)
        {
            prefab = catalog.GetWorldPrefab(canonicalName);
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[OpponentItemManager] Missing world prefab for '{canonicalName}'.", this);
        }

        return prefab;
    }

    private GameObject InstantiateWorldItem(string canonicalName, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetWorldPrefab(canonicalName);
        if (prefab == null)
        {
            return null;
        }

        return Instantiate(prefab, position, rotation);
    }

    private void OnDestroy()
    {
        ClearRuntimeHeldVisuals();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Banana")//regular banana
        {
            if (collision.gameObject.GetComponent<Banana>().lifetime > 0.2f && !StarPowerUp)
            {
                hitByBanana();
                if (collision.gameObject.GetComponent<Banana>().whoThrewBanana == "Mario")
                {
                    GameObject.Find("Mario").GetComponent<Player>().Driver.SetTrigger("HitItem");
                    if (GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSounds>().Check_if_playing())
                        GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSounds>().effectSounds[18].Play();

                }
                Destroy(collision.gameObject);
            }
            else if (StarPowerUp)
            {
                Destroy(collision.gameObject);
            }
        }


    }
    private IEnumerator OnTriggerEnter(Collider other)
    {
        //item box
        if (other.gameObject.tag == "ItemBox" && item_select_time > 3)
        {
            item_select_time = 0;
            for (int i = 0; i < 5; i++)
            {
                other.transform.GetChild(0).GetChild(i).GetComponent<ParticleSystem>().Play();

            }

            //start hiding stuff
            other.gameObject.GetComponent<BoxCollider>().enabled = false;
            for (int i = 1; i < 3; i++)
            {
                other.transform.GetChild(2).GetChild(i).GetComponent<SkinnedMeshRenderer>().enabled = false; //box
                other.transform.GetChild(1).GetChild(1).GetComponent<SkinnedMeshRenderer>().enabled = false; //question mark
            }
            other.gameObject.GetComponent<Animator>().SetBool("Enlarge", false); //reset to start process

            //re-enable
            yield return new WaitForSeconds(1);
            for (int i = 1; i < 3; i++)
            {
                other.transform.GetChild(2).GetChild(i).GetComponent<SkinnedMeshRenderer>().enabled = true; //box
                other.transform.GetChild(1).GetChild(1).GetComponent<SkinnedMeshRenderer>().enabled = true; //question mark
            }
            other.gameObject.GetComponent<Animator>().SetBool("Enlarge", true);  //show the item box spawning with animation, even though it was already there
            other.gameObject.GetComponent<BoxCollider>().enabled = true;

            yield return new WaitForSeconds(2);
            if (!already_has_item && !RACE_MANAGER.RACE_COMPLETED)
                ItemSelect();


        }

        

        if(other.gameObject.tag == "Explosion" && !StarPowerUp)
        {
            hitByBanana();//effect as if a normal Banana hit the opponent

            if (other.gameObject.name != "Explosion Blue Shell")
            {
                var bombComponent = other.gameObject.GetComponent<Bobomb>();
                if (bombComponent != null && bombComponent.whoThrewBomb == "Player")
                {
                    GameObject.Find("Player").GetComponent<Player>().Driver.SetTrigger("HitItem");
                }
            }

        }
        if (path.GetChild(currentWayPoint) == other.transform || path2.GetChild(currentWayPoint) == other.transform)
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

    //selects item
    void ItemSelect()
    {
        int index = GetComponent<ItemDistributionManager>().getItemNumber();

        if (ItemsPossible == null || index < 0 || index >= ItemsPossible.Length)
        {
            BuildItemCache();
        }

        GameObject heldVisual = GetHeldVisual(index);
        if (heldVisual != null)
        {
            ResetTripleChildren(index);
            heldVisual.SetActive(true);
        }

        if (!HeldVisualHasTag(index, "Non-Hold-Item"))
        {
            ai_script.DriverAnim.SetBool("hasItem", true);
            tripleItemCount = 0;
        }
        else
        {
            tripleItemCount = 3;
        }
        already_has_item = true;
        string canonicalName = GetItemName(index);
        if (string.IsNullOrEmpty(canonicalName) && heldVisual != null)
        {
            canonicalName = heldVisual.name;
        }
        current_item = canonicalName ?? string.Empty;
        itemIndex = index;

        if(current_item == "GoldenMushroom")
        {
            goldenMushroomTimer = 10;
        }
    }

    //these methods control what happens when hit by an item
    public void hitByShell() //same method for red and green shells since they do the exact same effect when hitting the player
    {

        if (!invincible)
        {
            GetComponent<Minimap>().playerInMap.GetComponent<Animator>().SetTrigger("Spin");
            invincibleTime = 1.25f;
            transform.GetChild(0).GetChild(0).GetComponent<Animator>().SetTrigger("ShellHit");
            invincible = true;
            HitByShell_ = true;
        }
           

            
        

    }
    public void hitByBanana()
    {
        if (!invincible)
        {
            GetComponent<Minimap>().playerInMap.GetComponent<Animator>().SetTrigger("Spin");
            invincibleTime = 1.25f;
            transform.GetChild(0).GetChild(0).GetComponent<Animator>().SetTrigger("BananaHit");
            invincible = true;
            HitByBanana_ = true;
        }
    }


    void usingItem()
    {
        timeHavingItem += Time.deltaTime; //stores the time for how long the opponent has an item for
        tripleitemActionTime += Time.deltaTime; //will be used as a timer for when the next item in the triple should be used 
        //this raycast thing will be used for some items such as projectiles and bananas

        if (current_item == "GreenShell")
        {
            RaycastHit hit = new RaycastHit();

            //backwards Shot
            if (Physics.Raycast(transform.position, -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(10, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-10, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                )) && timeHavingItem > 5)
            {
                if (hit.collider.tag == "Opponent" || hit.collider.tag == "Player")
                {
                    StartCoroutine(useShell(-1, shellposBack));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowBack");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                }
            }
            else if (Physics.Raycast(transform.position, transform.forward, out hit, Mathf.Infinity, mask) //forward shot
               || (Physics.Raycast(transform.position, Quaternion.AngleAxis(10, transform.up) * transform.forward, out hit, Mathf.Infinity, mask)
               || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-10, transform.up) * transform.forward, out hit, Mathf.Infinity, mask)
               )))
            {
                if (hit.collider.tag == "Opponent" || hit.collider.tag == "Player")
                {
                    StartCoroutine(useShell(1, shellPos));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                }    
            }
            else if(timeHavingItem > 5)
            {
                if(lap_counter.Position != 1)
                {
                    StartCoroutine(useShell(1, shellPos));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                }
                else
                {
                    StartCoroutine(useShell(-1, shellposBack));
                    already_has_item = false;
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    ai_script.DriverAnim.SetTrigger("ThrowBack");

                }
            }
        }
        else if(current_item == "RedShell")
        {
            if(lap_counter.Position == 1)
            {
                RaycastHit hit = new RaycastHit();

                if (timeHavingItem > 10 || (Physics.Raycast(transform.position, -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                ))) && timeHavingItem > 5)
                {
                    if(hit.transform.tag == "Opponent" || hit.transform.tag == "Player")
                    {
                        StartCoroutine(useRedShell(-1, shellposBack));
                        already_has_item = false;
                        ai_script.DriverAnim.SetTrigger("ThrowBack");
                        ai_script.DriverAnim.SetBool("hasItem", false);
                        timeHavingItem = 0;
                    }        
                }
            }
            else
            {
                if(timeHavingItem > 5)
                {
                    StartCoroutine(useRedShell(1, shellPos));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    timeHavingItem = 0;
                }
            }
        }
        else if(current_item == "Banana")
        {
            RaycastHit hit = new RaycastHit();

            if ((Physics.Raycast(transform.position, -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                ))) && timeHavingItem > 2.5f)
            {
                if(hit.transform.tag == "Opponent" || hit.transform.tag == "Player")
                {
                    StartCoroutine(spawnBanana(-1));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowBack");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    timeHavingItem = 0;
                }
            }
            else if(timeHavingItem > 8)
            {
                if(ai_script.path.GetChild(ai_script.current_node).tag != "DriftLeft" && ai_script.path.GetChild(ai_script.current_node).tag != "DriftRight")
                {
                    StartCoroutine(spawnBanana(1));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    timeHavingItem = 0;
                }
                else
                {
                    int x = Random.Range(0, 2);
                    if(x == 0)
                    {
                        StartCoroutine(spawnBanana(-1));
                        already_has_item = false;
                        ai_script.DriverAnim.SetTrigger("ThrowBack");
                        ai_script.DriverAnim.SetBool("hasItem", false);
                        timeHavingItem = 0;
                    }
                    else
                    {
                        StartCoroutine(spawnBanana(1));
                        already_has_item = false;
                        ai_script.DriverAnim.SetBool("hasItem", false);
                        timeHavingItem = 0;
                    }
                }
            }

        }
        else if(current_item == "BlueShell")
        {
            if(timeHavingItem > 5 && lap_counter.Position != 1)
            {
                //player_script.Driver.SetTrigger("ThrowForward");
                StartCoroutine(useBlueShell());
                already_has_item = false;
                ai_script.DriverAnim.SetTrigger("ThrowForward");
                ai_script.DriverAnim.SetBool("hasItem", false);
                current_item = "";
                timeHavingItem = 0;
            }
        }
        else if(current_item == "Bobomb-Hold")
        {
            if(timeHavingItem > 5)
            {
                int x = Random.Range(0, 3);

                if(x == 0)
                {
                    //player_script.Driver.SetTrigger("ThrowForward");
                    StartCoroutine(useBobomb(1));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    current_item = "";
                    timeHavingItem = 0;
                }
                else
                {
                    StartCoroutine(useBobomb(-1));
                    already_has_item = false;
                    ai_script.DriverAnim.SetTrigger("ThrowBack");
                    ai_script.DriverAnim.SetBool("hasItem", false);
                    timeHavingItem = 0;
                }
            }
        }
        else if(current_item == "Mushroom")
        {
            if(!ai_script.driftright && !ai_script.driftleft && timeHavingItem > 5)
            {
                ai_script.boost_time = 2f;
                already_has_item = false;
                SetHeldVisualActive(itemIndex, false);
                ai_script.DriverAnim.SetBool("hasItem", false);
                current_item = "";
                timeHavingItem = 0;
                for (int i = 0; i < ai_script.BoostBurstPS.transform.childCount; i++) //boost burst
                {
                    ai_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                }
            } 
        }
        else if(current_item == "Bullet")
        {
            if(timeHavingItem > 5)
            {
                current_item = "";
                StartCoroutine(UseBullet());
            }
        }
        else if(current_item == "TripleMushroom")
        {
            if(tripleitemActionTime > 5) //i need a way for this to get called once every 5 seconds
            {
                if (!ai_script.driftright && !ai_script.driftleft && timeHavingItem > 5 && tripleItemCount > 0)
                {
                    ai_script.boost_time = 2f;
                    tripleItemCount--;
                    tripleitemActionTime = 0;
                    ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false);
                    for (int i = 0; i < ai_script.BoostBurstPS.transform.childCount; i++) //boost burst
                    {
                        ai_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                    }
                }
                if (tripleItemCount <= 0)
                {
                    SetHeldVisualActive(itemIndex, false);
                    ItemsPossible[itemIndex].transform.GetChild(0).gameObject.SetActive(true);
                    ItemsPossible[itemIndex].transform.GetChild(1).gameObject.SetActive(true);
                    ItemsPossible[itemIndex].transform.GetChild(2).gameObject.SetActive(true);
                    already_has_item = false;
                    current_item = "";
                    timeHavingItem = 0;
                    tripleitemActionTime = 0;

                }
            }

        }
        else if(current_item == "GoldenMushroom")
        {
            goldenMushroomTimer -= Time.deltaTime;
            if(tripleitemActionTime > 2)
            {
                tripleitemActionTime = 0;
                ai_script.boost_time = 2f;

                for (int i = 0; i < ai_script.BoostBurstPS.transform.childCount; i++) //boost burst
                {
                    ai_script.BoostBurstPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play(); //left and right included
                }
                /*
                if (playersounds.Check_if_playing())
                {
                    playersounds.Mario_Boost_Sounds[playersounds.sound_count].Play();
                    playersounds.sound_count++;
                }
                */
            }

            if (goldenMushroomTimer < 0)
            {
                    SetHeldVisualActive(itemIndex, false);
                current_item = "";
                timeHavingItem = 0;
                ai_script.DriverAnim.SetBool("hasItem", false);
                already_has_item = false;
            }
        }
        else if(current_item == "TripleBananas")
        {
            RaycastHit hit = new RaycastHit();

            if (tripleItemCount > 0 && tripleitemActionTime > 4)
            {
                if ((Physics.Raycast(transform.position, -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-5, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                ))) && timeHavingItem > 2.5f)
                {
                    if (hit.transform.tag == "Opponent" || hit.transform.tag == "Player")
                    {
                        StartCoroutine(spawnBanana(-1));
                        ai_script.DriverAnim.SetTrigger("ThrowBack");
                        tripleitemActionTime = 0;
                        tripleItemCount--;  
                        ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three bananas

                    }
                } //the code above is to throw it backwards based on raycast hit
                else if (tripleitemActionTime > 6) //if the time is taking too long to detect a player, shoot it randomly wherever
                {
                    if (ai_script.path.GetChild(ai_script.current_node).tag != "DriftLeft" && ai_script.path.GetChild(ai_script.current_node).tag != "DriftRight") //if driver is not drifting (not making a sharp turn), throw forward
                    {
                        StartCoroutine(spawnBanana(1));
                        ai_script.DriverAnim.SetTrigger("ThrowForward");
                        tripleitemActionTime = 0;

                    }
                    else //otherwise  throw back
                    {
                        StartCoroutine(spawnBanana(-1));
                        ai_script.DriverAnim.SetTrigger("ThrowBack");
                        tripleitemActionTime = 0;
                    }
                    tripleItemCount--;
                    ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false);
                }
            }
            else if(tripleItemCount <= 0)
            {
                SetHeldVisualActive(itemIndex, false);
                ItemsPossible[itemIndex].transform.GetChild(0).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(1).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(2).gameObject.SetActive(true);
                already_has_item = false;
                current_item = "";
                timeHavingItem = 0;
                tripleitemActionTime = 0;
            }
        }
        else if(current_item == "TripleGreenShells")
        {
            RaycastHit hit = new RaycastHit();

            if (tripleItemCount > 0 && tripleitemActionTime > 4)
            {
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////backwards Shot
                if (Physics.Raycast(transform.position, -transform.forward, out hit, Mathf.Infinity, mask)
                    || (Physics.Raycast(transform.position, Quaternion.AngleAxis(10, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                    || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-10, transform.up) * -transform.forward, out hit, Mathf.Infinity, mask)
                    )) && timeHavingItem > 5)
                {
                    if (hit.collider.tag == "Opponent" || hit.collider.tag == "Player")
                    {
                        StartCoroutine(useShell(-1, shellposBack));
                        ai_script.DriverAnim.SetTrigger("ThrowBack");
                        tripleItemCount--;
                        tripleitemActionTime = 0;
                        ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three shells

                    }
                }

                else if (Physics.Raycast(transform.position, transform.forward, out hit, Mathf.Infinity, mask) ////////////////////////////////////////////////////////////forward shot
                   || (Physics.Raycast(transform.position, Quaternion.AngleAxis(10, transform.up) * transform.forward, out hit, Mathf.Infinity, mask)
                   || (Physics.Raycast(transform.position, Quaternion.AngleAxis(-10, transform.up) * transform.forward, out hit, Mathf.Infinity, mask)
                   )))
                {
                    if (hit.collider.tag == "Opponent" || hit.collider.tag == "Player")
                    {
                        StartCoroutine(useShell(1, shellPos));
                        ai_script.DriverAnim.SetTrigger("ThrowForward");
                        tripleItemCount--;
                        tripleitemActionTime = 0;
                        ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three shells
                    }
                }

                else if (tripleitemActionTime > 5)
                {
                    if (lap_counter.Position != 1)
                    {
                        StartCoroutine(useShell(1, shellPos));
                        tripleItemCount--;
                        tripleitemActionTime = 0;
                    }
                    else
                    {
                        StartCoroutine(useShell(-1, shellposBack));
                        tripleItemCount--;
                        tripleitemActionTime = 0;
                        ai_script.DriverAnim.SetTrigger("ThrowBack");

                    }
                    ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three shells
                }
            }
            else if (tripleItemCount <= 0)
            {
                SetHeldVisualActive(itemIndex, false);
                ItemsPossible[itemIndex].transform.GetChild(0).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(1).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(2).gameObject.SetActive(true);
                already_has_item = false;
                current_item = "";
                timeHavingItem = 0;
                tripleitemActionTime = 0;
            }
        }
        else if(current_item == "TripleRedShells")
        {

            if (tripleItemCount > 0 && tripleitemActionTime > 4)
            {
                if (lap_counter.Position == 1)
                {
                    StartCoroutine(useRedShell(-1, shellposBack));
                    tripleItemCount--;
                    ai_script.DriverAnim.SetTrigger("ThrowBack");
                    tripleitemActionTime = 0;
                    ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three shells
                }
                else
                {
                    StartCoroutine(useRedShell(1, shellPos));
                    tripleItemCount--;
                    ai_script.DriverAnim.SetTrigger("ThrowForward");
                    tripleitemActionTime = 0;
                    ItemsPossible[itemIndex].transform.GetChild(tripleItemCount).gameObject.SetActive(false); //turn off one of the three shells
                }
            } 
            else if(tripleItemCount <= 0)
            {
                SetHeldVisualActive(itemIndex, false);
                ItemsPossible[itemIndex].transform.GetChild(0).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(1).gameObject.SetActive(true);
                ItemsPossible[itemIndex].transform.GetChild(2).gameObject.SetActive(true);
                already_has_item = false;
                current_item = "";
                timeHavingItem = 0;
                tripleitemActionTime = 0;
            }
        }
        else if(current_item == "ItemStar")
        {
            if (timeHavingItem > 4)
            {
                current_item = ""; //1 use only
                already_has_item = false;
                timeHavingItem = 0;
                ai_script.DriverAnim.SetBool("hasItem", false);
                StartCoroutine(UseStar());
            }
        }
        else if(current_item == "Coin")
        {
            if (timeHavingItem > 4)
            {
                current_item = ""; //1 use only
                already_has_item = false;
                timeHavingItem = 0;
                ai_script.DriverAnim.SetBool("hasItem", false);
                StartCoroutine(UseCoin());
            }
        }
    }

    public void useItemDOne()
    {
        SetHeldVisualActive(itemIndex, false);
        ResetTripleChildren(itemIndex);
        already_has_item = false;
        ai_script.DriverAnim.SetBool("hasItem", false);
        timeHavingItem = 0;
        current_item = "";
        tripleitemActionTime = 0;
    }

    IEnumerator useShell(int direction, Transform position)
    {

        yield return new WaitForSeconds(0.15f);
        GameObject clone = InstantiateWorldItem("GreenShell", position.position, position.rotation);
        if (clone == null)
        {
            yield break;
        }
        GreenShell shellComponent = clone.GetComponent<GreenShell>();
        if (shellComponent == null)
        {
            Debug.LogWarning("[OpponentItemManager] Spawned GreenShell prefab is missing GreenShell component.", clone);
            yield break;
        }

        if (direction == 1) //backwards or forwards -1 and 1 respectively
        {
            shellComponent.myVelocity = transform.forward.normalized;
            shellComponent.velocityMagOriginal = 6000;
            shellComponent.AntiGravity = ai_script.AntiGravity;
            yield return new WaitForSeconds(0.25f);
            if(current_item !="TripleGreenShells")
                SetHeldVisualActive(itemIndex, false); //hand shell

        }
        if (direction == -1)
        {
            shellComponent.myVelocity = -transform.forward.normalized;
            shellComponent.velocityMagOriginal = 3500;
            shellComponent.AntiGravity = ai_script.AntiGravity;
            yield return new WaitForSeconds(0.25f);
            if (current_item != "TripleGreenShells")
                SetHeldVisualActive(itemIndex, false); //hand shell

            /*
            for (int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                    player_script.current_face_material = player_script.faces[1]; //look left
                yield return new WaitForSeconds(0.01f);
            }
            if (!StarPowerUp)
                player_script.current_face_material = player_script.faces[2]; //blink
            yield return new WaitForSeconds(0.1f);
            if (!StarPowerUp)
                player_script.current_face_material = player_script.faces[0];//normal
            */
        }
        shellComponent.who_threw_shell = gameObject.name;
    }
    IEnumerator useRedShell(int direction, Transform position)
    {
        if (direction == 1)
        {
            yield return new WaitForSeconds(0.15f);
            GameObject clone = InstantiateWorldItem("RedShell", position.position, position.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.SetActive(true);
            RedShell redShellComponent = clone.GetComponent<RedShell>();
            if (redShellComponent != null)
            {
                redShellComponent.who_threw_shell = gameObject.name;
                redShellComponent.current_node = currentWayPoint;
                redShellComponent.AntiGravity = ai_script.AntiGravity;
            }
            else
            {
                Debug.LogWarning("[OpponentItemManager] Spawned RedShell prefab is missing RedShell component.", clone);
            }

            yield return new WaitForSeconds(0.25f);
            if(current_item != "TripleRedShells")
                SetHeldVisualActive(itemIndex, false); //hand shell
        }
        else if (direction == -1)
        {
            yield return new WaitForSeconds(0.15f);
            GameObject clone = InstantiateWorldItem("RedShell", position.position, position.rotation);
            if (clone == null)
            {
                yield break;
            }
            RedShell redShellComponent = clone.GetComponent<RedShell>();
            if (redShellComponent != null)
            {
                redShellComponent.who_threw_shell = gameObject.name;
                redShellComponent.enabled = false;
            }
            clone.SetActive(false);
            clone.AddComponent<GreenShell>();
            clone.SetActive(true);

            GreenShell shellComponent = clone.GetComponent<GreenShell>();
            if (shellComponent != null)
            {
                shellComponent.myVelocity = -transform.forward.normalized;
                shellComponent.velocityMagOriginal = 3500;
                shellComponent.AntiGravity = ai_script.AntiGravity;
                shellComponent.who_threw_shell = gameObject.name;
            }
            else
            {
                Debug.LogWarning("[OpponentItemManager] Converted RedShell is missing GreenShell component.", clone);
            }

            yield return new WaitForSeconds(0.25f);
            if (current_item != "TripleRedShells")
                SetHeldVisualActive(itemIndex, false); //hand shell

            /*
            for (int i = 0; i < 75; i++)
            {
                if (!StarPowerUp)
                    player_script.current_face_material = player_script.faces[1]; //make sure it is not changed, by repeating in for loop
                yield return new WaitForSeconds(0.01f);
            }
            if (!StarPowerUp)
                player_script.current_face_material = player_script.faces[2]; //blink
            yield return new WaitForSeconds(0.1f);
            if (!StarPowerUp)
                player_script.current_face_material = player_script.faces[0];//normal

            */
        }
    }
    IEnumerator spawnBanana(int direction)
    {
        GameObject clone;
        if (direction == 1)//forward
        {
            yield return new WaitForSeconds(0.1f);
            if (current_item != "TripleBananas")
                SetHeldVisualActive(itemIndex, false); //hand banana
            clone = InstantiateWorldItem("Banana", bananaPos.position, bananaPos.rotation);
            if (clone != null)
            {
                var bananaComponent = clone.GetComponent<Banana>();
                if (bananaComponent != null)
                {
                    bananaComponent.Banana_thrown(transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity).z * 500);
                    bananaComponent.whoThrewBanana = gameObject.name;
                }
                else
                {
                    Debug.LogWarning("[OpponentItemManager] Spawned Banana prefab is missing Banana component.", clone);
                }
            }
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
            clone = InstantiateWorldItem("Banana", shellposBack.position, bananaPos.rotation);
            if(current_item != "TripleBananas")
                SetHeldVisualActive(itemIndex, false); //hand banana
            if (clone != null)
            {
                var bananaComponent = clone.GetComponent<Banana>();
                if (bananaComponent != null)
                {
                    bananaComponent.whoThrewBanana = gameObject.name;
                }
            }
                                                       /*
                                                       for (int i = 0; i < 75; i++)
                                                       {
                                                           if (!StarPowerUp)
                                                               player_script.current_face_material = player_script.faces[1]; //make sure it is not changed, by repeating in for loop
                                                           yield return new WaitForSeconds(0.01f);
                                                       }
                                                       item_gameobjects[1].SetActive(false);
                                                       if (!StarPowerUp)
                                                           player_script.current_face_material = player_script.faces[2]; //blink
                                                       yield return new WaitForSeconds(0.1f);
                                                       if (!StarPowerUp)
                                                           player_script.current_face_material = player_script.faces[0];//normal
                                                       clone.GetComponent<Banana>().whoThrewBanana = gameObject.name;

                                                       */
        }


    }
    IEnumerator useBlueShell()
    {
        yield return new WaitForSeconds(0.15f);
        GameObject clone = InstantiateWorldItem("BlueShell", shellPos.position, shellPos.transform.rotation);
        if (clone == null)
        {
            yield break;
        }

        clone.SetActive(true);
        BlueShell blueShellComponent = clone.GetComponent<BlueShell>();
        if (blueShellComponent != null)
        {
            blueShellComponent.AntiGravity = ai_script.AntiGravity;
            blueShellComponent.current_node = currentWayPoint + 1;
            blueShellComponent.who_threw_shell = gameObject.name;
        }
        else
        {
            Debug.LogWarning("[OpponentItemManager] Spawned BlueShell prefab is missing BlueShell component.", clone);
        }

        SetHeldVisualActive(itemIndex, false); //hand shell
    }
    IEnumerator useBobomb(int direction)
    {
        if (direction == 1)
        {
            yield return new WaitForSeconds(0.1f);
                SetHeldVisualActive(itemIndex, false);

            GameObject clone = InstantiateWorldItem("Bobomb-Hold", bananaPos.position, bananaPos.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.SetActive(true);
            clone.GetComponent<Bobomb>().bomb_thrown(transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity).z * 400);
            clone.GetComponent<AudioSource>().enabled = true;

            for (int i = 0; i < clone.GetComponent<Bobomb>().renderers.Length; i++)
            {
                clone.GetComponent<Bobomb>().renderers[i].enabled = true;
            }
            for (int i = 0; i < clone.GetComponent<Bobomb>().spark.Length; i++)
            {
                clone.GetComponent<Bobomb>().spark[i].SetActive(true);
            }
            clone.GetComponent<Bobomb>().whoThrewBomb = gameObject.name;

        }
        if (direction == -1)
        {
            yield return new WaitForSeconds(0.1f);
            SetHeldVisualActive(itemIndex, false);

            GameObject clone = InstantiateWorldItem("Bobomb-Hold", shellposBack.position, shellposBack.rotation);
            if (clone == null)
            {
                yield break;
            }
            clone.SetActive(true);
            clone.GetComponent<AudioSource>().enabled = true;
            clone.GetComponent<Bobomb>().bounce_count = 4;

            for (int i = 0; i < clone.GetComponent<Bobomb>().renderers.Length; i++)
            {
                clone.GetComponent<Bobomb>().renderers[i].enabled = true;
            }
            for (int i = 0; i < clone.GetComponent<Bobomb>().spark.Length; i++)
            {
                clone.GetComponent<Bobomb>().spark[i].SetActive(true);
            }
            clone.GetComponent<Bobomb>().whoThrewBomb = gameObject.name;
        }
    }
    IEnumerator UseBullet()
    {
        timeHavingItem = 0;
        SetHeldVisualActive(itemIndex, false);
        isBullet = true;

        //disabling the player renderers
        bulletPlayer.SetActive(true);
        for(int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].enabled = false;
        }

        //playersounds.effectSounds[0].Stop(); //drifting  noise
        //playersounds.effectSounds[1].Stop(); //drifting spark noise
        ai_script.boost_time = 0;
        comp_sounds.BulletSounds[1].Play();
        comp_sounds.BulletSounds[0].Play();

        yield return new WaitForSeconds(11);
        already_has_item = false;
        ai_script.DriverAnim.SetBool("hasItem", false);
        current_item = "";

        isBullet = false;
        bulletPlayer.SetActive(false);
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].enabled = true;
        }
        ai_script.current_speed = 70;
        timeHavingItem = 0;
        comp_sounds.BulletSounds[0].Stop();


    }
    IEnumerator UseStar()
    {
        SetHeldVisualActive(itemIndex, false);
        StarPowerUp = true;
        
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            playerRenderers[i].material = starMat;
            playerRenderers[i].sharedMaterial = starMat;
        }
        
        for (int i = 0; i < starPS.transform.childCount; i++)
        {
            starPS.transform.GetChild(i).GetComponent<ParticleSystem>().Play();
        }
        
        /*
        if (playersounds.Check_if_playing())
        {
            playersounds.MarioStarSounds[playersounds.star_count_sound].Play();
            playersounds.star_count_sound++;
            if (playersounds.star_count_sound > 2)
            {
                playersounds.star_count_sound = 0;
            }
        }
        */


        yield return new WaitForSeconds(7.5f);

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
        closeToPlayer = false;
        

    }
    IEnumerator UseCoin()
    {
        GameObject clone = InstantiateWorldItem("Coin", coinPos.position, coinPos.rotation);
        if (clone != null)
        {
            clone.transform.SetParent(transform);
        }
        SetHeldVisualActive(itemIndex, false);
        yield return new WaitForSeconds(0.1f);
    }






}
