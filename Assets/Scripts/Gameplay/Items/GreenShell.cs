using System.Collections;
using UnityEngine;

//THIS script is for shells moving around the track shot by opponents or player

[DisallowMultipleComponent]
public class GreenShell : MonoBehaviour
{
    private enum ShellState
    {
        Inactive,
        Held,
        Trailing,
        Projectile,
        Cooldown
    }

    [SerializeField]
    private LayerMask mask;

    [SerializeField]
    private bool needsExtraDownForceAntigravity = false;

    private SphereCollider sphereCollider;
    private Rigidbody rb;
    private RACE_MANAGER rm;
    private IItemDriver ownerManager;
    private Transform followParent;
    private Renderer[] cachedRenderers;
    private ParticleSystem[] cachedParticles;
    private Animator meshAnimator;
    private Transform visualRoot;
    private Vector3 initialVisualLocalPos;
    private Quaternion initialVisualLocalRot;
    private Vector3 initialVisualLocalScale;
    private Coroutine resetRoutine;
    private ShellState currentState = ShellState.Projectile;
    private bool managedByItemManager;
    private Vector3 initialLocalScale;

    public Vector3 myVelocity;
    [HideInInspector] public float lifetime = 0;
    [HideInInspector] public string who_threw_shell;
    [HideInInspector] public bool AntiGravity = false;
    [HideInInspector] public float velocityMagOriginal;

