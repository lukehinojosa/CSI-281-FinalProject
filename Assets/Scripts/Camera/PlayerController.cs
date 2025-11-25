using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 8f;
    public float rotationSpeed = 150f;
    public float gravity = -15.0f;
    
    // Smoothing for Orthographic turning
    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    private CharacterController controller;
    private bool isPerspectiveMode = false;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void SetControlMode(bool isPerspective)
    {
        isPerspectiveMode = isPerspective;
        // Lock cursor if perspective, unlock if orthographic
        Cursor.lockState = isPerspective ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isPerspective;
    }

    void Update()
    {
        // 1. Inputs
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        
        // 2. Gravity Logic
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = Vector3.zero;

        if (isPerspectiveMode)
        {
            // Perspective mode

            // Rotation
            float mouseX = Input.GetAxis("Mouse X");
            transform.Rotate(Vector3.up * mouseX * rotationSpeed * Time.deltaTime);

            // Convert Local Input to World Direction
            Vector3 input = new Vector3(h, 0, v);
            if (input.magnitude > 0.1f)
            {
                finalMove = transform.TransformDirection(input.normalized) * moveSpeed;
            }
        }
        else
        {
            // Orthographic mode
            
            // Input maps directly to World Direction
            Vector3 input = new Vector3(h, 0, v);
            if (input.magnitude > 0.1f)
            {
                // Face the direction we are moving
                float targetAngle = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                // Move in that direction
                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                finalMove = moveDir * moveSpeed;
            }
        }

        // 3. Apply Final Move + Gravity
        finalMove.y = verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);
    }
}