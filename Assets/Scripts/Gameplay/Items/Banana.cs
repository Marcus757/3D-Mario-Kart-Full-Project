using UnityEngine;

public class Banana : MonoBehaviour
{
    private enum BananaState
    {
        Inactive,
        Held,
        Trailing,
        Projectile
    }

    [HideInInspector] public Rigidbody rb;

    public float throwForceUp;
    public float throwForceForward;

    public float lifetime;

    [HideInInspector]
    public string whoThrewBanana;

    private IItemDriver ownerManager;
    private Transform followParent;
    private Collider cachedCollider;
    private Vector3 initialLocalScale;
    private BananaState currentState = BananaState.Inactive;
    private bool managedByItemManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        initialLocalScale = transform.localScale;
    }

    private void OnEnable()
    {
        if (!managedByItemManager)
        {
            currentState = BananaState.Projectile;
        }
    }

    private void OnDisable()
    {
        followParent = null;

        if (managedByItemManager && ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null) itemMgr.OnBananaReturned(this);
        }
    }

    private void FixedUpdate()
    {
        if (currentState != BananaState.Projectile)
        {
            return;
        }

        Move();
        lifetime += Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (currentState == BananaState.Trailing && followParent != null)
        {
            transform.position = followParent.position;
            transform.rotation = followParent.rotation;
        }
    }

    public void Initialize(IItemDriver owner)
    {
        ownerManager = owner;
        managedByItemManager = true;
        EnterInactive();
    }

    public bool IsAvailable()
    {
        return currentState == BananaState.Inactive || currentState == BananaState.Held;
    }

    public void EnterHeld(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        managedByItemManager = true;
        currentState = BananaState.Held;
        followParent = null;

        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = false;
        }
    }

    public void EnterTrailing(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        managedByItemManager = true;
        currentState = BananaState.Trailing;
        followParent = parent;

        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = false;
        }
    }

    public void EnterProjectile(Vector3 position, Quaternion rotation, float extraForward, string ownerName)
    {
        currentState = BananaState.Projectile;
        managedByItemManager = false;
        followParent = null;

        gameObject.SetActive(true);
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = initialLocalScale;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = false;
        }

        whoThrewBanana = ownerName;
        Banana_thrown(extraForward);
    }

    public void EnterMine(Vector3 position, Quaternion rotation, string ownerName)
    {
        currentState = BananaState.Projectile;
        managedByItemManager = false;
        followParent = null;

        gameObject.SetActive(true);
        transform.SetParent(null);
        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = initialLocalScale;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = false;
        }

        whoThrewBanana = ownerName;
    }

    public void EnterInactive()
    {
        currentState = BananaState.Inactive;
        followParent = null;
        lifetime = 0f;
        managedByItemManager = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = false;
        }

        Transform storageParent = null;
        if (ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null) storageParent = itemMgr.GreenShellStorage;
        }
        if (storageParent != null)
        {
            transform.SetParent(storageParent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = initialLocalScale;
        }

        if (managedByItemManager)
        {
            gameObject.SetActive(false);
        }
    }

    public void ReturnToPool()
    {
        EnterInactive();
    }

    public void DetachFromParent()
    {
        followParent = null;
        transform.SetParent(null);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("fence"))
        {
            return;
        }

        groundNormalRotation();

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("LandGround");
        }
    }

    public void Move()
    {
        if (rb != null)
        {
            rb.AddRelativeForce(Vector3.down * 10000 * Time.deltaTime, ForceMode.Acceleration);
        }
    }

    public void Banana_thrown(float extraForward)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            return;
        }

        rb.AddForce(transform.up * throwForceUp * Time.deltaTime, ForceMode.Impulse);
        rb.AddForce(-transform.forward * (throwForceForward + extraForward) * Time.deltaTime, ForceMode.Impulse);
    }

    private void groundNormalRotation()
    {
        Ray ground = new Ray(transform.position, transform.InverseTransformDirection(Vector3.down));
        if (Physics.Raycast(ground, out RaycastHit hit, 10f))
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.FromToRotation(transform.up * 2f, hit.normal) * transform.rotation,
                9f * Time.deltaTime);
        }
    }
}
