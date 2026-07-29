using UnityEngine;

/// <summary>
/// Interface for both player and AI item managers to provide common access points
/// </summary>
public interface IItemDriver
{
    Transform ForwardSpawn { get; }
    Transform BackSpawn { get; }
    Transform HeldParent { get; }
    Transform TrailingParent { get; }
    Transform ItemsStorage { get; }
    
    int CurrentWaypoint { get; }
    bool IsStarActive { get; }
    bool IsAntiGravity { get; }
    
    string DriverName { get; }
    GameObject DriverGameObject { get; }
    
    void TriggerThrowForward();
    void TriggerThrowBackward();
    void SetHasItem(bool hasItem);
    
    Rigidbody GetRigidbody();
}

























