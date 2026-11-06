using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
static class MarsFogEditorToggle
{
    static readonly int SuppressID = Shader.PropertyToID("_MarsFogSuppress");

    static MarsFogEditorToggle()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
    {
        bool fogOn = CoreUtils.IsSceneViewFogEnabled(camera)
                     && camera.cameraType != CameraType.Preview;

        Shader.SetGlobalFloat(SuppressID, fogOn ? 0f : 1f);
    }
}
