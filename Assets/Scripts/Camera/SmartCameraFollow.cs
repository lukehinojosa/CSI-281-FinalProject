using UnityEngine;

public class SmartCameraFollow : MonoBehaviour
{
    public enum CameraMode { ThirdPerson, TopDown }

    [Header("Mode")]
    [Tooltip("ThirdPerson = Rotates behind target. TopDown = Fixed rotation.")]
    public CameraMode mode = CameraMode.ThirdPerson;

    [Header("Target Settings")]
    public Transform target;
    
    // Used for Third Person
    public float height = 2.5f;
    public float distance = 3.5f;

    // Used for Top Down
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
            // For Top Down, we snap immediately to the offset
            transform.rotation = Quaternion.Euler(60, 0, 0); // Standard Top-Down angle
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

    // Perspective Camera
    void FollowThirdPerson(float dt)
    {
        // 1. Calculate Rotation (Follow Target's Y)
        float wantedRotationAngle = target.eulerAngles.y;
        currentRotationAngleY = Mathf.LerpAngle(currentRotationAngleY, wantedRotationAngle, rotationDamping * dt);
        Quaternion currentRotation = Quaternion.Euler(0, currentRotationAngleY, 0);

        // 2. Calculate Position
        Vector3 desiredPos = target.position;
        desiredPos -= currentRotation * Vector3.forward * distance;
        desiredPos += Vector3.up * height;

        // 3. Collision Logic
        Vector3 rayOrigin = target.position + Vector3.up * (height * 0.5f);
        Vector3 rayDir = desiredPos - rayOrigin;
        float targetDist = distance; 

        if (Physics.Raycast(rayOrigin, rayDir.normalized, out RaycastHit hit, distance, obstacleMask))
        {
            float hitDistance = Vector3.Distance(rayOrigin, hit.point);
            targetDist = Mathf.Clamp(hitDistance - wallBuffer, 0.5f, distance);
        }

        currentDistance = Mathf.Lerp(currentDistance, targetDist, zoomSpeed * dt);

        Vector3 finalPos = target.position;
        finalPos -= currentRotation * Vector3.forward * currentDistance;
        finalPos += Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, finalPos, positionDamping * dt);
        transform.LookAt(target.position + Vector3.up * (height * 0.5f));
    }
}