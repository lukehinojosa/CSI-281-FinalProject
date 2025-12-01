using UnityEngine;

[ExecuteInEditMode]
public class FitQuadToCamera : MonoBehaviour
{
    public Camera targetCamera;
    public float distance = 0.41f;

    void OnEnable()
    {
        // Subscribe to the render loop
        Camera.onPreCull += SnapToCamera;
    }

    void OnDisable()
    {
        // Clean up to prevent memory leaks
        Camera.onPreCull -= SnapToCamera;
    }

    // This runs immediately before any camera renders a frame.
    void SnapToCamera(Camera cam)
    {
        // Only run if the camera rendering the target camera
        if (targetCamera == null || cam != targetCamera) return;

        // Follow Position & Rotation
        transform.position = targetCamera.transform.position + targetCamera.transform.forward * distance;
        transform.rotation = targetCamera.transform.rotation;

        // Scale to fit Frustum/Ortho Size
        float height, width;

        if (targetCamera.orthographic)
        {
            height = targetCamera.orthographicSize * 2.0f;
            width = height * targetCamera.aspect;
        }
        else
        {
            height = 2.0f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            width = height * targetCamera.aspect;
        }

        transform.localScale = new Vector3(width, height, 1);
    }
}