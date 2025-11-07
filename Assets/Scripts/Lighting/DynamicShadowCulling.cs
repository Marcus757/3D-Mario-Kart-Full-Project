using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dynamically enables/disables shadows on lights based on distance from the player.
/// This improves performance by only casting shadows for nearby lights.
/// </summary>
public class DynamicShadowCulling : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Distance within which lights cast shadows")]
    public float shadowDistance = 100f;
    
    [Tooltip("How often to check light distances (seconds)")]
    public float updateInterval = 0.5f;
    
    [Tooltip("Maximum number of additional lights that can cast shadows at once")]
    public int maxShadowCastingLights = 8;
    
    private Light[] allLights;
    private Transform playerTransform;
    private float updateTimer = 0f;
    
    void Start()
    {
        // Find all lights in the scene
        allLights = FindObjectsOfType<Light>();
        
        // Find the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        Debug.Log($"[Shadow Culling] Found {allLights.Length} lights in scene");
    }
    
    void Update()
    {
        if (playerTransform == null || allLights == null) return;
        
        updateTimer += Time.deltaTime;
        
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateLightShadows();
        }
    }
    
    void UpdateLightShadows()
    {
        // Create a list of lights with their distances
        List<LightDistance> lightDistances = new List<LightDistance>();
        
        foreach (Light light in allLights)
        {
            if (light == null) continue;
            
            // Skip directional lights (main sun/moon)
            if (light.type == LightType.Directional) continue;
            
            float distance = Vector3.Distance(playerTransform.position, light.transform.position);
            lightDistances.Add(new LightDistance { light = light, distance = distance });
        }
        
        // Sort by distance (closest first)
        lightDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
        
        // Enable shadows only for the closest lights within range
        int shadowCount = 0;
        
        foreach (var ld in lightDistances)
        {
            if (ld.distance <= shadowDistance && shadowCount < maxShadowCastingLights)
            {
                // Enable shadows for close lights
                if (ld.light.shadows == LightShadows.None)
                {
                    ld.light.shadows = LightShadows.Soft;
                }
                shadowCount++;
            }
            else
            {
                // Disable shadows for distant lights
                if (ld.light.shadows != LightShadows.None)
                {
                    ld.light.shadows = LightShadows.None;
                }
            }
        }
    }
    
    private struct LightDistance
    {
        public Light light;
        public float distance;
    }
    
    // Visualize shadow distance in editor
    void OnDrawGizmosSelected()
    {
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, shadowDistance);
        }
    }
}

