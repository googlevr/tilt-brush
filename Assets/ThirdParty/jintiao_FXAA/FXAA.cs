using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class FXAA : MonoBehaviour
{
	public Material mat;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (mat == null)
			mat = new Material(Shader.Find("FX/FXAA"));

		Graphics.Blit(source, destination, mat);
	}
}
