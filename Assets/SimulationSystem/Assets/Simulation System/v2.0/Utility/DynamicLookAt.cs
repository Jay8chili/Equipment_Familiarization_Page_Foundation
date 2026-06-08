using UnityEngine;
    public class DynamicLookAt : MonoBehaviour
    {
        [Header("Attach transform to override center camera")]
        public GameObject followTransform;
        public bool rotateAlongY;
        private GameObject _followTransform;

        private void Start()
        {
            if (followTransform)
                _followTransform = followTransform;
            else
                _followTransform = Camera.main.gameObject;
            
        }

        private void LateUpdate()
        {
            DynamicRotate();
        }

        private void DynamicRotate()
        {
            Vector3 direction = (_followTransform.transform.position - transform.position).normalized;
            if (rotateAlongY)
            {
                direction = new Vector3(direction.x, 0f, direction.z);
            }
            Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
            transform.rotation = Quaternion.Euler(rotation.eulerAngles.x, rotation.eulerAngles.y, rotation.eulerAngles.z);
        }
    }
