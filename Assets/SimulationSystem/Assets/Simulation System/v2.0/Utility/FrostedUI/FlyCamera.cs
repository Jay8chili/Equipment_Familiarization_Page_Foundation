using UnityEngine;

public class FlyCamera : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 2.5f;
    public float climbSpeed = 5f;

    [Header("Look")]
    public float lookSensitivity = 2f;
    public float maxLookX = 90f;
    public bool lockCursor = true;

    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        rotationY = euler.y;
        rotationX = euler.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleCursorToggle();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -maxLookX, maxLookX);

        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }

    private void HandleMovement()
    {
        float currentSpeed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= sprintMultiplier;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;

        if (Input.GetKey(KeyCode.E))
        {
            move += Vector3.up;
        }

        if (Input.GetKey(KeyCode.Q))
        {
            move += Vector3.down;
        }

        transform.position += move * currentSpeed * Time.deltaTime;
    }

    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}