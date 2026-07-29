using System.Collections;
using UnityEngine;
using System;

public class Bobomb : MonoBehaviour
{
    private enum BobombState
    {
        Inactive,
        Held,
        Trailing,
        Projectile
    }

    [HideInInspector]
    public Rigidbody rb;

    public float throwForceUp;
    public float throwForceForward;

    public float lifetime;

    [HideInInspector]
    public float bounce_count = 1;
    public float bounceForce;
    public GameObject explosion;
    public Transform explosionPos;
    public Transform smokePos;
    public GameObject smoke;

    public SkinnedMeshRenderer[] renderers;
    public Material[] regMat;
    public Material glowMat;
    public GameObject[] spark;

    private bool exploded = false;
    private bool landed = false;

    bool countDownColor = false;

    [HideInInspector]
    public string whoThrewBomb;

    [Header("Throw Tuning")]
    [SerializeField, Range(0.1f, 4f)]
    private float throwForceMultiplier = 1f;
    [SerializeField]
    private bool autoCalibrate = false;
    [SerializeField]
    private bool matchArcAngle = false;
    [SerializeField, Range(0f, 80f)]
    private float desiredArcAngleDegrees = 30f;

    private Vector3 throwStartPosition;
    private bool trackingDistance;
    private bool distanceRecorded;

    private float baselineThrowDistance = -1f;

    // State machine
    private BobombState currentState = BobombState.Inactive;
    private IItemDriver ownerManager;
    private Transform followParent;
    private Vector3 initialLocalScale;
    private bool managedByItemManager;
    private Collider cachedCollider;

    // Held (drag) behaviour
    private bool isHeld;
    private bool heldFuseActive;
    private float heldFuseTimer;
    private Action heldExplosionCallback;

    public void ResetThrowCalibration()
    {
        baselineThrowDistance = -1f;
        throwForceMultiplier = 1f;
    }

    [ContextMenu("Reset Throw Calibration")]
    void ContextResetThrowCalibration()
    {
        ResetThrowCalibration();
        Debug.Log("[Bobomb] Throw calibration reset.");
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();
        initialLocalScale = transform.localScale;
        if (renderers != null && renderers.Length > 1 && regMat != null && regMat.Length > 1)
        {
            renderers[1].material = regMat[1];
        }
        if (renderers != null && renderers.Length > 0 && regMat != null && regMat.Length > 0)
        {
            renderers[0].material = regMat[0];
        }
    }

    void Start()
    {
        if (!managedByItemManager)
        {
            currentState = BobombState.Projectile;
        }
    }

    void Update()
    {
        if (heldFuseActive)
        {
            heldFuseTimer -= Time.deltaTime;
            if (heldFuseTimer <= 0f)
            {
                HandleHeldExplosion();
            }
        }
    }

