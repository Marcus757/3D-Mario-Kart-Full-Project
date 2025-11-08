using UnityEngine;

[System.Serializable]
public class ItemDefinition
{
    [UnityEngine.Header("Item Selection")]
    public DebugItemSelection debugSelection = DebugItemSelection.None;
    [UnityEngine.Header("Primary References")]
    public Sprite icon;
    public GameObject prefab;
    public GameObject handPrefab;
    [UnityEngine.Header("Optional References")]
    public GameObject alternatePrefab;
    public Transform spawnPoint;
}


