using UnityEngine.InputSystem;
using UnityEngine;

public class ControllerHandAnimationManager : MonoBehaviour
{

    public InputActionReference triggerActionRefrence;
    public InputActionReference gripActionRefrence;


    public Animator HandAnimator;

    private void Awake()
    {
         HandAnimator = GetComponent<Animator>();
        SetupInputAction();
    }


    private void SetupInputAction()
    {
        if (!triggerActionRefrence)
        {
            Debug.LogError("Please assign triggerActionRefrence in inspector on " + transform.name + "GameObject");
            return;
        }
        else
        {
            //Trigger is already defined as a parameter which is used by the Blend Tree so make sure to not change it upon creating a new/ Modifying the existing Blend tree
            triggerActionRefrence.action.performed += callbackContext => UpdateHandAnimaton("Trigger", callbackContext.ReadValue<float>()) ;
            triggerActionRefrence.action.canceled += callbackContext => UpdateHandAnimaton("Trigger", 0);
        }

        if (!gripActionRefrence)
        {
            Debug.LogError("Please assign gripActionRefrence in inspector on " + transform.name + "GameObject");
            return;

        }
        else
        {
            //Girp is already defined as a parameter which is used by the Blend Tree so make sure to not change it upon creating a new/ Modifying the existing Blend tree
            gripActionRefrence.action.performed += callbackContext => UpdateHandAnimaton("Grip", callbackContext.ReadValue<float>());
            gripActionRefrence.action.canceled += callbackContext => UpdateHandAnimaton("Grip", 0);
        }

        if (!HandAnimator)
        {
            Debug.LogError("Please make sure there is an animation Controller and animator on " + transform.name + "GameObject");
            return;

        }
    }


    private void DerigesterInputAction()
    {
        //Mostly we dont need this at all. But just in case!!

            //Trigger is already defined as a parameter which is used by the Blend Tree So make sure to not change it upon creating a new/ Modifying the existing Blend tree
            triggerActionRefrence.action.performed -= callbackContext => UpdateHandAnimaton("Trigger", callbackContext.ReadValue<float>());
            triggerActionRefrence.action.canceled -= callbackContext => UpdateHandAnimaton("Trigger", 0);
        

       
            //Girp is already defined as a parameter which is used by the Blend Tree so make sure to not change it upon creating a new/ Modifying the existing Blend tree
            gripActionRefrence.action.performed -= callbackContext => UpdateHandAnimaton("Grip", callbackContext.ReadValue<float>());
            gripActionRefrence.action.canceled -= callbackContext => UpdateHandAnimaton("Grip", 0);
                
    }

   

    private void UpdateHandAnimaton(string parameterName, float value)
    {
        HandAnimator.SetFloat(parameterName, value);
    }

    private void OnEnable()
    {
        triggerActionRefrence?.action.Enable();
        gripActionRefrence?.action.Enable();

    }

    private void OnDisable()
    {
        triggerActionRefrence?.action.Disable();
        gripActionRefrence?.action.Disable();
        DerigesterInputAction();
    }
}
