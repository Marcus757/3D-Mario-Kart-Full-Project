using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputController : MonoBehaviour
{
    public PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        inputActions.Driving.Accelerate.performed += Accelerate_Performed;
        inputActions.Driving.Accelerate.canceled += Accelerate_Canceled;
        inputActions.Driving.HopDrift.performed += HopDrift_performed;
        inputActions.Driving.BrakeReverse.performed += BrakeReverse_Performed;
        inputActions.Driving.LookBackwards.performed += LookBackwards_Performed;
        inputActions.Driving.SteeringLeft.performed += SteeringLeft_Performed;
        inputActions.Driving.SteeringRight.performed += SteeringRight_Performed;
        inputActions.Driving.GliderGlideDown.performed += GliderGlideDown_Performed;
        inputActions.Driving.GliderGlideUp.performed += GliderGlideUp_Performed;
    }

    private void OnDestroy()
    {
        inputActions.Driving.Accelerate.performed -= Accelerate_Performed;
        inputActions.Driving.Accelerate.canceled -= Accelerate_Canceled;
        inputActions.Driving.HopDrift.performed -= HopDrift_performed;
        inputActions.Driving.BrakeReverse.performed -= BrakeReverse_Performed;
        inputActions.Driving.LookBackwards.performed -= LookBackwards_Performed;
        inputActions.Driving.SteeringLeft.performed -= SteeringLeft_Performed;
        inputActions.Driving.SteeringRight.performed -= SteeringRight_Performed;
        inputActions.Driving.GliderGlideDown.performed -= GliderGlideDown_Performed;
        inputActions.Driving.GliderGlideUp.performed -= GliderGlideUp_Performed;
    }

    private void OnEnable()
    {
        inputActions.Driving.Enable();
    }

    private void OnDisable()
    {
        inputActions.Driving.Disable();
    }

    private void Accelerate_Performed(InputAction.CallbackContext context)
    {
        Debug.Log("Accelerate_Performed");
    }

    private void Accelerate_Canceled(InputAction.CallbackContext context)
    {
        Debug.Log("Accelerate_Canceled");
    }

    private void HopDrift_performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void BrakeReverse_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void LookBackwards_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void SteeringLeft_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void SteeringRight_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void GliderGlideDown_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    private void GliderGlideUp_Performed(InputAction.CallbackContext context)
    {
        throw new NotImplementedException();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
}
