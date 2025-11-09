using System;
using UnityEngine.InputSystem;

public class ItemInputHandler
{
    private readonly GameControls controls;

    public event Action UsePressed;
    public event Action UseReleased;

    public bool AimBackwardHeld => controls.Gameplay.AimBackward.IsPressed();
    public bool UseHeld => controls.Gameplay.UseItem.IsPressed();
    public bool WasPressedThisFrame => controls.Gameplay.UseItem.triggered;

    private bool useHeldLastFrame;

    public ItemInputHandler(GameControls controlsInstance)
    {
        controls = controlsInstance;
        controls.Gameplay.UseItem.performed += OnUsePerformed;
        controls.Gameplay.UseItem.canceled += OnUseCanceled;
    }

    public void Enable()
    {
        controls.Gameplay.Enable();
    }

    public void Disable()
    {
        controls.Gameplay.UseItem.performed -= OnUsePerformed;
        controls.Gameplay.UseItem.canceled -= OnUseCanceled;
        controls.Gameplay.Disable();
    }

    public void Update()
    {
        bool useHeldNow = UseHeld;
        if (useHeldLastFrame && !useHeldNow)
        {
            UseReleased?.Invoke();
        }

        useHeldLastFrame = useHeldNow;
    }

    private void OnUsePerformed(InputAction.CallbackContext context)
    {
        UsePressed?.Invoke();
    }

    private void OnUseCanceled(InputAction.CallbackContext context)
    {
        UseReleased?.Invoke();
    }
}

