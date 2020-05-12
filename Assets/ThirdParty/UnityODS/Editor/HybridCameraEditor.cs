//The HybridCamera uses Coroutines for rendering, so "Capture" only works correctly when in "Play" mode
//Coroutines do not work correctly when in normal "Editor" mode.
using UnityEditor;
using UnityEngine;
using ODS;

[CustomEditor(typeof(ODS.HybridCamera))]
public class HybridCameraEditor : Editor
{
    public bool StereoCubemapToggle = false;
    public bool VR180Toggle = false;
    private GUIContent StereoCubmapLabel = new GUIContent("Stereo Cubemap");
    private GUIContent Vr180Label = new GUIContent("VR 180");

    public override void OnInspectorGUI() {
        ODS.HybridCamera cam = (ODS.HybridCamera)target;

        StereoCubemapToggle = EditorGUILayout.Toggle(StereoCubmapLabel, StereoCubemapToggle);
        VR180Toggle = EditorGUILayout.Toggle(Vr180Label, VR180Toggle);
        EditorGUILayout.Space();
        DrawDefaultInspector();

        if (cam) {
          if (StereoCubemapToggle) {
            cam.SetOdsRendererType(HybridCamera.OdsRendererType.StereoCubemap);
          }
          else {
            cam.SetOdsRendererType(HybridCamera.OdsRendererType.Slice);
          }

          cam.EnableVr180(VR180Toggle);
        }

        if ( cam != null && GUILayout.Button( "Capture" ) ) {
            cam.RenderAll(cam.transform);
        }

        GUILayout.Label( cam.FinalImage, GUILayout.MaxWidth( 300 ), GUILayout.MaxHeight( 300 ) );
    }
}
