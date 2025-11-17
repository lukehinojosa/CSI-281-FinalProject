using UnityEngine;

[ExecuteInEditMode] // Allows the script to run in the editor
public class FitQuadToCamera : MonoBehaviour
{
    void Update()
    {
        Camera cam = transform.parent.GetComponent<Camera>();
        if (cam == null) return;

        float distance = transform.localPosition.z;
        float height, width;

        if (cam.orthographic)
        {
            height = cam.orthographicSize * 2.0f;
            width = height * cam.aspect;
        }
        else // Perspective
        {
            height = 2.0f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            width = height * cam.aspect;
        }

        transform.localScale = new Vector3(width, height, 1);
    }
}