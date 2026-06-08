using System.Collections;
using System.Threading;
using UnityEngine;

public class MoveToHelper : MonoBehaviour
{
    private bool _sync;
    private Transform _syncTarget;
    private int _resetDelay;
    private CancellationTokenSource _cancellation;
    private Vector3 currentvelocity;
    private float speed = 0.3f;
    private IEnumerator _lerp;

    public Transform headAnchor;
    private bool teleportThisObject;
    //public GameObject XROrigin;

    private bool UseConstantReset;
    public bool useAlternateMethod;
    private Transform ResetPos;

    private void LateUpdate()
    {
        if (UseConstantReset)
        {
            MoveToPositionInstantly(ResetPos);
        }
        if (teleportThisObject)
        {
            if (!useAlternateMethod)
            {
                MoveToPositionInstantly(headAnchor);
            }
            else
            {
                MoveToPositionRotateInstantly(headAnchor);
            }
        }
    }

    public void ChangeParent(Transform newParentTransform)
    {
        transform.parent = newParentTransform;
    }

    public void MoveToPosition(Transform moveToTransform)
    {
        if (_lerp != null) StopCoroutine(_lerp);
        _lerp = MoveToPositionAsync(moveToTransform);
        StartCoroutine(_lerp);

        if (teleportThisObject)
        {
            UseConstantReset = false;
            MoveToPositionInstantly(headAnchor);
        }
    }

    private IEnumerator MoveToPositionAsync(Transform moveToTransform)
    {
        var destination = moveToTransform;
        var lerpAmount = 0f;

        while (transform.position != destination.position)
        {
            lerpAmount = Mathf.Clamp01(lerpAmount += Time.deltaTime);
            transform.position = Vector3.SmoothDamp(transform.position, destination.position, ref currentvelocity, speed);
            transform.rotation = Quaternion.Lerp(transform.rotation, destination.rotation, lerpAmount * 0.1f);

            yield return null;
        }
    }

    public void MoveToPositionInstantly(Transform T)
    {
        transform.position = T.position;
    }

    public void MoveToPositionRotateInstantly(Transform T)
    {
        transform.SetPositionAndRotation(T.position, T.rotation);
    }



    public void TeleportThisObject()
    {
        FollowHeadAnchor();
        Debug.LogError("#101 : Teleport this object");
        this.GetComponent<GrabInteraction>().onGrabbed.AddListener(() => {
            StopFollowingHeadAnchor();
            Debug.LogError("#101 : 1212");
            SimulationManager.Instance.currentState.currentInteraction.canInteract = true;
        });
        //this.GetComponent<PointableUnityEventWrapper>().WhenUnselect.AddListener(a => { FollowHeadAnchor(); });

    }
    public void TeleportThisObjectTOHeadAnchor()
    {
        this.GetComponent<GrabInteraction>().onGrabbed.AddListener(() => {
            StopFollowingHeadAnchor();
            //SimulationManager.instance.currentState.canDetect = true;
            SimulationManager.Instance.currentState.currentInteraction.canInteract = true;

        });
        this.GetComponent<GrabInteraction>().onReleased.AddListener(() => {

            FollowHeadAnchor();
        });

    }

    public void FollowHeadAnchor()
    {
        teleportThisObject = true;
    }

    public void StopFollowingHeadAnchor()
    {
        teleportThisObject = false;
    }
    public void StartTransformSync(Transform target)
    {
        _sync = true;
        _syncTarget = target;
    }

    public void StopTransformSync()
    {
        _sync = false;
    }

    private void Update()
    {
        if (_sync)
        {
            MoveToPositionRotateInstantly(_syncTarget);
        }

    }
}