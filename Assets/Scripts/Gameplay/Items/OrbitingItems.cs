using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitingItems : MonoBehaviour
{
    [SerializeField]
    private GameObject owner;

    private string ownerName;
    [SerializeField]
    private bool enableDebugLogs;

    private static bool globalDebugLogs;

    public static void SetGlobalDebugLogging(bool enabled)
    {
        globalDebugLogs = enabled;
    }

    private bool ShouldLog => enableDebugLogs || globalDebugLogs;

    private void Awake()
    {
        if (owner == null)
        {
            owner = ResolveOwnerFromHierarchy();
        }

        ownerName = owner != null ? owner.name : null;

        if (ShouldLog)
        {
            Debug.Log($"[OrbitingItems] Owner resolved to '{ownerName ?? "null"}' for '{name}'", this);
        }
    }

    private void Update()
    {
        if (owner == null)
        {
            owner = ResolveOwnerFromHierarchy();
            ownerName = owner != null ? owner.name : ownerName;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null)
        {
            if (ShouldLog)
            {
                Debug.Log($"[OrbitingItems] No owner assigned, ignoring collision with '{other.name}'", this);
            }
            return;
        }

        if (other.gameObject == owner)
        {
            return;
        }

        ItemManager ownerItemManager = owner.GetComponent<ItemManager>();
        OpponentItemManager ownerOpponentManager = owner.GetComponent<OpponentItemManager>();

        if (ownerItemManager != null)
        {
        OpponentItemManager targetOpponent = other.GetComponent<OpponentItemManager>();
        if (targetOpponent == null)
        {
            targetOpponent = other.GetComponentInParent<OpponentItemManager>();
        }
            if (targetOpponent != null)
            {
            if (ShouldLog)
            {
                Debug.Log($"[OrbitingItems] '{ownerName}' orbiting hit opponent '{other.name}'", this);
            }
                if (CompareTag("Shell"))
                {
                    targetOpponent.hitByShell();
                }
                else
                {
                    targetOpponent.hitByBanana();
                }

                ownerItemManager.HandleOrbitingItemConsumed(gameObject);
            }
            return;
        }

        if (ownerOpponentManager != null)
        {
            // TODO: handle opponent-owned orbiting items if needed
        }
    }

    public void SetOwner(GameObject ownerObject)
    {
        owner = ownerObject;
        ownerName = owner != null ? owner.name : null;
    }

    private GameObject ResolveOwnerFromHierarchy()
    {
        Transform current = transform;
        for (int i = 0; i < 6 && current != null; i++)
        {
            if (current.GetComponent<ItemManager>() != null || current.CompareTag("Player"))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        if (!string.IsNullOrEmpty(ownerName))
        {
            var candidate = GameObject.Find(ownerName);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }
}
