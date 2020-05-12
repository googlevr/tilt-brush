using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(SENaturalBloomAndDirtyLens))]
public class SENaturalBloomAndDirtyLensEditor : Editor
{
	SerializedObject serObj;

	SerializedProperty bloomIntensity;
	SerializedProperty bloomScatterFactor;
	SerializedProperty lensDirtIntensity;
	SerializedProperty lensDirtScatterFactor;
	SerializedProperty lensDirtTexture;
	SerializedProperty lowQuality;
	SerializedProperty depthBlending;
	SerializedProperty depthBlendFunction;
	SerializedProperty depthBlendFactor;
	SerializedProperty maxDepthBlendFactor;
	SerializedProperty depthScatterFactor;

	SENaturalBloomAndDirtyLens instance;

	void OnEnable()
	{
		serObj = new SerializedObject(target);
		bloomIntensity = serObj.FindProperty("bloomIntensity");
		bloomScatterFactor = serObj.FindProperty("bloomScatterFactor");
		lensDirtIntensity = serObj.FindProperty("lensDirtIntensity");
		lensDirtScatterFactor = serObj.FindProperty("lensDirtScatterFactor");
		lensDirtTexture = serObj.FindProperty("lensDirtTexture");
		lowQuality = serObj.FindProperty("lowQuality");
		depthBlending = serObj.FindProperty("depthBlending");
		depthBlendFunction = serObj.FindProperty("depthBlendFunction");
		depthBlendFactor = serObj.FindProperty("depthBlendFactor");
		maxDepthBlendFactor = serObj.FindProperty("maxDepthBlendFactor");
		depthScatterFactor = serObj.FindProperty("depthScatterFactor");

		instance = (SENaturalBloomAndDirtyLens)target;		
	}

	public override void OnInspectorGUI()
	{
		serObj.Update();


		if (!instance.inputIsHDR)
		{
			EditorGUILayout.HelpBox("The camera is either not HDR enabled or there is an image effect before this one that converts from HDR to LDR. This image effect is dependant an HDR input to function properly.", MessageType.Warning);
		}

		EditorGUILayout.PropertyField(bloomIntensity, new GUIContent("Bloom Intensity", "The amount of light that is scattered inside the lens uniformly. Increase this value for a more drastic bloom."));
		EditorGUILayout.PropertyField(bloomScatterFactor, new GUIContent("Bloom Scatter Factor", "Affects the scattering appeariance/tightness of bloom."));
		EditorGUILayout.PropertyField(lensDirtIntensity, new GUIContent("Lens Dirt Intensity", "The amount that the lens dirt texture contributes to light scattering. Increase this value for a dirtier lens."));
		EditorGUILayout.PropertyField(lensDirtScatterFactor, new GUIContent("Lens Dirt Scatter Factor", "Affects the scattering appeariance/tightness of lens dirt bloom."));
		EditorGUILayout.PropertyField(lensDirtTexture, new GUIContent("Lens Dirt Texture", "The texture that controls per-channel light scattering amount. Black pixels do not affect light scattering. The brighter the pixel, the more light that is scattered."));
		EditorGUILayout.PropertyField(lowQuality, new GUIContent("Low Quality", "Enable this for lower quality in exchange for faster rendering."));

		EditorGUILayout.Space();
		EditorGUILayout.PropertyField(depthBlending, new GUIContent("Depth Blending", "Enable depth-based bloom blending (useful for fog)."));
		if (depthBlending.boolValue)
		{
			EditorGUILayout.PropertyField(depthBlendFunction, new GUIContent("Blend Function", "Depth-based blend function."));
			EditorGUILayout.PropertyField(depthBlendFactor, new GUIContent("Blend Factor", "Depth-based blend factor. Higher values mean bloom is blended more aggressively."));
			EditorGUILayout.PropertyField(maxDepthBlendFactor, new GUIContent("Max Depth Blend", "The maximum blend factor for Depth Blending. Lower this to clamp the maximum allowed blend factor."));
			EditorGUILayout.PropertyField(depthScatterFactor, new GUIContent("Depth Scatter Factor", "Affects the scattering appearance/tightness of depth-blended bloom."));

		}



		serObj.ApplyModifiedProperties();
	}
}