    private bool grounded = false;
    private bool antiGravityGrounded = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        rm = GameObject.Find("RaceManager").GetComponent<RACE_MANAGER>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedParticles = GetComponentsInChildren<ParticleSystem>(true);
        meshAnimator = GetComponentInChildren<Animator>(true);
        initialLocalScale = transform.localScale;
        if (cachedRenderers != null && cachedRenderers.Length > 0)
        {
            visualRoot = cachedRenderers[0].transform;
            initialVisualLocalPos = visualRoot.localPosition;
            initialVisualLocalRot = visualRoot.localRotation;
            initialVisualLocalScale = visualRoot.localScale;
        }
    }

    public void Initialize(IItemDriver owner)
    {
        ownerManager = owner;
        managedByItemManager = true;
        EnterInactive();
    }

    private void Start()
    {
        if (!managedByItemManager)
        {
            currentState = ShellState.Projectile;
            ActivateForProjectile();
        }
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

    public void EnterProjectile(Vector3 position, Quaternion rotation, Vector3 direction, float speed, bool antiGravity, string ownerName)
    {
        if (ownerManager == null)
        {
            return;
        }

        StopCooldown();
        followParent = null;
        EnsureRenderers(false);
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = initialLocalScale;
        rb.position = position;
        rb.rotation = rotation;
        myVelocity = direction.normalized;
        if (myVelocity == Vector3.zero)
        {
            myVelocity = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(myVelocity, Vector3.up);
            rb.rotation = transform.rotation;
        }
        velocityMagOriginal = speed;
        AntiGravity = antiGravity;
        lifetime = 0f;
        who_threw_shell = ownerName;
        currentState = ShellState.Projectile;
        sphereCollider.enabled = true;
        sphereCollider.isTrigger = false;
        rb.isKinematic = false;
        if (meshAnimator != null)
        {
            meshAnimator.enabled = true;
        }

        StartCoroutine(DelayActivateForProjectile(position));
        ApplyImmediateVelocity();
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

    public void LaunchStandalone(Vector3 position, Quaternion rotation, Vector3 direction, float speed, bool antiGravity, string ownerName)
    {
        managedByItemManager = false;
        ownerManager = null;
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = initialLocalScale;
        rb.position = position;
        rb.rotation = rotation;
        myVelocity = direction.normalized;
        if (myVelocity == Vector3.zero)
        {
            myVelocity = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(myVelocity, Vector3.up);
            rb.rotation = transform.rotation;
        }
        velocityMagOriginal = speed;
        AntiGravity = antiGravity;
        lifetime = 0f;
        who_threw_shell = ownerName;
        currentState = ShellState.Projectile;
        sphereCollider.enabled = true;
        sphereCollider.isTrigger = false;
        rb.isKinematic = false;
        EnsureRenderers(true);
        if (meshAnimator != null)
        {
            meshAnimator.enabled = true;
        }
        ApplyImmediateVelocity();
    }

    internal void ReturnToPool()
    {
        EnterInactive();
    }

    private void StopCooldown()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
    }

    private void ActivateForProjectile()
    {
        currentState = ShellState.Projectile;
        sphereCollider.enabled = true;
        sphereCollider.isTrigger = false;
        rb.isKinematic = false;
        EnsureRenderers(true);
        if (meshAnimator != null)
        {
            meshAnimator.enabled = true;
        }
        gameObject.SetActive(true);
    }

    private void ApplyImmediateVelocity()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 initialVelocity = myVelocity.normalized * velocityMagOriginal * Time.deltaTime;
        rb.velocity = initialVelocity;
        Debug.Log($"[GreenShell] Initial velocity set {rb.velocity} frame {Time.frameCount}", this);
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

    private void FixedUpdate()
    {
        if (currentState == ShellState.Projectile)
        {
            Move();
            GroundNormalRotation();
        }
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

    private void Move()
    {
        myVelocity = myVelocity.normalized;
        myVelocity *= velocityMagOriginal * Time.deltaTime;

        if (!AntiGravity)
        {
            myVelocity.y = rb.velocity.y;
        }

        rb.velocity = myVelocity;

        if (!AntiGravity)
        {
            Player player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
            if (player != null && !player.GLIDER_FLY)
            {
                rb.AddForce(Vector3.down * 20000 * Time.deltaTime, ForceMode.Acceleration);
            }
        }
        else
        {
            rb.AddRelativeForce(Vector3.down * 10000 * Time.deltaTime, ForceMode.Acceleration);
        }

        if (needsExtraDownForceAntigravity && AntiGravity && !antiGravityGrounded)
        {
            rb.AddRelativeForce(Vector3.down * 100000 * Time.deltaTime, ForceMode.Acceleration);
        }

        lifetime += Time.deltaTime;
    }

    private void GroundNormalRotation()
    {
        Ray ground = new Ray(transform.position, -transform.up);
        if (Physics.Raycast(ground, out RaycastHit hit, 10, mask))
        {
            transform.rotation = Quaternion.LerpUnclamped(transform.rotation, Quaternion.FromToRotation(transform.up * 2, hit.normal) * transform.rotation, 13f * Time.deltaTime);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "AntiGravity")
        {
            AntiGravity = true;
        }
        if (other.gameObject.tag == "AntiGravityFalse")
        {
            AntiGravity = false;

            AntiGravityExitRotate exitRotate = other.gameObject.GetComponent<AntiGravityExitRotate>();
            if (exitRotate == null)
            {
                return;
            }

            if (exitRotate.rotateX)
            {
                transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(exitRotate.newRotation.x, transform.eulerAngles.y, transform.eulerAngles.z), 1 * Time.deltaTime);
            }
            if (exitRotate.rotateZ)
            {
                transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, exitRotate.newRotation.z), 3 * Time.deltaTime);
            }
            if (exitRotate.rotateY)
            {
                transform.rotation = Quaternion.SlerpUnclamped(transform.rotation, Quaternion.Euler(transform.eulerAngles.x, exitRotate.newRotation.y, transform.eulerAngles.z), 3 * Time.deltaTime);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        if (collision.gameObject.tag == "Ground" || collision.gameObject.tag == "Dirt" || collision.gameObject.tag == "JumpPanel" || collision.gameObject.tag == "ShellPlatforms")
        {
            return;
        }

        if (collision.gameObject.tag == "Shell")
        {
            destroyShell();
            return;
        }

        if (collision.gameObject.tag == "Banana" || collision.gameObject.tag == "Cow")
        {
            destroyShell();
            if (collision.gameObject.tag != "Cow")
            {
                Destroy(collision.gameObject);
            }
            return;
        }

        if (collision.gameObject.name.Equals(who_threw_shell))
        {
            if (lifetime > 0.5f)
            {
                destroyShell();
            }
            else
            {
                Physics.IgnoreCollision(collision.collider, sphereCollider);
            }
            return;
        }

        if (collision.gameObject.tag != "GliderPanel")
        {
            rb.velocity = Vector3.zero;
            myVelocity = Vector3.Reflect(myVelocity, collision.contacts[0].normal);
            if (lifetime > 20)
            {
                destroyShell();
            }
        }

        if (collision.gameObject.tag == "Opponent" && collision.gameObject.tag != who_threw_shell)
        {
            OpponentItemManager opponent = collision.gameObject.GetComponent<OpponentItemManager>();
            if (opponent != null && !opponent.StarPowerUp && lifetime > 0.1f)
            {
                opponent.hitByShell();
                if (who_threw_shell == "Mario")
                {
                    GameObject.Find("Mario").GetComponent<Player>().Driver.SetTrigger("HitItem");
                    PlayerSounds sounds = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSounds>();
                    if (sounds != null && sounds.Check_if_playing())
                    {
                        sounds.effectSounds[18].Play();
                    }
                }
                destroyShell();
            }
        }

        if (collision.gameObject.tag == "Player" && lifetime > 0.05f)
        {
            ItemManager targetManager = collision.gameObject.GetComponent<ItemManager>();
            if (targetManager != null && !targetManager.StarPowerUp)
            {
                StartCoroutine(collision.gameObject.GetComponent<Player>().hitByShell());
                if (rm.FrontCam.activeSelf)
                {
                    GameObject.Find("Main Camera").GetComponent<Animator>().SetTrigger("ShellHit");
                }
            }
            destroyShell();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (currentState != ShellState.Projectile)
        {
            return;
        }

        if (collision.gameObject.tag != "Ground" || collision.gameObject.tag != "Dirt")
        {
            grounded = true;
        }
        if (collision.gameObject.tag == "Ground" || collision.gameObject.tag == "Dirt")
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

        if (collision.gameObject.tag != "Ground" || collision.gameObject.tag != "Dirt")
        {
            grounded = false;
        }
        if (collision.gameObject.tag == "Ground" || collision.gameObject.tag == "Dirt")
        {
            antiGravityGrounded = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentState != ShellState.Trailing || ownerManager == null)
        {
            return;
        }

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
                ItemManager itemMgr = ownerManager as ItemManager;
                if (itemMgr != null) itemMgr.OnGreenShellTrailingConsumed(this);
                destroyShell();
            }
            return;
        }

        if (other.CompareTag("Player"))
        {
            ItemManager targetManager = other.GetComponent<ItemManager>();
            if (targetManager != null && targetManager != ownerManager && !targetManager.StarPowerUp)
            {
                StartCoroutine(other.GetComponent<Player>().hitByShell());
                ItemManager itemMgr = ownerManager as ItemManager;
                if (itemMgr != null) itemMgr.OnGreenShellTrailingConsumed(this);
                destroyShell();
            }
            return;
        }

        if (other.CompareTag("Banana") || other.CompareTag("Cow"))
        {
            ownerManager.OnGreenShellTrailingConsumed(this);
            destroyShell();
        }
    }

    public void destroyShell()
    {
        if (currentState == ShellState.Cooldown)
        {
            return;
        }

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

        sphereCollider.enabled = false;
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentState = ShellState.Cooldown;

        if (ownerManager == null)
        {
            Destroy(gameObject, 3f);
            return;
        }

        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
        }
        resetRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        SetMeshEnabled(false);
        yield return new WaitForSeconds(3f);
        EnsureRenderers(true);
        EnterInactive();
        ItemManager itemMgr = ownerManager as ItemManager;
        if (itemMgr != null) itemMgr.OnGreenShellReturned(this);
    }

    private void EnterInactive()
    {
        currentState = ShellState.Inactive;
        followParent = null;
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        sphereCollider.enabled = false;
        sphereCollider.isTrigger = false;
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
}
