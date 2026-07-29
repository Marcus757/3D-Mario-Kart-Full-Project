using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RedShell : MonoBehaviour
{
    private enum ShellState
    {
        Inactive,
        Held,
        Trailing,
        Projectile,
        Cooldown
    }

    [Header("Movement")]
    public Transform pathMain;
    public Transform pathOption1;
    public Transform pathOption2;
    public int current_node = 0;

    [Header("Physics")]
    public LayerMask mask;
    public LayerMask maskAntiGravity;
    [SerializeField] private float speed = 6000f;
    public bool needsExtraDownForceAntigravity = false;

    [Header("Targets")]
    public GameObject[] allplayers;

    [HideInInspector] public float dir = 0f;
    [HideInInspector] public string who_threw_shell;
    [HideInInspector] public float lifetime;
    [HideInInspector] public bool AntiGravity = false;
    [HideInInspector] public bool isactive = false;

    private Rigidbody rb;
    private SphereCollider sphereCollider;
    private Renderer[] cachedRenderers;
    private ParticleSystem[] cachedParticles;
    private Animator meshAnimator;
    private IItemDriver ownerManager;
    private Transform followParent;

    private GameObject[] opponents;
    private Transform chase_opponent;
    private Transform player;
    private RACE_MANAGER rm;

    private bool lockedOnTarget;
    private bool grounded;
    private bool antiGravityGrounded;
    private bool closeToPlayer;
    private float y;
    private bool pathsResolved;
    private bool managedByItemManager;

    private ShellState currentState = ShellState.Inactive;
    private Coroutine cooldownRoutine;
    private Vector3 initialLocalScale;

    private void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedParticles = GetComponentsInChildren<ParticleSystem>(true);
        meshAnimator = GetComponentInChildren<Animator>(true);
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        rm = GameObject.Find("RaceManager")?.GetComponent<RACE_MANAGER>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        initialLocalScale = transform.localScale;
        EnterInactive(false);
    }

    public void Initialize(IItemDriver owner)
    {
        ownerManager = owner;
        managedByItemManager = true;
        EnterInactive();
    }

    private void Start()
    {
        opponents = GameObject.FindGameObjectsWithTag("Opponent");
        if (!managedByItemManager)
        {
            currentState = ShellState.Projectile;
            isactive = true;
        }
    }

    private void OnEnable()
    {
        ReacquireReferencesIfNeeded();
    }

    public bool IsAvailable()
    {
        return currentState == ShellState.Inactive || currentState == ShellState.Held;
    }

    public void EnterHeld(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        StopCooldown();
        currentState = ShellState.Held;
        followParent = null;
        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        sphereCollider.enabled = false;
        sphereCollider.isTrigger = false;
        EnsureRenderers(true);
        if (meshAnimator != null)
        {
            meshAnimator.enabled = false;
        }
    }

    public void EnterTrailing(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        StopCooldown();
        currentState = ShellState.Trailing;
        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;
        followParent = parent;
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        sphereCollider.enabled = true;
        sphereCollider.isTrigger = true;
        EnsureRenderers(true);
        if (meshAnimator != null)
        {
            meshAnimator.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        lifetime += Time.deltaTime;

        DetectTarget();
        if (!lockedOnTarget)
        {
            RotateTowards();
        }
        else
        {
            ChaseTarget();
        }

        MoveProjectile();
        ApplyAdditionalForces();
        CheckWarningTrigger();
    }

    private void LateUpdate()
    {
        if (currentState == ShellState.Trailing && followParent != null)
        {
            transform.position = followParent.position;
            transform.rotation = followParent.rotation;
            return;
        }

        if (currentState == ShellState.Held && ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null && itemMgr.GreenShellStorage != null && transform.parent == itemMgr.GreenShellStorage)
            {
                if (transform.localPosition != Vector3.zero)
                {
                    transform.localPosition = Vector3.zero;
                }

            if (transform.localRotation != Quaternion.identity)
            {
                transform.localRotation = Quaternion.identity;
            }

            if (transform.localScale != initialLocalScale)
            {
                transform.localScale = initialLocalScale;
            }
        }
    }

    public void EnterProjectile(Vector3 position, Quaternion rotation, int startingNode, bool antiGravity, string ownerName)
    {
        if (managedByItemManager && ownerManager == null)
        {
            return;
        }

        StopCooldown();
        followParent = null;
        if (managedByItemManager)
        {
            EnsureRenderers(false);
        }
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = initialLocalScale;

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
        }

        if (sphereCollider != null)
        {
            sphereCollider.enabled = true;
            sphereCollider.isTrigger = false;
        }

        who_threw_shell = ownerName;
        AntiGravity = antiGravity;
        lifetime = 0f;
        current_node = Mathf.Max(0, startingNode);

        lockedOnTarget = false;
        chase_opponent = null;
        closeToPlayer = false;
        grounded = false;
        antiGravityGrounded = false;

        pathsResolved = false;
        if (managedByItemManager)
        {
            StartCoroutine(DelayActivateForProjectile(position));
        }
        else
        {
            EnsureRenderers(true);
        }
        if (meshAnimator != null)
        {
            meshAnimator.enabled = true;
        }

        isactive = true;
        currentState = ShellState.Projectile;

        EnsurePathsResolved();
        if (pathMain != null && pathMain.childCount > 0)
        {
            current_node = Mathf.Clamp(current_node, 0, pathMain.childCount - 1);
        }
    }

    private IEnumerator DelayActivateForProjectile(Vector3 launchPosition)
    {
        EnsureRenderers(false);
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            Vector3 displacement = transform.position - launchPosition;
            if (displacement.sqrMagnitude >= 9f)
            {
                break;
            }
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }
        EnsureRenderers(true);
    }

    public void LaunchStandalone(Vector3 position, Quaternion rotation, int startingNode, bool antiGravity, string ownerName)
    {
        managedByItemManager = false;
        ownerManager = null;
        EnterProjectile(position, rotation, startingNode, antiGravity, ownerName);
    }

    internal void ReturnToPool()
    {
        EnterInactive();
    }

    public void destroyShell()
    {
        if (currentState == ShellState.Cooldown)
        {
            return;
        }

        isactive = false;
        currentState = ShellState.Cooldown;

        if (cachedParticles != null)
        {
            for (int i = 0; i < cachedParticles.Length; i++)
            {
                if (cachedParticles[i] != null)
                {
                    cachedParticles[i].Play();
                }
            }
        }

        SetMeshEnabled(false);
        if (meshAnimator != null)
        {
            meshAnimator.enabled = false;
        }

        if (sphereCollider != null)
        {
            sphereCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        StopCooldown();
        cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        SetMeshEnabled(false);
        yield return new WaitForSeconds(3f);
        EnsureRenderers(true);
        EnterInactive();
        if (ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null) itemMgr.OnRedShellReturned(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnterInactive()
    {
        EnterInactive(true);
    }

    private void EnterInactive(bool hideVisuals)
    {
        currentState = ShellState.Inactive;
        isactive = false;
        lockedOnTarget = false;
        chase_opponent = null;
        closeToPlayer = false;
        y = 0f;
        followParent = null;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (sphereCollider != null)
        {
            sphereCollider.enabled = false;
            sphereCollider.isTrigger = false;
        }

        if (hideVisuals)
        {
            EnsureRenderers(false);
            if (meshAnimator != null)
            {
                meshAnimator.enabled = false;
            }
        }
        else
        {
            EnsureRenderers(true);
            if (meshAnimator != null)
            {
                meshAnimator.enabled = false;
            }
        }

        gameObject.SetActive(false);
        if (ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null && itemMgr.GreenShellStorage != null)
            {
                transform.SetParent(itemMgr.GreenShellStorage, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = initialLocalScale;
                if (rb != null)
                {
                    rb.position = transform.position;
                    rb.rotation = transform.rotation;
                }
            }
        }
    }

    private void StopCooldown()
    {
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }
    }

    private void MoveProjectile()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 velocity = transform.forward * speed * Time.deltaTime;
        if (!AntiGravity)
        {
            velocity.y = rb.velocity.y;
        }

        rb.velocity = velocity;

        if (needsExtraDownForceAntigravity && AntiGravity && !antiGravityGrounded)
        {
            rb.AddRelativeForce(Vector3.down * 20000f * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    private void ApplyAdditionalForces()
    {
        if (rb == null)
        {
            return;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        Player playerComponent = player != null ? player.GetComponent<Player>() : null;

        if (playerComponent != null && !playerComponent.GLIDER_FLY && !AntiGravity)
        {
            rb.AddForce(Vector3.down * 15000f * Time.deltaTime, ForceMode.Acceleration);
        }

        if (AntiGravity)
        {
            rb.AddRelativeForce(Vector3.down * 30000f * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    private void CheckWarningTrigger()
    {
        if (closeToPlayer)
        {
            return;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (player == null || string.Equals(who_threw_shell, player.name))
        {
            return;
        }

        if (rm == null)
        {
            rm = GameObject.Find("RaceManager")?.GetComponent<RACE_MANAGER>();
        }

        if (rm == null)
        {
            return;
        }

        if (Vector3.Distance(player.position, transform.position) < 100f)
        {
            closeToPlayer = true;
            StartCoroutine(rm.warningRedShell(transform));
        }
    }

    private void RotateTowards()
    {
        if (pathMain == null || pathMain.childCount == 0)
        {
            return;
        }

        Ray ground = new Ray(transform.position, -transform.up);
        if (AntiGravity)
        {
            if (Physics.Raycast(ground, out RaycastHit antiHit, 10f))
            {
                transform.rotation = Quaternion.LerpUnclamped(transform.rotation, Quaternion.FromToRotation(transform.up * 2f, antiHit.normal) * transform.rotation, 10f * Time.deltaTime);
                AdjustRotationTowardsPoint(pathMain.GetChild(current_node).position);
            }
            return;
        }

        if (Physics.Raycast(ground, out RaycastHit hit, 10f, mask))
        {
            Quaternion rot = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(transform.up * 2f, hit.normal) * transform.rotation, 6f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(rot.eulerAngles.x, transform.eulerAngles.y, rot.eulerAngles.z);
        }

        AdjustRotationTowardsPoint(pathMain.GetChild(current_node).position);
    }

    private void AdjustRotationTowardsPoint(Vector3 targetPosition)
    {
        Vector3 desired = targetPosition - transform.position;
        Vector3 angle = Vector3.Cross(transform.forward, desired);
        dir = Vector3.Dot(angle, transform.up);
        float velocity = 0f;
        y = Mathf.SmoothDamp(y, dir, ref velocity, 2.5f * Time.deltaTime);
        transform.Rotate(0f, y * 0.5f, 0f, Space.Self);
    }

    private void ChaseTarget()
    {
        if (chase_opponent == null)
        {
            lockedOnTarget = false;
            return;
        }

        Ray ground = new Ray(transform.position, -transform.up);
        if (Physics.Raycast(ground, out RaycastHit hit, 10f, mask))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(transform.up * 2f, hit.normal) * transform.rotation, 6f * Time.deltaTime);
        }

        Vector3 desired = chase_opponent.position - transform.position;
        Vector3 angle = Vector3.Cross(transform.forward, desired);
        dir = Vector3.Dot(angle, transform.up);
        transform.Rotate(0f, dir * 2f, 0f, Space.Self);
    }

    private void DetectTarget()
    {
        if (lockedOnTarget || string.IsNullOrEmpty(who_threw_shell))
        {
            return;
        }

        if (opponents == null || opponents.Length == 0)
        {
            opponents = GameObject.FindGameObjectsWithTag("Opponent");
        }

        if (who_threw_shell == "Mario")
        {
            DetectOpponentTarget(opponents);
        }
        else
        {
            DetectPlayerTarget(allplayers);
        }
    }

    private void DetectOpponentTarget(GameObject[] candidates)
    {
        if (candidates == null)
        {
            return;
        }

        LapCounter ownerCounter = GetLapCounter(who_threw_shell);
        if (ownerCounter == null)
        {
            return;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject target = candidates[i];
            if (target == null)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, target.transform.position) >= 30f)
            {
                continue;
            }

            LapCounter targetCounter = target.GetComponent<LapCounter>();
            if (targetCounter == null)
            {
                continue;
            }

            if (ownerCounter.totalCheckpointVal < targetCounter.totalCheckpointVal ||
                (ownerCounter.totalCheckpointVal == targetCounter.totalCheckpointVal &&
                 ownerCounter.distanceToNextCheckpoint > targetCounter.distanceToNextCheckpoint))
            {
                chase_opponent = target.transform;
                lockedOnTarget = true;
                break;
            }
        }
    }

    private void DetectPlayerTarget(GameObject[] candidates)
    {
        if (candidates == null)
        {
            return;
        }

        LapCounter ownerCounter = GetLapCounter(who_threw_shell);
        if (ownerCounter == null)
        {
            return;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject target = candidates[i];
            if (target == null || target.name == who_threw_shell)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, target.transform.position) >= 30f)
            {
                continue;
            }

            LapCounter targetCounter = target.GetComponent<LapCounter>();
            if (targetCounter == null)
            {
                continue;
            }

            if (targetCounter.totalCheckpointVal > ownerCounter.totalCheckpointVal ||
                (targetCounter.totalCheckpointVal == ownerCounter.totalCheckpointVal &&
                 ownerCounter.distanceToNextCheckpoint > targetCounter.distanceToNextCheckpoint))
            {
                chase_opponent = target.transform;
                lockedOnTarget = true;
                break;
            }
        }
    }

    private LapCounter GetLapCounter(string racerName)
    {
        GameObject ownerObject = GameObject.Find(racerName);
        return ownerObject != null ? ownerObject.GetComponent<LapCounter>() : null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState == ShellState.Trailing && ownerManager != null)
        {
            HandleTrailingStateCollision(other);
            return;
        }

        if (currentState != ShellState.Projectile)
        {
            return;
        }

        EnsurePathsResolved();
        if (pathMain == null || pathMain.childCount == 0)
        {
            return;
        }

        if (other.transform == pathMain.GetChild(current_node))
        {
            if (current_node >= pathMain.childCount - 1)
            {
                current_node = 0;
            }
            else
            {
                current_node++;
            }
        }

        if (other.CompareTag("TrailingItem"))
        {
            HandleTrailingItemCollision(other);
        }
    }

    private void HandleTrailingStateCollision(Collider other)
    {
        if (other.gameObject == ownerManager.gameObject)
        {
            return;
        }

        if (other.CompareTag("Opponent"))
        {
            OpponentItemManager opponent = other.GetComponent<OpponentItemManager>() ?? other.GetComponentInParent<OpponentItemManager>();
            if (opponent != null && !opponent.StarPowerUp)
            {
                opponent.hitByShell();
                if (ownerManager != null)
                {
                    ItemManager itemMgr = ownerManager as ItemManager;
                    if (itemMgr != null) itemMgr.OnRedShellTrailingConsumed(this);
                }
                destroyShell();
            }
            return;
        }

        if (other.CompareTag("Player"))
        {
            ItemManager targetManager = other.GetComponent<ItemManager>();
            if (targetManager != null && targetManager != ownerManager && !targetManager.StarPowerUp)
            {
                Player targetPlayer = other.GetComponent<Player>();
                if (targetPlayer != null)
                {
                    StartCoroutine(targetPlayer.hitByShell());
                }
                if (ownerManager != null)
                {
                    ItemManager itemMgr = ownerManager as ItemManager;
                    if (itemMgr != null) itemMgr.OnRedShellTrailingConsumed(this);
                }
                destroyShell();
            }
            return;
        }

        if (other.CompareTag("Banana") || other.CompareTag("Cow"))
        {
            if (ownerManager != null)
            {
                ItemManager itemMgr = ownerManager as ItemManager;
                if (itemMgr != null) itemMgr.OnRedShellTrailingConsumed(this);
            }
            destroyShell();
        }
    }

    private void HandleTrailingItemCollision(Collider other)
    {
        ItemManager manager = FindItemManager(other.transform);
        if (manager == null)
        {
            destroyShell();
            return;
        }

        if (other.gameObject.name == "TrailingBanana")
        {
            if (manager != null)
            {
                Banana banana = other.GetComponentInParent<Banana>();
                if (banana == null)
                {
                    banana = other.transform.parent != null ? other.transform.parent.GetComponent<Banana>() : null;
                }
                manager.OnBananaTrailingConsumed(banana);
            }
        }
        else
        {
            Transform particleRoot = other.transform.childCount > 0 ? other.transform.GetChild(0) : null;
            if (particleRoot != null)
            {
                for (int i = 0; i < particleRoot.childCount; i++)
                {
                    ParticleSystem ps = particleRoot.GetChild(i).GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                    }
                }
            }

            if (manager.CurrentTrailingItem != null)
            {
                manager.CurrentTrailingItem.SetActive(false);
                manager.CurrentTrailingItem = null;
            }
        }

        manager.current_Item = string.Empty;
        manager.used_Item_Done();
        destroyShell();
    }

    private ItemManager FindItemManager(Transform start)
    {
        Transform current = start;
        while (current != null)
        {
            ItemManager manager = current.GetComponent<ItemManager>();
            if (manager != null)
            {
                return manager;
            }
            current = current.parent;
        }

        return null;
    }

    private void OnTriggerStay(Collider other)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        EnsurePathsResolved();

        if (other.gameObject.name.Equals("ItemPathColliderPath1"))
        {
            pathMain = pathOption1;
        }
        else if (other.gameObject.name.Equals("ItemPathColliderPath2"))
        {
            pathMain = pathOption2;
        }

        if (other.CompareTag("AntiGravity"))
        {
            AntiGravity = true;
        }
        else if (other.CompareTag("AntiGravityFalse"))
        {
            AntiGravity = false;
            AntiGravityExitRotate exitRotate = other.GetComponent<AntiGravityExitRotate>();
            if (exitRotate != null)
            {
                ApplyExitRotation(exitRotate);
            }
        }
    }

    private void ApplyExitRotation(AntiGravityExitRotate exitRotate)
    {
        if (exitRotate.rotateX)
        {
            transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(exitRotate.newRotation.x, transform.eulerAngles.y, transform.eulerAngles.z), Time.deltaTime);
        }

        if (exitRotate.rotateZ)
        {
            transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, exitRotate.newRotation.z), 3f * Time.deltaTime);
        }

        if (exitRotate.rotateY)
        {
            transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(transform.eulerAngles.x, exitRotate.newRotation.y, transform.eulerAngles.z), 3f * Time.deltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Dirt"))
        {
            return;
        }

        if (collision.gameObject.name.Equals(who_threw_shell))
        {
            if (lifetime > 0.5f)
            {
                destroyShell();
            }
            return;
        }

        if (collision.gameObject.CompareTag("Shell"))
        {
            destroyShell();
            return;
        }

        if (collision.gameObject.CompareTag("Banana") || collision.gameObject.CompareTag("Cow"))
        {
            destroyShell();
            if (!collision.gameObject.CompareTag("Cow"))
            {
                Destroy(collision.gameObject);
            }
            return;
        }

        if (collision.gameObject.CompareTag("Opponent") && collision.gameObject.name != who_threw_shell)
        {
            HandleOpponentCollision(collision.gameObject);
            return;
        }

        if (collision.gameObject.CompareTag("Player") && collision.gameObject.name != who_threw_shell)
        {
            HandlePlayerCollision(collision.gameObject);
        }
    }

    private void HandleOpponentCollision(GameObject opponentObject)
    {
        OpponentItemManager opponent = opponentObject.GetComponent<OpponentItemManager>();
        if (opponent == null)
        {
            destroyShell();
            return;
        }

        if (!opponent.StarPowerUp)
        {
            opponent.hitByShell();
            if (who_threw_shell == "Mario")
            {
                GameObject mario = GameObject.Find("Mario");
                if (mario != null)
                {
                    Player marioPlayer = mario.GetComponent<Player>();
                    if (marioPlayer != null && marioPlayer.Driver != null)
                    {
                        marioPlayer.Driver.SetTrigger("HitItem");
                    }
                }

                PlayerSounds sounds = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerSounds>();
                if (sounds != null && sounds.Check_if_playing())
                {
                    sounds.effectSounds[18].Play();
                }
            }
        }

        destroyShell();
    }

    private void HandlePlayerCollision(GameObject playerObject)
    {
        if (Vector3.Distance(transform.position, playerObject.transform.position) >= 5f)
        {
            return;
        }

        ItemManager targetManager = playerObject.GetComponent<ItemManager>();
        if (targetManager != null && !targetManager.StarPowerUp)
        {
            Player targetPlayer = playerObject.GetComponent<Player>();
            if (targetPlayer != null)
            {
                StartCoroutine(targetPlayer.hitByShell());
            }

            Animator mainCameraAnimator = GameObject.Find("Main Camera")?.GetComponent<Animator>();
            if (mainCameraAnimator != null)
            {
                mainCameraAnimator.SetTrigger("ShellHit");
            }
        }

        destroyShell();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        if (!collision.gameObject.CompareTag("Ground") && !collision.gameObject.CompareTag("Dirt"))
        {
            grounded = true;
        }
        else
        {
            antiGravityGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        if (!collision.gameObject.CompareTag("Ground") && !collision.gameObject.CompareTag("Dirt"))
        {
            grounded = false;
        }
        else
        {
            antiGravityGrounded = false;
        }
    }

    private void EnsureRenderers(bool enabled)
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = enabled;
            }
        }
    }

    private void SetMeshEnabled(bool enabled)
    {
        if (cachedRenderers == null)
        {
            return;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            renderer.enabled = enabled;
        }
    }

    private void EnsurePathsResolved()
    {
        if (pathsResolved && pathMain != null)
        {
            return;
        }

        Transform owner = null;
        if (!string.IsNullOrEmpty(who_threw_shell))
        {
            GameObject ownerObject = GameObject.Find(who_threw_shell);
            if (ownerObject != null)
            {
                owner = ownerObject.transform;
            }
        }

        if (owner != null)
        {
            ItemManager itemManager = owner.GetComponent<ItemManager>();
            if (itemManager != null)
            {
                if (itemManager.path1 != null)
                {
                    pathOption1 = itemManager.path1;
                }
                else if (itemManager.path != null)
                {
                    pathOption1 = itemManager.path;
                }

                if (itemManager.path2 != null)
                {
                    pathOption2 = itemManager.path2;
                }
            }
            else
            {
                OpponentItemManager opponent = owner.GetComponent<OpponentItemManager>();
                if (opponent != null)
                {
                    if (opponent.path != null)
                    {
                        pathOption1 = opponent.path;
                    }

                    if (opponent.path2 != null)
                    {
                        pathOption2 = opponent.path2;
                    }
                }
            }
        }

        if (pathOption1 == null)
        {
            Transform globalPaths = RACE_MANAGER.allPaths;
            if (globalPaths != null)
            {
                if (globalPaths.childCount > 0)
                {
                    pathOption1 = globalPaths.GetChild(0);
                }
                else
                {
                    pathOption1 = globalPaths;
                }
            }
            else
            {
                GameObject legacy = GameObject.Find("ITEM PATHS");
                if (legacy != null)
                {
                    pathOption1 = legacy.transform;
                }
            }
        }

        if (pathOption2 == null)
        {
            if (pathOption1 != null && pathOption1.childCount > 1)
            {
                pathOption2 = pathOption1.GetChild(1);
            }
            else
            {
                Transform globalPaths = RACE_MANAGER.allPaths;
                if (globalPaths != null && globalPaths.childCount > 1)
                {
                    pathOption2 = globalPaths.GetChild(1);
                }
                else
                {
                    pathOption2 = pathOption1;
                }
            }
        }

        pathMain = pathOption1;
        pathsResolved = pathMain != null;

        if (!pathsResolved)
        {
            Debug.LogWarning($"[RedShell] Unable to resolve paths for '{name}'.", this);
        }
    }

    private void ReacquireReferencesIfNeeded()
    {
        if (rm == null)
        {
            rm = GameObject.Find("RaceManager")?.GetComponent<RACE_MANAGER>();
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (opponents == null || opponents.Length == 0)
        {
            opponents = GameObject.FindGameObjectsWithTag("Opponent");
        }
    }
}
