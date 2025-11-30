using UnityEngine;

public class SmartCameraFollow : MonoBehaviour
{
    public enum CameraMode { ThirdPerson, TopDown }

    [Header("Mode")]
    [Tooltip("ThirdPerson = Rotates behind target. TopDown = Fixed rotation.")]
    public CameraMode mode = CameraMode.ThirdPerson;

    [Header("Target")]
    public Transform target;

    [Header("Third Person Settings")]
    [Tooltip("How far back the camera sits.")]
    public float distance = 3.5f;
    [Tooltip("The fixed vertical angle (pitch) of the camera.")]
    public float fixedPitch = 20f;
    [Tooltip("Height offset from the target's feet (e.g., 1.5 for chest height).")]
    public float pivotOffset = 1.5f;

    [Header("Top Down Settings")]
    public Vector3 topDownOffset = new Vector3(0, 20, -10);

    [Header("Follow Settings")]
    public float rotationDamping = 5.0f; 
    public float positionDamping = 10.0f;

    [Header("Collision Settings (Third Person Only)")]
    public LayerMask obstacleMask;
    public float wallBuffer = 0.2f;
    public float zoomSpeed = 15f;

    private float currentDistance;
    private float currentRotationAngleY;
    
    void Start()
    {
        if (target == null)
        {
            target = transform.parent;
            if (target == null) { enabled = false; return; }
        }

        transform.SetParent(null);
        
        // Initialization
        if (mode == CameraMode.ThirdPerson)
        {
            currentDistance = distance;
            currentRotationAngleY = target.eulerAngles.y;
        }
        else
        {
            // For Top Down, snap immediately to the offset
            transform.rotation = Quaternion.Euler(60, 0, 0);
        }
        
        SnapCamera();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        if (mode == CameraMode.ThirdPerson)
        {
            FollowThirdPerson(Time.deltaTime);
        }
        else
        {
            FollowTopDown(Time.deltaTime);
        }
    }

    public void SnapCamera()
    {
        if (mode == CameraMode.ThirdPerson)
        {
            currentRotationAngleY = target.eulerAngles.y;
            currentDistance = distance;
            FollowThirdPerson(100f);
        }
        else
        {
            FollowTopDown(100f);
        }
    }

    // Orthographic
    void FollowTopDown(float dt)
    {
        Vector3 desiredPos = target.position + topDownOffset;
        
        // Smoothly move there
        transform.position = Vector3.Lerp(transform.position, desiredPos, positionDamping * dt);
        transform.rotation = Quaternion.Euler(90, 0, 0); 
    }

    void FollowThirdPerson(float dt)
    {
        // Calculate Yaw
        float wantedRotationAngle = target.eulerAngles.y;
        currentRotationAngleY = Mathf.LerpAngle(currentRotationAngleY, wantedRotationAngle, rotationDamping * dt);
        
        // Create the Fixed Rotation
        Quaternion finalRotation = Quaternion.Euler(fixedPitch, currentRotationAngleY, 0);

        // Determine what the camera is looking at
        Vector3 pivotPos = target.position + Vector3.up * pivotOffset;

        // Calculate Direction
        Vector3 direction = finalRotation * -Vector3.forward;

        // Collision Logic
        float targetDist = distance; 
        
        // Raycast from Pivot backwards along the camera direction
        if (Physics.Raycast(pivotPos, direction, out RaycastHit hit, distance, obstacleMask))
        {
            targetDist = Mathf.Clamp(hit.distance - wallBuffer, 0.1f, distance);
        }

        // Smooth zoom
        currentDistance = Mathf.Lerp(currentDistance, targetDist, zoomSpeed * dt);

        // Apply Final Position & Rotation
        Vector3 finalPos = pivotPos + (direction * currentDistance);

        transform.position = finalPos;
        transform.rotation = finalRotation;
    }
}