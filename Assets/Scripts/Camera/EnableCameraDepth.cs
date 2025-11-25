using UnityEngine;

[ExecuteInEditMode]
public class EnableCameraDepth : MonoBehaviour
{
    void OnEnable()
    {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }
}