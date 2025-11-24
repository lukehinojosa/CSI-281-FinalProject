using UnityEngine;

public class EntityCameraRig : MonoBehaviour
{
    [Header("Components")]
    public FieldOfView fov;
    public Renderer meshRenderer; // Used for transparency control

    [Header("Cameras")]
    public Camera primaryCamera;
    public Camera secondaryCamera;

    void Awake()
    {
        // Auto-find components if not assigned
        if (fov == null) fov = GetComponent<FieldOfView>();
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        
        if(primaryCamera) primaryCamera.gameObject.SetActive(false);
        if(secondaryCamera) secondaryCamera.gameObject.SetActive(false);
    }

    public Camera GetCamera(bool useSecondary)
    {
        return useSecondary ? secondaryCamera : primaryCamera;
    }

    public void SetMaterialAlpha(float alpha)
    {
        if (meshRenderer != null)
        {
            Color color = meshRenderer.material.color;
            color.a = alpha;
            meshRenderer.material.color = color;
        }
    }
}