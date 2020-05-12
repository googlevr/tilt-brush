using UnityEngine;

[ExecuteInEditMode]
public class TiltShift : MonoBehaviour
{
	public Material mat;

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (mat == null)
			mat = new Material(Shader.Find("FX/TiltShift"));

		Graphics.Blit(source, destination, mat);
	}
}
