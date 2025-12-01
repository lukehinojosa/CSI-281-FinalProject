using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFogBinder : MonoBehaviour
{
    // 0 = Player Layer, 1 = Enemy Layer
    public int fogLayerIndex = 0; 

    // Called automatically by Unity before this camera renders
    void OnPreRender()
    {
        // The shader will use this value for everything drawn by this camera.
        Shader.SetGlobalFloat("_FowIndex", (float)fogLayerIndex);
    }
}