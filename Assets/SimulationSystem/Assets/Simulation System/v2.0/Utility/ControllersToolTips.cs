using UnityEngine;
using System;
using UnityEngine.Events;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.InputSystem.XR;
public class ControllersToolTips : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject target1, target2;

    [Tooltip("Max distance the ray should travel")]
    public float maxDistance = 100f;


    public static Action<bool> ShowControllers;
    public UnityEvent OnControllersShow, OnControllersHide;

    public static bool AreRefrencesIntact;


    [Header("ControllerFollowFields")]
    public Vector3 PosOffset;
    public GameObject LeftfollowPrefab,RightFollowPrefab, OffsetGO;
    public GameObject XrRig;

    public GameObject LeftControllerParent, RightControllerParent;

    public static ControllersToolTips Instance;

    private bool AlreadyOn;
    private void Awake()
    {
        LeftControllerParent.transform.GetChild(0).transform.eulerAngles = new Vector3(0   , 180, 0);
        RightControllerParent.transform.GetChild(0).transform.eulerAngles = new Vector3(0, 180, 0);
    }

    private void Start()
    {
        StartCoroutine(DetectTarget());
        StartCoroutine(Follow());
    }
    private void OnEnable()
    {
        ShowControllers += ShowController; 
    }


    private void OnDisable()
    {
        ShowControllers -= ShowController;
    }
    private void ShowController(bool a)
    {
      
            //show the Controllers
            if (a)
            {
                if (!AlreadyOn)
                {
                    AlreadyOn = true;
                    OnControllersShow?.Invoke();
                }
            }
            else if (!a)
            {
                if (AlreadyOn)
                {
                    AlreadyOn = false;
                    OnControllersHide?.Invoke();
                }
            }
        
    }
    
    private void UpdateFollowForControllers()
    {

    }
    public IEnumerator Follow()
    {
        yield return null;
        while (true)
        {


            if (RightFollowPrefab && OffsetGO && XrRig)
            {
                PosOffset = OffsetGO.transform.position;
                RightControllerParent.transform.position = RightFollowPrefab.transform.position + (-PosOffset);
                RightControllerParent.transform.rotation = (RightFollowPrefab.transform.rotation);
                yield return null;
            }

            if (LeftfollowPrefab && OffsetGO && XrRig)
            {
                PosOffset = OffsetGO.transform.position;
                LeftControllerParent.transform.position = LeftfollowPrefab.transform.position + (-PosOffset);
                LeftControllerParent.transform.rotation = (LeftfollowPrefab.transform.rotation);
                yield return null;
            }


           
            
                this.transform.rotation = XrRig.transform.rotation;
            yield return null;
        }
      
    }
    IEnumerator DetectTarget()
    {
        while (true)
        {
            if (mainCamera != null)
            {
                // 1. Setup Ray
                Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
                RaycastHit hitInfo;

                // Track if we found a valid target this frame
                bool isHittingTarget = false;

                // 2. Perform Raycast
                if (Physics.Raycast(ray, out hitInfo, maxDistance))
                {
                    // Case A: We hit something. Is it one of our targets?
                    if (hitInfo.collider.gameObject == target1 || hitInfo.collider.gameObject == target2)
                    {
                        isHittingTarget = true;
                        Debug.DrawLine(ray.origin, hitInfo.point, Color.green);
                    }
                    else
                    {
                        // Case B: We hit a wall/floor (not a target)
                        isHittingTarget = false;
                        Debug.DrawLine(ray.origin, hitInfo.point, Color.red);
                    }
                }
                else
                {
                    // Case C: We hit nothing (looking at sky/void)
                    // THIS was the missing logic in your original code
                    isHittingTarget = false;
                    Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.yellow);
                }

                // 3. Execute Logic based on the result
                if (isHittingTarget)
                {
                    ShowControllers?.Invoke(true);
                    // Debug.Log("#101 SHOWING"); 
                }
                else
                {
                    ShowControllers?.Invoke(false);
                    // Debug.Log("#101 HIDDEN");
                }
            }

            yield return null;
        }

    }
}

