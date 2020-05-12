using UnityEngine;

#pragma warning disable 414

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Sonic Ether/SE Natural Bloom and Dirty Lens")]
public class SENaturalBloomAndDirtyLens : MonoBehaviour
{
	
	public Shader shader;
	private Material material;
	private bool isSupported;
	private float blurSize = 4.0f;
	Camera cam;
	
	
	
	[Range(0.0f, 0.4f)]
	public float bloomIntensity = 0.05f;
	[Range(0.0f, 1.0f)]
	public float bloomScatterFactor = 0.5f;
	[Range(0.0f, 1.0f)]
	public float lensDirtScatterFactor = 0.5f;
	[Range(0.0f, 0.95f)]
	public float lensDirtIntensity = 0.05f;
	public Texture2D lensDirtTexture;



	public enum DepthBlendFunction
	{
		Exponential,
		ExponentialSquared
	};


	public bool inputIsHDR;
	public bool lowQuality = false;

	public bool depthBlending = false;
	public DepthBlendFunction depthBlendFunction = DepthBlendFunction.Exponential;
	[Range(0.0f, 1.0f)]
	public float depthBlendFactor = 0.1f;
	[Range(0.0f, 1.0f)]
	public float maxDepthBlendFactor = 1.0f;
	[Range(0.0f, 1.0f)]
	public float depthScatterFactor = 0.5f;

	void OnEnable() 
	{
		isSupported = true;

		if (!material)
			material = new Material(shader);

		if (!SystemInfo.supportsImageEffects || !SystemInfo.supportsRenderTextures || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
		{
			isSupported = false;
		}

		cam = GetComponent<Camera>();
	}

	void OnDisable()
	{
		if(material)
			DestroyImmediate(material);
	}
	
	void OnRenderImage(RenderTexture source, RenderTexture destination) 
	{
		if (!isSupported)
		{
			Graphics.Blit(source, destination);
			return;
		}


		#if UNITY_EDITOR
		if (source.format == RenderTextureFormat.ARGBHalf)
			inputIsHDR = true;
		else
			inputIsHDR = false;
		#endif

		// Intermediate format for temp buffers.
		RenderTextureFormat fmt = RenderTextureFormat.ARGBHalf;

		material.hideFlags = HideFlags.HideAndDontSave;

		material.SetFloat("_BloomIntensity", Mathf.Exp(bloomIntensity) - 1.0f);
		material.SetFloat("_LensDirtIntensity", Mathf.Exp(lensDirtIntensity) - 1.0f);
		material.SetFloat("_DepthBlendFactor", depthBlending ? Mathf.Pow(depthBlendFactor, 2.0f) : 0.0f);
		material.SetFloat("_MaxDepthBlendFactor", maxDepthBlendFactor);
		material.SetFloat("_DepthScatterFactor", depthScatterFactor * 4.0f - 1.0f);
		material.SetFloat("_BloomScatterFactor", bloomScatterFactor * 5.0f - 2.5f);
		material.SetFloat("_LensDirtScatterFactor", lensDirtScatterFactor * 5.0f - 2.5f);
		material.SetInt("_DepthBlendFunction", depthBlendFunction == DepthBlendFunction.Exponential ? 0 : 1);
		material.SetMatrix("ProjectionMatrixInverse", cam.projectionMatrix.inverse);

		source.filterMode = FilterMode.Bilinear;

		RenderTexture clampedSource = RenderTexture.GetTemporary(source.width, source.height, 0, fmt);
		Graphics.Blit(source, clampedSource, material, 5);

		int initialDivisor = lowQuality ? 4 : 2;

		int rtWidth = source.width / initialDivisor;
		int rtHeight = source.height / initialDivisor;

		RenderTexture downsampled;
		downsampled = clampedSource;



		int iterations = 1;

		int octaves = lowQuality ? 4 : 8;

		for (int i = 0; i < octaves; i++)
		{
			RenderTexture rt = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, fmt);
			rt.filterMode = FilterMode.Bilinear;

			Graphics.Blit(downsampled, rt, material, 1);


			if (i >= 1)
			{
				iterations = 2;
			}

			for (int j = 0; j < iterations; j++)
			{

				//vertical blur
				RenderTexture rt2 = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, fmt);
				rt2.filterMode = FilterMode.Bilinear;
				Graphics.Blit(rt, rt2, material, 2);
				RenderTexture.ReleaseTemporary(rt);
				rt = rt2;

				rt2 = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, fmt);
				rt2.filterMode = FilterMode.Bilinear;
				Graphics.Blit(rt, rt2, material, 3);
				RenderTexture.ReleaseTemporary(rt);
				rt = rt2;
			}

			downsampled = rt;

			switch (i)
			{
				case 0:
					material.SetTexture("_Bloom0", rt);
					break;
				case 1:
					material.SetTexture("_Bloom1", rt);
					break;
				case 2:
					material.SetTexture("_Bloom2", rt);
					break;
				case 3:
					material.SetTexture("_Bloom3", rt);
					break;
				case 4:
					material.SetTexture("_Bloom4", rt);
					break;
				case 5:
					material.SetTexture("_Bloom5", rt);
					break;
				case 6:
					material.SetTexture("_Bloom6", rt);
					break;
				case 7:
					material.SetTexture("_Bloom7", rt);
					break;
				default: 
					break;
			}

			RenderTexture.ReleaseTemporary(rt);

			rtWidth /= lowQuality ? 3 : 2;
			rtHeight /= lowQuality ? 3 : 2;
		}


		material.SetTexture("_LensDirt", lensDirtTexture);
		Graphics.Blit(clampedSource, destination, material, lowQuality ? 4 : 0);
		RenderTexture.ReleaseTemporary(clampedSource);

	}
}