    void FixedUpdate()
    {
        if (currentState == BobombState.Held || currentState == BobombState.Trailing)
        {
            return;
        }

        if (rb != null)
        {
            rb.AddRelativeForce(Vector3.down * 10000 * Time.deltaTime, ForceMode.Acceleration);
        }

        if (landed)
        {
            groundNormalRotation();
            if (!countDownColor)
            {
                StartCoroutine(countdownColor());
                countDownColor = true;
            }
        }

        if (exploded)
        {
            AudioSource audio = GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.Stop();
            }
        }
    }

    private void LateUpdate()
    {
        if (currentState == BobombState.Trailing && followParent != null)
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
        return currentState == BobombState.Inactive || currentState == BobombState.Held;
    }

    public void EnterHeld(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        currentState = BobombState.Held;
        followParent = null;
        isHeld = true;
        managedByItemManager = true;

        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }

        landed = false;
        exploded = false;
        bounce_count = 1;
        heldFuseActive = false;
        heldExplosionCallback = null;
    }

    public void EnterTrailing(Transform parent)
    {
        if (ownerManager == null)
        {
            return;
        }

        currentState = BobombState.Trailing;
        followParent = parent;
        isHeld = true;
        managedByItemManager = true;

        gameObject.SetActive(true);
        transform.SetParent(parent, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = initialLocalScale;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
            cachedCollider.isTrigger = true;
        }

        BeginHeld(OnTrailingExplosion);
    }

    public void EnterProjectile(Vector3 position, Quaternion rotation, float extraForward, string ownerName)
    {
        currentState = BobombState.Projectile;
        managedByItemManager = false;
        followParent = null;
        isHeld = false;
        heldFuseActive = false;
        heldExplosionCallback = null;

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

        whoThrewBomb = ownerName;
        landed = false;
        exploded = false;
        bounce_count = 1;
        countDownColor = false;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                }
            }
        }

        if (spark != null)
        {
            for (int i = 0; i < spark.Length; i++)
            {
                if (spark[i] != null)
                {
                    spark[i].SetActive(true);
                }
            }
        }

        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
        }

        bomb_thrown(extraForward);
    }

    public void EnterMine(Vector3 position, Quaternion rotation, string ownerName)
    {
        currentState = BobombState.Projectile;
        managedByItemManager = false;
        followParent = null;
        isHeld = false;
        heldFuseActive = false;
        heldExplosionCallback = null;

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

        whoThrewBomb = ownerName;
        bounce_count = 4;
        landed = false;
        exploded = false;
        countDownColor = false;

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                }
            }
        }

        if (spark != null)
        {
            for (int i = 0; i < spark.Length; i++)
            {
                if (spark[i] != null)
                {
                    spark[i].SetActive(true);
                }
            }
        }

        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null)
        {
            audio.enabled = true;
            audio.Play();
        }
    }

    public void EnterInactive()
    {
        currentState = BobombState.Inactive;
        followParent = null;
        isHeld = false;
        heldFuseActive = false;
        heldExplosionCallback = null;
        lifetime = 0f;
        landed = false;
        exploded = false;
        bounce_count = 1;
        countDownColor = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
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

    private void OnTrailingExplosion()
    {
        if (ownerManager != null)
        {
            ItemManager itemMgr = ownerManager as ItemManager;
            if (itemMgr != null)
            {
                itemMgr.OnBobombTrailingExploded(this);
            }
        }
    }

    public void bomb_thrown(float extraForward)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        float multiplier = throwForceMultiplier;
        const float legacyScale = 1f / 60f;

        float forwardImpulse = (throwForceForward + extraForward) * legacyScale * multiplier;
        float verticalImpulse = throwForceUp * legacyScale * multiplier;

        if (matchArcAngle)
        {
            float baseRatio = throwForceForward > Mathf.Epsilon ? throwForceUp / throwForceForward : 0f;
            float desiredRatio = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(desiredArcAngleDegrees, 0f, 80f));
            if (baseRatio > Mathf.Epsilon)
            {
                float arcMultiplier = desiredRatio / baseRatio;
                verticalImpulse *= arcMultiplier;
            }
        }

        if (rb != null)
        {
            rb.AddForce(transform.up * verticalImpulse, ForceMode.Impulse);
            rb.AddForce(-transform.forward * forwardImpulse, ForceMode.Impulse);
        }

        throwStartPosition = transform.position;
        trackingDistance = true;
        distanceRecorded = false;
    }

    void groundNormalRotation()
    {
        Ray ground = new Ray(transform.position, transform.InverseTransformDirection(Vector3.down));
        RaycastHit hit;
        if (Physics.Raycast(ground, out hit, 5))
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(transform.up * 2, hit.normal) * transform.rotation, 9f * Time.deltaTime);
        }
    }

    private IEnumerator OnCollisionEnter(Collision collision)
    {
        if (currentState != BobombState.Projectile)
        {
            yield break;
        }

        if (collision.gameObject.tag == "Ground" || collision.gameObject.tag == "Dirt")
        {
            groundNormalRotation();
            RecordThrowDistance();

            if (bounce_count < 4)
            {
                const float legacyScale = 1f / 60f;
                if (rb != null)
                {
                    rb.AddRelativeForce(transform.InverseTransformDirection(transform.up) * bounceForce / (bounce_count * 1.5f) * legacyScale, ForceMode.Impulse);
                }
                yield return new WaitForSeconds(0.01f);
                bounce_count++;
            }
            if (bounce_count == 4)
            {
                StartCoroutine(Explode());
                landed = true;
            }
        }

        if (collision.gameObject.tag == "Player" || collision.gameObject.tag == "Opponent")
        {
            RecordThrowDistance();
            StartCoroutine(explodeImmediately());
        }
    }

    IEnumerator Explode()
    {
        yield return new WaitForSeconds(4);
        if (!exploded)
        {
            heldFuseActive = false;
            RecordThrowDistance();
            GameObject clone = Instantiate(explosion, explosionPos.position, explosion.transform.rotation);

            Instantiate(smoke, smokePos.position, smokePos.rotation);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
            for (int i = 0; i < spark.Length; i++)
            {
                spark[i].SetActive(false);
            }
            exploded = true;
            if (Vector3.Distance(GameObject.FindGameObjectWithTag("Player").transform.position, transform.position) < 250)
            {
                clone.GetComponent<AudioSource>().Play();
                Camera.main.GetComponent<Animator>().SetTrigger("Shake2");
            }
            GetComponent<AudioSource>().Stop();
        }
        yield return new WaitForSeconds(2);
        gameObject.SetActive(false);
    }

    IEnumerator explodeImmediately()
    {
        if (!exploded)
        {
            heldFuseActive = false;
            RecordThrowDistance();
            GameObject clone = Instantiate(explosion, explosionPos.position, explosion.transform.rotation);
            Instantiate(smoke, smokePos.position, smokePos.rotation);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
            for (int i = 0; i < spark.Length; i++)
            {
                spark[i].SetActive(false);
            }
            exploded = true;
            if (Vector3.Distance(GameObject.FindGameObjectWithTag("Player").transform.position, transform.position) < 250)
            {
                clone.GetComponent<AudioSource>().Play();
                try
                {
                    Camera.main.GetComponent<Animator>().SetTrigger("Shake2");
                }
                catch (Exception)
                {
                }
            }
        }
        yield return new WaitForSeconds(2);
        gameObject.SetActive(false);
    }

    void RecordThrowDistance()
    {
        if (!trackingDistance || distanceRecorded)
        {
            return;
        }

        Vector3 start = throwStartPosition;
        Vector3 end = transform.position;
        start.y = 0f;
        end.y = 0f;
        float distance = Vector3.Distance(start, end);

        trackingDistance = false;
        distanceRecorded = true;

        if (distance <= Mathf.Epsilon)
        {
            return;
        }

        if (baselineThrowDistance < 0f)
        {
            baselineThrowDistance = distance;
            throwForceMultiplier = 1f;
            Debug.Log($"[Bobomb] Baseline throw distance recorded: {distance:F2} units.");
        }
        else if (autoCalibrate)
        {
            float desired = baselineThrowDistance;
            float newMultiplier = desired / distance;
            throwForceMultiplier = Mathf.Clamp(newMultiplier, 0.1f, 4f);
            Debug.Log($"[Bobomb] Throw distance {distance:F2} units. Adjusting multiplier to {throwForceMultiplier:F2} to match baseline {desired:F2}.");
        }
    }

    IEnumerator countdownColor()
    {
        while (!exploded)
        {
            if (renderers != null && renderers.Length > 1 && glowMat != null)
            {
                renderers[1].material = glowMat;
            }
            yield return new WaitForSeconds(0.2f);
            if (renderers != null && renderers.Length > 1 && regMat != null && regMat.Length > 1)
            {
                renderers[1].material = regMat[1];
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void HandleHeldExplosion()
    {
        if (exploded)
        {
            heldFuseActive = false;
            return;
        }

        heldFuseActive = false;
        isHeld = false;
        transform.SetParent(null, true);
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        heldExplosionCallback?.Invoke();
        heldExplosionCallback = null;

        StartCoroutine(explodeImmediately());
    }

    public void BeginHeld(Action onHeldExplosion)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        isHeld = true;
        heldFuseActive = true;
        heldFuseTimer = throwForceMultiplier > 1f ? 2f / throwForceMultiplier : 2f;
        heldExplosionCallback = onHeldExplosion;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        landed = false;
        exploded = false;
        bounce_count = 1;
    }

    public void ReleaseHeldAsMine()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        isHeld = false;
        if (rb != null)
        {
            rb.isKinematic = false;
        }
        transform.SetParent(null, true);
        bounce_count = 4;
        heldExplosionCallback = null;
    }

    public void CancelHeld()
    {
        isHeld = false;
        heldFuseActive = false;
        heldExplosionCallback = null;
    }

    public void ApplyDebugThrowSettings(float multiplier, bool matchArc, float desiredAngleDegrees, bool autoCal)
    {
        throwForceMultiplier = Mathf.Clamp(multiplier, 0.1f, 4f);
        matchArcAngle = matchArc;
        desiredArcAngleDegrees = Mathf.Clamp(desiredAngleDegrees, 0f, 80f);
        autoCalibrate = autoCal;
        baselineThrowDistance = -1f;
    }
}
