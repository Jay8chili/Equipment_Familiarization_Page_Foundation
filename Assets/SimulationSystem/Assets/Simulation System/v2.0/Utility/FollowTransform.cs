
using UnityEngine;

public class FollowTransform : MonoBehaviour
{
	#region Variables
	[Tooltip("Assign the object to follow")]
	public Vector3 OffsetForRotation;
	public Vector3 OffsetForPostion;
    [SerializeField] private Transform _target;
	[SerializeField] private bool rotationAlongYAxis;
    [SerializeField] private Transform MainCam;
	#endregion

	#region Unity Methods

	private void LateUpdate()
    {
        FollowTarget();
        DynamicRotate();
    }
    #endregion

    #region Helper Methods
    private void FollowTarget()
	{
		if (_target == null) return;
		transform.position = _target.position;
        transform.position = transform.position + OffsetForPostion;

        //transform.rotation = _target.rotation;
    }
    private void DynamicRotate()
    {
        Vector3 direction = (MainCam.transform.position - transform.position).normalized;
        if (rotationAlongYAxis)
        {
            direction = new Vector3(direction.x, 0f, direction.z);
        }
        Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
        transform.rotation = Quaternion.Euler(rotation.eulerAngles.x, rotation.eulerAngles.y, rotation.eulerAngles.z);
        transform.eulerAngles = transform.rotation.eulerAngles + (OffsetForRotation);
    }

    #endregion
}
