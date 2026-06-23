using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class testsetmode : MonoBehaviour
{
    public InputActionReference KeyToSetMode;


    private void OnEnable()
    {
        KeyToSetMode.action.performed += SetMode;
    }

    private void SetMode(InputAction.CallbackContext context)
    {
        SimulationManager.Instance.SetMode(0);
    }
}
