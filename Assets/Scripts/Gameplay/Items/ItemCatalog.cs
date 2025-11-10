using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemCatalog : MonoBehaviour
{
    public static ItemCatalog Instance { get; private set; }

    [SerializeField]
    private ItemDefinition[] itemDefinitions = System.Array.Empty<ItemDefinition>();

    private readonly Dictionary<string, int> indexByName = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> sanitizedToCanonical = new Dictionary<string, string>();
    private readonly Dictionary<string, Sprite> iconByName = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> heldPrefabByName = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> worldPrefabByName = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate ItemCatalog detected on '{name}'. Destroying the newest instance.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Initialize();
    }
#endif

    public void Initialize()
    {
        indexByName.Clear();
        sanitizedToCanonical.Clear();
        iconByName.Clear();
        heldPrefabByName.Clear();
        worldPrefabByName.Clear();

        for (int i = 0; i < DefinitionCount; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition == null)
            {
                continue;
            }

            string canonicalName = GetCanonicalName(definition);
            if (string.IsNullOrEmpty(canonicalName))
            {
                continue;
            }

            if (!indexByName.ContainsKey(canonicalName))
            {
                indexByName.Add(canonicalName, i);
            }

            string sanitized = SanitizeName(canonicalName);
            if (!string.IsNullOrEmpty(sanitized))
            {
                sanitizedToCanonical[sanitized] = canonicalName;
            }

            if (definition.icon != null && !iconByName.ContainsKey(canonicalName))
            {
                iconByName.Add(canonicalName, definition.icon);
            }

            GameObject heldPrefab = definition.handPrefab != null ? definition.handPrefab : definition.prefab;
            if (heldPrefab != null && !heldPrefabByName.ContainsKey(canonicalName))
            {
                heldPrefabByName.Add(canonicalName, heldPrefab);
            }

            GameObject worldPrefab = definition.prefab != null ? definition.prefab : definition.alternatePrefab;
            if (worldPrefab != null && !worldPrefabByName.ContainsKey(canonicalName))
            {
                worldPrefabByName.Add(canonicalName, worldPrefab);
            }
        }

        initialized = true;
    }

    public int DefinitionCount => itemDefinitions != null ? itemDefinitions.Length : 0;

    public ItemDefinition GetDefinition(int index)
    {
        EnsureInitialized();

        if (index < 0 || index >= DefinitionCount)
        {
            return null;
        }

        return itemDefinitions[index];
    }

    public ItemDefinition GetDefinition(string canonicalName)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(canonicalName))
        {
            return null;
        }

        if (indexByName.TryGetValue(canonicalName, out int directIndex))
        {
            return GetDefinition(directIndex);
        }

        string sanitized = SanitizeName(canonicalName);
        if (!string.IsNullOrEmpty(sanitized) && sanitizedToCanonical.TryGetValue(sanitized, out string canonical))
        {
            return indexByName.TryGetValue(canonical, out int index) ? GetDefinition(index) : null;
        }

        return null;
    }

    public int GetIndex(string canonicalName)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(canonicalName))
        {
            return -1;
        }

        if (indexByName.TryGetValue(canonicalName, out int directIndex))
        {
            return directIndex;
        }

        string sanitized = SanitizeName(canonicalName);
        if (!string.IsNullOrEmpty(sanitized) && sanitizedToCanonical.TryGetValue(sanitized, out string canonical))
        {
            return indexByName.TryGetValue(canonical, out int index) ? index : -1;
        }

        return -1;
    }

    public string GetCanonicalName(ItemDefinition definition)
    {
        if (definition == null)
        {
            return null;
        }

        string debugName = GetDebugName(definition.debugSelection);
        if (!string.IsNullOrEmpty(debugName))
        {
            return debugName;
        }

        if (definition.handPrefab != null)
        {
            return definition.handPrefab.name;
        }

        if (definition.prefab != null)
        {
            return definition.prefab.name;
        }

        return null;
    }

    public string GetCanonicalName(int index)
    {
        return GetCanonicalName(GetDefinition(index));
    }

    public Sprite GetIcon(int index)
    {
        EnsureInitialized();

        ItemDefinition definition = GetDefinition(index);
        return definition != null ? definition.icon : null;
    }

    public Sprite GetIcon(string canonicalName)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(canonicalName))
        {
            return null;
        }

        if (iconByName.TryGetValue(canonicalName, out Sprite direct))
        {
            return direct;
        }

        string sanitized = SanitizeName(canonicalName);
        if (!string.IsNullOrEmpty(sanitized) && sanitizedToCanonical.TryGetValue(sanitized, out string canonical))
        {
            iconByName.TryGetValue(canonical, out direct);
            return direct;
        }

        return null;
    }

    public GameObject InstantiateHeldVisual(int index, Transform parent)
    {
        EnsureInitialized();
        ItemDefinition definition = GetDefinition(index);
        return InstantiateHeldVisual(definition, parent);
    }

    public GameObject InstantiateHeldVisual(string canonicalName, Transform parent)
    {
        EnsureInitialized();
        ItemDefinition definition = GetDefinition(canonicalName);
        return InstantiateHeldVisual(definition, parent);
    }

    public GameObject GetWorldPrefab(string canonicalName)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(canonicalName))
        {
            return null;
        }

        if (worldPrefabByName.TryGetValue(canonicalName, out GameObject prefab))
        {
            return prefab;
        }

        string sanitized = SanitizeName(canonicalName);
        if (!string.IsNullOrEmpty(sanitized) && sanitizedToCanonical.TryGetValue(sanitized, out string canonical))
        {
            worldPrefabByName.TryGetValue(canonical, out prefab);
            return prefab;
        }

        ItemDefinition definition = GetDefinition(canonicalName);
        if (definition == null)
        {
            return null;
        }

        return definition.prefab != null ? definition.prefab : definition.alternatePrefab;
    }

    public GameObject GetWorldPrefab(DebugItemSelection selection)
    {
        EnsureInitialized();

        string canonicalName = GetDebugName(selection);
        if (string.IsNullOrEmpty(canonicalName))
        {
            return null;
        }

        return GetWorldPrefab(canonicalName);
    }

    private GameObject InstantiateHeldVisual(ItemDefinition definition, Transform parent)
    {
        if (definition == null)
        {
            return null;
        }

        GameObject source = definition.handPrefab != null ? definition.handPrefab : definition.prefab;
        if (source == null)
        {
            return null;
        }

        Transform targetParent = parent != null ? parent : transform;
        GameObject instance = Instantiate(source, targetParent);
        instance.name = GetCanonicalName(definition) ?? source.name;
        return instance;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private static string GetDebugName(DebugItemSelection selection)
    {
        switch (selection)
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
            default:
                return null;
        }
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}

