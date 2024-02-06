using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VolumetricFogAndMist2
{
	[ExecuteInEditMode]
	[DefaultExecutionOrder(100)]
	public class VolumetricFog : MonoBehaviour
	{
		private struct FogOfWarTransition
		{
			public bool enabled;

			public int x;

			public int y;

			public float startTime;

			public float startDelay;

			public float duration;

			public int initialAlpha;

			public int targetAlpha;
		}

		public enum HeightmapCaptureResolution
		{
			_64 = 0x40,
			_128 = 0x80,
			_256 = 0x100,
			_512 = 0x200,
			_1024 = 0x400
		}

		public VolumetricFogProfile profile;

		[Tooltip("Supports Unity native lights including point and spot lights.")]
		public bool enableNativeLights;

		[Tooltip("Enable fast point lights. This option is much faster than native lights. However, if you enable native lights, this option can't be enabled as point lights are already included in the native lights support.")]
		public bool enablePointLights;

		public bool enableSpotLights;

		public bool enableVoids;

		[Tooltip("Fades in/out fog effect when reference controller enters the fog volume.")]
		public bool enableFade;

		[Tooltip("Fog volume blending starts when reference controller is within this fade distance to any volume border.")]
		public float fadeDistance = 1f;

		[Tooltip("If this option is disabled, the fog disappears when the reference controller exits the volume and appears when the controller enters the volume. Enable this option to fade out the fog volume when the controller enters the volume. ")]
		public bool fadeOut;

		[Tooltip("The controller (player or camera) to check if enters the fog volume.")]
		public Transform fadeController;

		[Tooltip("Enable sub-volume blending.")]
		public bool enableSubVolumes;

		[Tooltip("Allowed subVolumes. If no subvolumes are specified, any subvolume entered by this controller will affect this fog volume.")]
		public List<VolumetricFogSubVolume> subVolumes;

		[Tooltip("Shows the fog volume boundary in Game View")]
		public bool showBoundary;

		[NonSerialized]
		public MeshRenderer meshRenderer;

		private Material fogMat;

		private Material noiseMat;

		private Material turbulenceMat;

		private Shader fogShader;

		private RenderTexture rtNoise;

		private RenderTexture rtTurbulence;

		private float turbAcum;

		private Vector3 windAcum;

		private Vector3 sunDir;

		private float dayLight;

		private float moonLight;

		private List<string> shaderKeywords;

		private Texture3D detailTex;

		private Texture3D refDetailTex;

		private Mesh debugMesh;

		private Material fogDebugMat;

		private VolumetricFogProfile activeProfile;

		private VolumetricFogProfile lerpProfile;

		private Vector3 lastControllerPosition;

		private float alphaMultiplier = 1f;

		private bool profileIsInstanced;

		private bool requireUpdateMaterial;

		private ColorSpace currentAppliedColorSpace;

		private static Texture2D blueNoiseTex;

		public static List<VolumetricFog> volumetricFogs = new List<VolumetricFog>();

		public bool enableFogOfWar;

		public Vector3 fogOfWarCenter;

		public bool fogOfWarIsLocal;

		public Vector3 fogOfWarSize = new Vector3(1024f, 0f, 1024f);

		[Range(32f, 2048f)]
		public int fogOfWarTextureSize = 256;

		[Range(0f, 100f)]
		public float fogOfWarRestoreDelay;

		[Range(0f, 25f)]
		public float fogOfWarRestoreDuration = 2f;

		[Range(0f, 1f)]
		public float fogOfWarSmoothness = 1f;

		public bool fogOfWarBlur;

		private const int MAX_SIMULTANEOUS_TRANSITIONS = 10000;

		private bool canDestroyFOWTexture;

		public bool maskEditorEnabled;

		public MASK_TEXTURE_BRUSH_MODE maskBrushMode = MASK_TEXTURE_BRUSH_MODE.RemoveFog;

		public Color maskBrushColor = Color.white;

		[Range(1f, 128f)]
		public int maskBrushWidth = 20;

		[Range(0f, 1f)]
		public float maskBrushFuzziness = 0.5f;

		[Range(0f, 1f)]
		public float maskBrushOpacity = 0.15f;

		[SerializeField]
		private Texture2D _fogOfWarTexture;

		private Color32[] fogOfWarColorBuffer;

		private FogOfWarTransition[] fowTransitionList;

		private int lastTransitionPos;

		private Dictionary<int, int> fowTransitionIndices;

		private bool requiresTextureUpload;

		private Material fowBlur;

		private RenderTexture fowBlur1;

		private RenderTexture fowBlur2;

		private const string SURFACE_CAM_NAME = "SurfaceCam";

		private RenderTexture rt;

		private Camera cam;

		private int camStartFrameCount;

		private Matrix4x4 camMatrix;

		private Vector3 lastCamPos;

		private static Matrix4x4 identityMatrix = Matrix4x4.identity;

		public VolumetricFogProfile settings
		{
			get
			{
				if (!profileIsInstanced && profile != null)
				{
					profile = UnityEngine.Object.Instantiate(profile);
					profileIsInstanced = true;
				}
				requireUpdateMaterial = true;
				return profile;
			}
			set
			{
				profile = value;
				profileIsInstanced = false;
			}
		}

		public Material material => fogMat;

		public Vector3 anchoredFogOfWarCenter
		{
			get
			{
				if (!fogOfWarIsLocal)
				{
					return fogOfWarCenter;
				}
				return base.transform.position + fogOfWarCenter;
			}
		}

		public Texture2D fogOfWarTexture
		{
			get
			{
				return _fogOfWarTexture;
			}
			set
			{
				if (!(_fogOfWarTexture != value) || !(value != null))
				{
					return;
				}
				if (value.width != value.height)
				{
					Debug.LogError("Fog of war texture must be square.");
					return;
				}
				_fogOfWarTexture = value;
				canDestroyFOWTexture = false;
				ReloadFogOfWarTexture();
				if (fogMat != null)
				{
					fogMat.SetTexture(ShaderParams.FogOfWarTexture, _fogOfWarTexture);
				}
			}
		}

		public Color32[] fogOfWarTextureData
		{
			get
			{
				return fogOfWarColorBuffer;
			}
			set
			{
				enableFogOfWar = true;
				fogOfWarColorBuffer = value;
				if (value != null && !(_fogOfWarTexture == null) && value.Length == _fogOfWarTexture.width * _fogOfWarTexture.height)
				{
					_fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
					_fogOfWarTexture.Apply();
				}
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Init()
		{
			volumetricFogs.Clear();
		}

		private void OnEnable()
		{
			volumetricFogs.Add(this);
			VolumetricFogManager volumetricFogManager = Tools.CheckMainManager();
			base.gameObject.layer = volumetricFogManager.fogLayer;
			FogOfWarInit();
			CheckSurfaceCapture();
			UpdateMaterialProperties();
		}

		private void OnDisable()
		{
			if (volumetricFogs.Contains(this))
			{
				volumetricFogs.Remove(this);
			}
			if (profile != null)
			{
				profile.onSettingsChanged -= UpdateMaterialProperties;
			}
			DisableSurfaceCapture();
		}

		private void OnValidate()
		{
			UpdateMaterialProperties();
		}

		private void OnDestroy()
		{
			if (rtNoise != null)
			{
				rtNoise.Release();
			}
			if (rtTurbulence != null)
			{
				rtTurbulence.Release();
			}
			if (fogMat != null)
			{
				UnityEngine.Object.DestroyImmediate(fogMat);
				fogMat = null;
			}
			FogOfWarDestroy();
			DisposeSurfaceCapture();
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = new Color(1f, 1f, 0f, 0.75f);
			Gizmos.matrix = base.transform.localToWorldMatrix;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
		}

		private void LateUpdate()
		{
			if (fogMat == null || meshRenderer == null || profile == null)
			{
				return;
			}
			if (requireUpdateMaterial)
			{
				requireUpdateMaterial = false;
				UpdateMaterialProperties();
			}
			base.transform.rotation = Quaternion.identity;
			Vector3 position = base.transform.position;
			Vector3 vector = base.transform.lossyScale * 0.5f;
			new Bounds(position, vector * 2f);
			ComputeActiveProfile();
			if (enableFade || enableSubVolumes)
			{
				ApplyProfileSettings();
			}
			if (activeProfile.shape == VolumetricFogShape.Sphere)
			{
				Vector3 localScale = base.transform.localScale;
				if (localScale.z != localScale.x)
				{
					localScale.z = localScale.x;
					base.transform.localScale = localScale;
					vector = base.transform.lossyScale * 0.5f;
				}
				vector.x *= vector.x;
			}
			Vector4 value = new Vector4(vector.x * activeProfile.border + 0.0001f, vector.x * (1f - activeProfile.border), vector.z * activeProfile.border + 0.0001f, vector.z * (1f - activeProfile.border));
			if (activeProfile.terrainFit)
			{
				vector.y = Mathf.Max(vector.y, activeProfile.terrainFogHeight);
			}
			fogMat.SetVector(ShaderParams.BoundsCenter, position);
			fogMat.SetVector(ShaderParams.BoundsExtents, vector);
			fogMat.SetVector(ShaderParams.BoundsBorder, value);
			Vector4 value2 = new Vector4(activeProfile.verticalOffset, position.y - vector.y, vector.y * 2f, 0f);
			fogMat.SetVector(ShaderParams.BoundsData, value2);
			VolumetricFogManager instance = VolumetricFogManager.instance;
			Light sun = instance.sun;
			Color color;
			float num;
			if (activeProfile.dayNightCycle && sun != null)
			{
				sunDir = -sun.transform.forward;
				color = sun.color;
				num = sun.intensity;
				_ = activeProfile.lightDiffusionIntensity;
			}
			else
			{
				sunDir = Vector3.up;
				color = Color.white;
				num = 1f;
			}
			dayLight = 1f + sunDir.y * 2f;
			if (dayLight < 0f)
			{
				dayLight = 0f;
			}
			else if (dayLight > 1f)
			{
				dayLight = 1f;
			}
			float brightness = activeProfile.brightness;
			float a = activeProfile.albedo.a;
			Color color2 = color * (dayLight * num * brightness * ((QualitySettings.activeColorSpace == ColorSpace.Gamma) ? 2f : 1.33f));
			color2.a = a;
			fogMat.SetFloat(ShaderParams.LightDiffusionIntensity, activeProfile.lightDiffusionIntensity);
			fogMat.SetVector(ShaderParams.SunDir, sunDir);
			Light moon = instance.moon;
			moonLight = 0f;
			if (activeProfile.dayNightCycle && !enableNativeLights && moon != null)
			{
				moonLight = 1f + (-moon.transform.forward).y * 2f;
				if (moonLight < 0f)
				{
					moonLight = 0f;
				}
				else if (moonLight > 1f)
				{
					moonLight = 1f;
				}
				brightness = activeProfile.brightness;
				a = activeProfile.albedo.a;
				color2 += moon.color * (moonLight * moon.intensity * brightness * 2f);
				color2.a = a;
			}
			if (enableFade && fadeOut && Application.isPlaying)
			{
				color2.a *= 1f - alphaMultiplier;
			}
			else
			{
				color2.a *= alphaMultiplier;
			}
			fogMat.SetVector(ShaderParams.LightColor, color2);
			meshRenderer.enabled = color2.a > 0f;
			windAcum += activeProfile.windDirection * Time.deltaTime;
			windAcum.x %= 10000f;
			windAcum.y %= 10000f;
			windAcum.z %= 10000f;
			fogMat.SetVector(ShaderParams.WindDirection, windAcum);
			UpdateNoise();
			if (enableFogOfWar)
			{
				UpdateFogOfWar();
			}
			if (showBoundary)
			{
				if (fogDebugMat == null)
				{
					fogDebugMat = new Material(Shader.Find("Hidden/VolumetricFog2/VolumeDebug"));
				}
				if (debugMesh == null)
				{
					MeshFilter component = GetComponent<MeshFilter>();
					if (component != null)
					{
						debugMesh = component.sharedMesh;
					}
				}
				Matrix4x4 matrix = Matrix4x4.TRS(base.transform.position, base.transform.rotation, base.transform.lossyScale);
				Graphics.DrawMesh(debugMesh, matrix, fogDebugMat, 0);
			}
			if (enablePointLights && !enableNativeLights)
			{
				PointLightManager.usingPointLights = true;
			}
			if (enableVoids)
			{
				FogVoidManager.usingVoids = true;
			}
			SurfaceCaptureUpdate();
		}

		private void UpdateNoise()
		{
			if (activeProfile == null)
			{
				return;
			}
			Texture noiseTexture = activeProfile.noiseTexture;
			if (!(noiseTexture == null))
			{
				if (rtTurbulence == null || rtTurbulence.width != noiseTexture.width)
				{
					RenderTextureDescriptor desc = new RenderTextureDescriptor(noiseTexture.width, noiseTexture.height, RenderTextureFormat.ARGB32, 0);
					rtTurbulence = new RenderTexture(desc);
					rtTurbulence.wrapMode = TextureWrapMode.Repeat;
				}
				turbAcum += Time.deltaTime * activeProfile.turbulence;
				turbAcum %= 10000f;
				turbulenceMat.SetFloat(ShaderParams.TurbulenceAmount, turbAcum);
				turbulenceMat.SetFloat(ShaderParams.NoiseStrength, activeProfile.noiseStrength);
				turbulenceMat.SetFloat(ShaderParams.NoiseFinalMultiplier, activeProfile.noiseFinalMultiplier);
				Graphics.Blit(noiseTexture, rtTurbulence, turbulenceMat);
				if (rtNoise == null || rtNoise.width != noiseTexture.width)
				{
					RenderTextureDescriptor desc2 = new RenderTextureDescriptor(noiseTexture.width, noiseTexture.height, RenderTextureFormat.ARGB32, 0);
					rtNoise = new RenderTexture(desc2);
					rtNoise.wrapMode = TextureWrapMode.Repeat;
				}
				noiseMat.SetColor(ShaderParams.SpecularColor, activeProfile.specularColor);
				noiseMat.SetFloat(ShaderParams.SpecularIntensity, activeProfile.specularIntensity);
				float num = 1.0001f - activeProfile.specularThreshold;
				float value = ((sunDir.y > 0f) ? (1f - sunDir.y) : (1f + sunDir.y)) / num;
				noiseMat.SetFloat(ShaderParams.SpecularThreshold, value);
				noiseMat.SetVector(ShaderParams.SunDir, sunDir);
				Color ambientLight = RenderSettings.ambientLight;
				float ambientIntensity = RenderSettings.ambientIntensity;
				Color a = ambientLight * ambientIntensity;
				float num2 = 1.15f;
				num2 *= dayLight + moonLight;
				Color value2 = Color.Lerp(a, activeProfile.albedo * num2, num2);
				noiseMat.SetColor(ShaderParams.Color, value2);
				Graphics.Blit(rtTurbulence, rtNoise, noiseMat);
				fogMat.SetTexture(ShaderParams.MainTex, rtNoise);
				Color value3 = new Color(value2.r * 0.5f, value2.g * 0.5f, value2.b * 0.5f, 0f);
				fogMat.SetColor(ShaderParams.DetailColor, value3);
			}
		}

		public void UpdateMaterialProperties()
		{
			if (base.gameObject == null || !base.gameObject.activeInHierarchy)
			{
				DisableSurfaceCapture();
				return;
			}
			fadeDistance = Mathf.Max(0.1f, fadeDistance);
			meshRenderer = GetComponent<MeshRenderer>();
			if (profile == null)
			{
				if (fogMat == null && meshRenderer != null)
				{
					fogMat = new Material(Shader.Find("VolumetricFog2/Empty"));
					fogMat.hideFlags = HideFlags.DontSave;
					meshRenderer.sharedMaterial = fogMat;
				}
				DisableSurfaceCapture();
				return;
			}
			profile.onSettingsChanged -= UpdateMaterialProperties;
			profile.onSettingsChanged += UpdateMaterialProperties;
			if (subVolumes != null)
			{
				foreach (VolumetricFogSubVolume subVolume in subVolumes)
				{
					if (subVolume != null && subVolume.profile != null)
					{
						subVolume.profile.onSettingsChanged -= UpdateMaterialProperties;
						subVolume.profile.onSettingsChanged += UpdateMaterialProperties;
					}
				}
			}
			if (turbulenceMat == null)
			{
				turbulenceMat = new Material(Shader.Find("VolumetricFog2/Turbulence2D"));
			}
			if (noiseMat == null)
			{
				noiseMat = new Material(Shader.Find("VolumetricFog2/Noise2DGen"));
			}
			if (blueNoiseTex == null)
			{
				blueNoiseTex = Resources.Load<Texture2D>("Textures/BlueNoiseVF128");
			}
			if (meshRenderer != null)
			{
				if (fogShader == null)
				{
					fogShader = Shader.Find("VolumetricFog2/VolumetricFog2DURP");
					if (fogShader == null)
					{
						return;
					}
				}
				if (fogMat == null || fogMat.shader != fogShader)
				{
					fogMat = new Material(fogShader);
					meshRenderer.sharedMaterial = fogMat;
				}
			}
			if (!(fogMat == null))
			{
				profile.ValidateSettings();
				lastControllerPosition.x = float.MaxValue;
				activeProfile = profile;
				ComputeActiveProfile();
				ApplyProfileSettings();
				SurfaceCaptureSupportCheck();
			}
		}

		private void ComputeActiveProfile()
		{
			if (maskEditorEnabled)
			{
				alphaMultiplier = 0.85f;
			}
			if (Application.isPlaying)
			{
				if (enableFade || enableSubVolumes)
				{
					if (fadeController == null && Camera.main != null)
					{
						fadeController = Camera.main.transform;
					}
					if (fadeController != null && lastControllerPosition != fadeController.position)
					{
						lastControllerPosition = fadeController.position;
						activeProfile = profile;
						alphaMultiplier = 1f;
						if (enableFade)
						{
							float num = ComputeVolumeFade(base.transform, fadeDistance);
							alphaMultiplier *= num;
						}
						if (enableSubVolumes)
						{
							int count = VolumetricFogSubVolume.subVolumes.Count;
							int num2 = ((subVolumes != null) ? subVolumes.Count : 0);
							for (int i = 0; i < count; i++)
							{
								VolumetricFogSubVolume volumetricFogSubVolume = VolumetricFogSubVolume.subVolumes[i];
								if (volumetricFogSubVolume == null || volumetricFogSubVolume.profile == null || (num2 > 0 && !subVolumes.Contains(volumetricFogSubVolume)))
								{
									continue;
								}
								float num3 = ComputeVolumeFade(volumetricFogSubVolume.transform, volumetricFogSubVolume.fadeDistance);
								if (num3 > 0f)
								{
									if (lerpProfile == null)
									{
										lerpProfile = ScriptableObject.CreateInstance<VolumetricFogProfile>();
									}
									lerpProfile.Lerp(activeProfile, volumetricFogSubVolume.profile, num3);
									activeProfile = lerpProfile;
								}
							}
						}
					}
				}
				else
				{
					alphaMultiplier = 1f;
				}
			}
			if (activeProfile == null)
			{
				activeProfile = profile;
			}
		}

		private float ComputeVolumeFade(Transform transform, float fadeDistance)
		{
			Vector3 vector = transform.position - fadeController.position;
			vector.x = ((vector.x < 0f) ? (0f - vector.x) : vector.x);
			vector.y = ((vector.y < 0f) ? (0f - vector.y) : vector.y);
			vector.z = ((vector.z < 0f) ? (0f - vector.z) : vector.z);
			Vector3 vector2 = transform.lossyScale * 0.5f;
			Vector3 vector3 = vector - vector2;
			float num = ((vector3.x > vector3.y) ? vector3.x : vector3.y);
			num = ((num > vector3.z) ? num : vector3.z);
			fadeDistance += 0.0001f;
			return 1f - Mathf.Clamp01(num / fadeDistance);
		}

		private void ApplyProfileSettings()
		{
			currentAppliedColorSpace = QualitySettings.activeColorSpace;
			meshRenderer.sortingLayerID = activeProfile.sortingLayerID;
			meshRenderer.sortingOrder = activeProfile.sortingOrder;
			fogMat.renderQueue = activeProfile.renderQueue;
			float num = 0.1f / activeProfile.noiseScale;
			fogMat.SetFloat(ShaderParams.NoiseScale, num);
			fogMat.SetFloat(ShaderParams.DeepObscurance, activeProfile.deepObscurance * ((currentAppliedColorSpace == ColorSpace.Gamma) ? 1f : 1.2f));
			fogMat.SetFloat(ShaderParams.LightDiffusionPower, activeProfile.lightDiffusionPower);
			fogMat.SetFloat(ShaderParams.ShadowIntensity, activeProfile.shadowIntensity);
			fogMat.SetFloat(ShaderParams.Density, activeProfile.density);
			fogMat.SetVector(ShaderParams.RaymarchSettings, new Vector4(activeProfile.raymarchQuality, activeProfile.dithering * 0.01f, activeProfile.jittering, activeProfile.raymarchMinStep));
			if (activeProfile.useDetailNoise)
			{
				float z = 1f / activeProfile.detailScale * num;
				fogMat.SetVector(ShaderParams.DetailData, new Vector4(activeProfile.detailStrength, activeProfile.detailOffset, z, activeProfile.noiseFinalMultiplier));
				fogMat.SetColor(ShaderParams.DetailColor, activeProfile.albedo);
				fogMat.SetFloat(ShaderParams.DetailOffset, activeProfile.detailOffset);
				if ((detailTex == null || refDetailTex != activeProfile.detailTexture) && activeProfile.detailTexture != null)
				{
					refDetailTex = activeProfile.detailTexture;
					Texture3D texture3D = new Texture3D(activeProfile.detailTexture.width, activeProfile.detailTexture.height, activeProfile.detailTexture.depth, TextureFormat.Alpha8, mipChain: false);
					texture3D.filterMode = FilterMode.Bilinear;
					Color32[] pixels = activeProfile.detailTexture.GetPixels32();
					for (int i = 0; i < pixels.Length; i++)
					{
						pixels[i].a = pixels[i].r;
					}
					texture3D.SetPixels32(pixels);
					texture3D.Apply();
					detailTex = texture3D;
				}
				fogMat.SetTexture(ShaderParams.DetailTexture, detailTex);
			}
			fogMat.SetTexture(ShaderParams.BlueNoiseTexture, blueNoiseTex);
			if (shaderKeywords == null)
			{
				shaderKeywords = new List<string>();
			}
			else
			{
				shaderKeywords.Clear();
			}
			if (activeProfile.distance > 0f || activeProfile.enableDepthGradient)
			{
				fogMat.SetVector(ShaderParams.DistanceData, new Vector4(0f, 10f * (1f - activeProfile.distanceFallOff), 1f / (0.0001f + activeProfile.depthGradientMaxDistance * activeProfile.depthGradientMaxDistance), 1f / (0.0001f + activeProfile.distance * activeProfile.distance)));
			}
			if (activeProfile.distance > 0f)
			{
				shaderKeywords.Add("VF2_DISTANCE");
			}
			if (activeProfile.enableDepthGradient)
			{
				shaderKeywords.Add("VF2_DEPTH_GRADIENT");
				fogMat.SetTexture(ShaderParams.DepthGradientTexture, activeProfile.depthGradientTex);
			}
			if (activeProfile.enableHeightGradient)
			{
				shaderKeywords.Add("VF2_HEIGHT_GRADIENT");
				fogMat.SetTexture(ShaderParams.HeightGradientTexture, activeProfile.heightGradientTex);
			}
			if (activeProfile.shape == VolumetricFogShape.Box)
			{
				shaderKeywords.Add("VF2_SHAPE_BOX");
			}
			else
			{
				shaderKeywords.Add("VF2_SHAPE_SPHERE");
			}
			if (enableNativeLights)
			{
				shaderKeywords.Add("VF2_NATIVE_LIGHTS");
			}
			else if (enablePointLights)
			{
				shaderKeywords.Add("VF2_POINT_LIGHTS");
			}
			if (enableVoids)
			{
				shaderKeywords.Add("VF2_VOIDS");
			}
			if (activeProfile.receiveShadows)
			{
				shaderKeywords.Add("VF2_RECEIVE_SHADOWS");
			}
			if (activeProfile.cookie)
			{
				shaderKeywords.Add("VF2_LIGHT_COOKIE");
			}
			if (enableFogOfWar)
			{
				fogMat.SetTexture(ShaderParams.FogOfWarTexture, fogOfWarTexture);
				UpdateFogOfWarMaterialBoundsProperties();
				shaderKeywords.Add("VF2_FOW");
			}
			if (activeProfile.useDetailNoise)
			{
				shaderKeywords.Add("VF2_DETAIL_NOISE");
			}
			if (activeProfile.terrainFit)
			{
				shaderKeywords.Add("VF2_SURFACE");
			}
			fogMat.enabledKeywords = null;
			fogMat.shaderKeywords = shaderKeywords.ToArray();
		}

		private void UpdateFogOfWarMaterialBoundsProperties()
		{
			Vector3 vector = anchoredFogOfWarCenter;
			fogMat.SetVector(ShaderParams.FogOfWarCenter, vector);
			fogMat.SetVector(ShaderParams.FogOfWarSize, fogOfWarSize);
			Vector3 vector2 = vector - 0.5f * fogOfWarSize;
			fogMat.SetVector(ShaderParams.FogOfWarCenterAdjusted, new Vector4(vector2.x / fogOfWarSize.x, 1f, vector2.z / (fogOfWarSize.z + 0.0001f), 0f));
		}

		public static void FindAlphaClippingObjects()
		{
			DepthRenderPrePassFeature.DepthRenderPass.FindAlphaClippingRenderers();
		}

		private void FogOfWarInit()
		{
			if (fowTransitionList == null || fowTransitionList.Length != 10000)
			{
				fowTransitionList = new FogOfWarTransition[10000];
			}
			if (fowTransitionIndices == null)
			{
				fowTransitionIndices = new Dictionary<int, int>(10000);
			}
			else
			{
				fowTransitionIndices.Clear();
			}
			lastTransitionPos = -1;
			if (_fogOfWarTexture == null)
			{
				FogOfWarUpdateTexture();
			}
			else if (enableFogOfWar && (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0))
			{
				ReloadFogOfWarTexture();
			}
		}

		private void FogOfWarDestroy()
		{
			if (canDestroyFOWTexture)
			{
				UnityEngine.Object.DestroyImmediate(_fogOfWarTexture);
			}
			if (fowBlur1 != null)
			{
				fowBlur1.Release();
			}
			if (fowBlur2 != null)
			{
				fowBlur2.Release();
			}
		}

		public void ReloadFogOfWarTexture()
		{
			if (!(_fogOfWarTexture == null) && !(profile == null))
			{
				fogOfWarTextureSize = _fogOfWarTexture.width;
				fogOfWarColorBuffer = _fogOfWarTexture.GetPixels32();
				lastTransitionPos = -1;
				fowTransitionIndices.Clear();
				if (!enableFogOfWar)
				{
					enableFogOfWar = true;
					UpdateMaterialProperties();
				}
			}
		}

		private void FogOfWarUpdateTexture()
		{
			if (enableFogOfWar && Application.isPlaying)
			{
				int scaledSize = GetScaledSize(fogOfWarTextureSize, 1f);
				if (_fogOfWarTexture == null || _fogOfWarTexture.width != scaledSize || _fogOfWarTexture.height != scaledSize)
				{
					_fogOfWarTexture = new Texture2D(scaledSize, scaledSize, TextureFormat.RGBA32, mipChain: false, linear: true);
					_fogOfWarTexture.hideFlags = HideFlags.DontSave;
					_fogOfWarTexture.filterMode = FilterMode.Bilinear;
					_fogOfWarTexture.wrapMode = TextureWrapMode.Clamp;
					canDestroyFOWTexture = true;
					ResetFogOfWar();
				}
			}
		}

		private int GetScaledSize(int size, float factor)
		{
			size = (int)((float)size / factor);
			size /= 4;
			if (size < 1)
			{
				size = 1;
			}
			return size * 4;
		}

		public void UpdateFogOfWar(bool forceUpload = false)
		{
			if (!enableFogOfWar || _fogOfWarTexture == null)
			{
				return;
			}
			if (forceUpload)
			{
				requiresTextureUpload = true;
			}
			int width = _fogOfWarTexture.width;
			float time = Time.time;
			for (int i = 0; i <= lastTransitionPos; i++)
			{
				FogOfWarTransition fogOfWarTransition = fowTransitionList[i];
				if (!fogOfWarTransition.enabled)
				{
					continue;
				}
				float num = time - fogOfWarTransition.startTime - fogOfWarTransition.startDelay;
				if (!(num > 0f))
				{
					continue;
				}
				float num2 = ((fogOfWarTransition.duration <= 0f) ? 1f : (num / fogOfWarTransition.duration));
				if (num2 < 0f)
				{
					num2 = 0f;
				}
				else if (num2 > 1f)
				{
					num2 = 1f;
				}
				int num3 = (int)((float)fogOfWarTransition.initialAlpha + (float)(fogOfWarTransition.targetAlpha - fogOfWarTransition.initialAlpha) * num2);
				int num4 = fogOfWarTransition.y * width + fogOfWarTransition.x;
				fogOfWarColorBuffer[num4].a = (byte)num3;
				requiresTextureUpload = true;
				if (num2 >= 1f)
				{
					fowTransitionList[i].enabled = false;
					if (fogOfWarTransition.targetAlpha < 255 && fogOfWarRestoreDelay > 0f)
					{
						AddFogOfWarTransitionSlot(fogOfWarTransition.x, fogOfWarTransition.y, (byte)fogOfWarTransition.targetAlpha, byte.MaxValue, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
					}
				}
			}
			if (requiresTextureUpload)
			{
				requiresTextureUpload = false;
				_fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
				_fogOfWarTexture.Apply();
				if (fogOfWarBlur)
				{
					SetFowBlurTexture();
				}
			}
			if (fogOfWarIsLocal)
			{
				UpdateFogOfWarMaterialBoundsProperties();
			}
		}

		private void SetFowBlurTexture()
		{
			if (fowBlur == null)
			{
				fowBlur = new Material(Shader.Find("VolumetricFog2/FoWBlur"));
				fowBlur.hideFlags = HideFlags.DontSave;
			}
			if (!(fowBlur == null))
			{
				if (fowBlur1 == null || fowBlur1.width != _fogOfWarTexture.width || fowBlur2 == null || fowBlur2.width != _fogOfWarTexture.width)
				{
					CreateFoWBlurRTs();
				}
				fowBlur1.DiscardContents();
				Graphics.Blit(_fogOfWarTexture, fowBlur1, fowBlur, 0);
				fowBlur2.DiscardContents();
				Graphics.Blit(fowBlur1, fowBlur2, fowBlur, 1);
				fogMat.SetTexture(ShaderParams.FogOfWarTexture, fowBlur2);
			}
		}

		private void CreateFoWBlurRTs()
		{
			if (fowBlur1 != null)
			{
				fowBlur1.Release();
			}
			if (fowBlur2 != null)
			{
				fowBlur2.Release();
			}
			RenderTextureDescriptor desc = new RenderTextureDescriptor(_fogOfWarTexture.width, _fogOfWarTexture.height, RenderTextureFormat.ARGB32, 0);
			fowBlur1 = new RenderTexture(desc);
			fowBlur2 = new RenderTexture(desc);
		}

		public void SetFogOfWarAlpha(Vector3 worldPosition, float radius, float fogNewAlpha)
		{
			SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, 1f);
		}

		public void SetFogOfWarAlpha(Vector3 worldPosition, float radius, float fogNewAlpha, float duration)
		{
			SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, blendAlpha: true, duration, fogOfWarSmoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
		}

		public void SetFogOfWarAlpha(Vector3 worldPosition, float radius, float fogNewAlpha, float duration, float smoothness)
		{
			SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, blendAlpha: true, duration, smoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
		}

		public void SetFogOfWarAlpha(Vector3 worldPosition, float radius, float fogNewAlpha, bool blendAlpha, float duration, float smoothness, float restoreDelay, float restoreDuration)
		{
			if (_fogOfWarTexture == null || fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0)
			{
				return;
			}
			Vector3 vector = anchoredFogOfWarCenter;
			float num = (worldPosition.x - vector.x) / fogOfWarSize.x + 0.5f;
			if (num < 0f || num > 1f)
			{
				return;
			}
			float num2 = (worldPosition.z - vector.z) / fogOfWarSize.z + 0.5f;
			if (num2 < 0f || num2 > 1f)
			{
				return;
			}
			int width = _fogOfWarTexture.width;
			int height = _fogOfWarTexture.height;
			int num3 = (int)(num * (float)width);
			int num4 = (int)(num2 * (float)height);
			float num5 = 0.0001f + smoothness;
			byte b = (byte)(fogNewAlpha * 255f);
			float num6 = radius / fogOfWarSize.z;
			int num7 = (int)((float)height * num6);
			int num8 = num7 * num7;
			for (int i = num4 - num7; i <= num4 + num7; i++)
			{
				if (i <= 0 || i >= height - 1)
				{
					continue;
				}
				for (int j = num3 - num7; j <= num3 + num7; j++)
				{
					if (j <= 0 || j >= width - 1)
					{
						continue;
					}
					int num9 = (num4 - i) * (num4 - i) + (num3 - j) * (num3 - j);
					if (num9 > num8)
					{
						continue;
					}
					int num10 = i * width + j;
					Color32 color = fogOfWarColorBuffer[num10];
					if (!blendAlpha)
					{
						color.a = byte.MaxValue;
					}
					num9 = num8 - num9;
					float num11 = (float)num9 / ((float)num8 * num5);
					num11 = 1f - num11;
					if (num11 < 0f)
					{
						num11 = 0f;
					}
					else if (num11 > 1f)
					{
						num11 = 1f;
					}
					byte b2 = (byte)((float)(int)b + (float)(color.a - b) * num11);
					if (b2 >= byte.MaxValue || color.a == b2)
					{
						continue;
					}
					if (duration > 0f)
					{
						AddFogOfWarTransitionSlot(j, i, color.a, b2, 0f, duration);
						continue;
					}
					color.a = b2;
					fogOfWarColorBuffer[num10] = color;
					requiresTextureUpload = true;
					if (restoreDelay > 0f)
					{
						AddFogOfWarTransitionSlot(j, i, b2, byte.MaxValue, restoreDelay, restoreDuration);
					}
				}
			}
		}

		public void SetFogOfWarAlpha(Bounds bounds, float fogNewAlpha, float duration)
		{
			SetFogOfWarAlpha(bounds, fogNewAlpha, blendAlpha: true, duration, fogOfWarSmoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
		}

		public void SetFogOfWarAlpha(Bounds bounds, float fogNewAlpha, float duration, float smoothness)
		{
			SetFogOfWarAlpha(bounds, fogNewAlpha, blendAlpha: true, duration, smoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
		}

		public void SetFogOfWarAlpha(Bounds bounds, float fogNewAlpha, bool blendAlpha, float duration, float smoothness, float restoreDelay, float restoreDuration)
		{
			if (_fogOfWarTexture == null || fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0)
			{
				return;
			}
			Vector3 vector = anchoredFogOfWarCenter;
			Vector3 center = bounds.center;
			float num = (center.x - vector.x) / fogOfWarSize.x + 0.5f;
			if (num < 0f || num > 1f)
			{
				return;
			}
			float num2 = (center.z - vector.z) / fogOfWarSize.z + 0.5f;
			if (num2 < 0f || num2 > 1f)
			{
				return;
			}
			int width = _fogOfWarTexture.width;
			int height = _fogOfWarTexture.height;
			int num3 = (int)(num * (float)width);
			int num4 = (int)(num2 * (float)height);
			byte b = (byte)(fogNewAlpha * 255f);
			float num5 = bounds.extents.z / fogOfWarSize.z;
			float num6 = bounds.extents.x / fogOfWarSize.x;
			float num7 = ((num6 > num5) ? 1f : (num5 / num6));
			float num8 = ((num6 > num5) ? (num6 / num5) : 1f);
			int num9 = (int)((float)height * num5);
			int num10 = num9 * num9;
			int num11 = (int)((float)width * num6);
			int num12 = num11 * num11;
			float num13 = 0.0001f + smoothness;
			for (int i = num4 - num9; i <= num4 + num9; i++)
			{
				if (i <= 0 || i >= height - 1)
				{
					continue;
				}
				int num14 = (num4 - i) * (num4 - i);
				num14 = num10 - num14;
				float num15 = (float)num14 * num7 / ((float)num10 * num13);
				for (int j = num3 - num11; j <= num3 + num11; j++)
				{
					if (j <= 0 || j >= width - 1)
					{
						continue;
					}
					int num16 = (num3 - j) * (num3 - j);
					int num17 = i * width + j;
					Color32 color = fogOfWarColorBuffer[num17];
					if (!blendAlpha)
					{
						color.a = byte.MaxValue;
					}
					num16 = num12 - num16;
					float num18 = (float)num16 * num8 / ((float)num12 * num13);
					float num19 = ((num15 < num18) ? num15 : num18);
					num19 = 1f - num19;
					if (num19 < 0f)
					{
						num19 = 0f;
					}
					else if (num19 > 1f)
					{
						num19 = 1f;
					}
					byte b2 = (byte)((float)(int)b + (float)(color.a - b) * num19);
					if (b2 >= byte.MaxValue || color.a == b2)
					{
						continue;
					}
					if (duration > 0f)
					{
						AddFogOfWarTransitionSlot(j, i, color.a, b2, 0f, duration);
						continue;
					}
					color.a = b2;
					fogOfWarColorBuffer[num17] = color;
					requiresTextureUpload = true;
					if (restoreDelay > 0f)
					{
						AddFogOfWarTransitionSlot(j, i, b2, byte.MaxValue, restoreDelay, restoreDuration);
					}
				}
			}
		}

		public void ResetFogOfWarAlpha(Vector3 worldPosition, float radius)
		{
			if (_fogOfWarTexture == null || fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0)
			{
				return;
			}
			Vector3 vector = anchoredFogOfWarCenter;
			float num = (worldPosition.x - vector.x) / fogOfWarSize.x + 0.5f;
			if (num < 0f || num > 1f)
			{
				return;
			}
			float num2 = (worldPosition.z - vector.z) / fogOfWarSize.z + 0.5f;
			if (num2 < 0f || num2 > 1f)
			{
				return;
			}
			int width = _fogOfWarTexture.width;
			int height = _fogOfWarTexture.height;
			int num3 = (int)(num * (float)width);
			int num4 = (int)(num2 * (float)height);
			float num5 = radius / fogOfWarSize.z;
			int num6 = (int)((float)height * num5);
			int num7 = num6 * num6;
			for (int i = num4 - num6; i <= num4 + num6; i++)
			{
				if (i <= 0 || i >= height - 1)
				{
					continue;
				}
				for (int j = num3 - num6; j <= num3 + num6; j++)
				{
					if (j > 0 && j < width - 1 && (num4 - i) * (num4 - i) + (num3 - j) * (num3 - j) <= num7)
					{
						int num8 = i * width + j;
						Color32 color = fogOfWarColorBuffer[num8];
						color.a = byte.MaxValue;
						fogOfWarColorBuffer[num8] = color;
						requiresTextureUpload = true;
					}
				}
			}
		}

		public void ResetFogOfWarAlpha(Bounds bounds)
		{
			ResetFogOfWarAlpha(bounds.center, bounds.extents.x, bounds.extents.z);
		}

		public void ResetFogOfWarAlpha(Vector3 position, Vector3 size)
		{
			ResetFogOfWarAlpha(position, size.x * 0.5f, size.z * 0.5f);
		}

		public void ResetFogOfWarAlpha(Vector3 position, float extentsX, float extentsZ)
		{
			if (_fogOfWarTexture == null || fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0)
			{
				return;
			}
			Vector3 vector = anchoredFogOfWarCenter;
			float num = (position.x - vector.x) / fogOfWarSize.x + 0.5f;
			if (num < 0f || num > 1f)
			{
				return;
			}
			float num2 = (position.z - vector.z) / fogOfWarSize.z + 0.5f;
			if (num2 < 0f || num2 > 1f)
			{
				return;
			}
			int width = _fogOfWarTexture.width;
			int height = _fogOfWarTexture.height;
			int num3 = (int)(num * (float)width);
			int num4 = (int)(num2 * (float)height);
			float num5 = extentsZ / fogOfWarSize.z;
			float num6 = extentsX / fogOfWarSize.x;
			int num7 = (int)((float)height * num5);
			int num8 = (int)((float)width * num6);
			for (int i = num4 - num7; i <= num4 + num7; i++)
			{
				if (i <= 0 || i >= height - 1)
				{
					continue;
				}
				for (int j = num3 - num8; j <= num3 + num8; j++)
				{
					if (j > 0 && j < width - 1)
					{
						int num9 = i * width + j;
						Color32 color = fogOfWarColorBuffer[num9];
						color.a = byte.MaxValue;
						fogOfWarColorBuffer[num9] = color;
						requiresTextureUpload = true;
					}
				}
			}
		}

		public void ResetFogOfWar(byte alpha = byte.MaxValue)
		{
			if (!(_fogOfWarTexture == null))
			{
				int height = _fogOfWarTexture.height;
				int width = _fogOfWarTexture.width;
				int num = height * width;
				if (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length != num)
				{
					fogOfWarColorBuffer = new Color32[num];
				}
				Color32 color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha);
				for (int i = 0; i < num; i++)
				{
					fogOfWarColorBuffer[i] = color;
				}
				_fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
				_fogOfWarTexture.Apply();
				lastTransitionPos = -1;
				fowTransitionIndices.Clear();
			}
		}

		private void AddFogOfWarTransitionSlot(int x, int y, byte initialAlpha, byte targetAlpha, float delay, float duration)
		{
			int key = y * 64000 + x;
			if (!fowTransitionIndices.TryGetValue(key, out var value))
			{
				value = -1;
				for (int i = 0; i <= lastTransitionPos; i++)
				{
					if (!fowTransitionList[i].enabled)
					{
						value = i;
						fowTransitionIndices[key] = value;
						break;
					}
				}
			}
			if (value >= 0 && fowTransitionList[value].enabled && (fowTransitionList[value].x != x || fowTransitionList[value].y != y))
			{
				value = -1;
			}
			if (value < 0)
			{
				if (lastTransitionPos >= 9999)
				{
					return;
				}
				value = ++lastTransitionPos;
				fowTransitionIndices[key] = value;
			}
			else if (fowTransitionList[value].enabled)
			{
				return;
			}
			fowTransitionList[value].x = x;
			fowTransitionList[value].y = y;
			fowTransitionList[value].duration = duration;
			fowTransitionList[value].startTime = Time.time;
			fowTransitionList[value].startDelay = delay;
			fowTransitionList[value].initialAlpha = initialAlpha;
			fowTransitionList[value].targetAlpha = targetAlpha;
			fowTransitionList[value].enabled = true;
		}

		public float GetFogOfWarAlpha(Vector3 worldPosition)
		{
			if (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0 || _fogOfWarTexture == null)
			{
				return 1f;
			}
			float num = (worldPosition.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
			if (num < 0f || num > 1f)
			{
				return 1f;
			}
			float num2 = (worldPosition.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
			if (num2 < 0f || num2 > 1f)
			{
				return 1f;
			}
			int width = _fogOfWarTexture.width;
			int height = _fogOfWarTexture.height;
			int num3 = (int)(num * (float)width);
			int num4 = (int)(num2 * (float)height) * width + num3;
			if (num4 < 0 || num4 >= fogOfWarColorBuffer.Length)
			{
				return 1f;
			}
			return (float)(int)fogOfWarColorBuffer[num4].a / 255f;
		}

		private void DisposeSurfaceCapture()
		{
			DisableSurfaceCapture();
			if (rt != null)
			{
				rt.Release();
				UnityEngine.Object.DestroyImmediate(rt);
			}
		}

		private void CheckSurfaceCapture()
		{
			if (!(cam == null))
			{
				return;
			}
			Transform transform = base.transform.Find("SurfaceCam");
			if (transform != null)
			{
				cam = transform.GetComponent<Camera>();
				if (cam == null)
				{
					UnityEngine.Object.DestroyImmediate(transform.gameObject);
				}
			}
		}

		private void DisableSurfaceCapture()
		{
			if (cam != null)
			{
				cam.enabled = false;
			}
		}

		private void SurfaceCaptureSupportCheck()
		{
			Transform transform = base.transform.Find("SurfaceCam");
			if (transform != null)
			{
				cam = transform.GetComponent<Camera>();
			}
			if (!activeProfile.terrainFit)
			{
				DisposeSurfaceCapture();
				return;
			}
			if (cam == null)
			{
				if (transform != null)
				{
					UnityEngine.Object.DestroyImmediate(transform.gameObject);
				}
				if (cam == null)
				{
					GameObject gameObject = new GameObject("SurfaceCam", typeof(Camera));
					gameObject.transform.SetParent(base.transform, worldPositionStays: false);
					cam = gameObject.GetComponent<Camera>();
					cam.depthTextureMode = DepthTextureMode.None;
					cam.clearFlags = CameraClearFlags.Depth;
					cam.allowHDR = false;
					cam.allowMSAA = false;
				}
				cam.stereoTargetEye = StereoTargetEyeMask.None;
			}
			UniversalAdditionalCameraData component = cam.GetComponent<UniversalAdditionalCameraData>();
			if (component != null)
			{
				component.dithering = false;
				component.renderPostProcessing = false;
				component.renderShadows = false;
				component.requiresColorTexture = false;
				component.requiresDepthTexture = false;
				component.stopNaN = false;
			}
			cam.orthographic = true;
			cam.nearClipPlane = 1f;
			if (rt != null && rt.width != (int)activeProfile.terrainFitResolution)
			{
				if (cam.targetTexture == rt)
				{
					cam.targetTexture = null;
				}
				rt.Release();
				UnityEngine.Object.DestroyImmediate(rt);
			}
			if (rt == null)
			{
				rt = new RenderTexture((int)activeProfile.terrainFitResolution, (int)activeProfile.terrainFitResolution, 24, RenderTextureFormat.Depth);
				rt.antiAliasing = 1;
			}
			int num = 1 << base.gameObject.layer;
			if (((int)activeProfile.terrainLayerMask & num) != 0)
			{
				VolumetricFogProfile volumetricFogProfile = activeProfile;
				volumetricFogProfile.terrainLayerMask = (int)volumetricFogProfile.terrainLayerMask & ~num;
			}
			cam.cullingMask = activeProfile.terrainLayerMask;
			cam.targetTexture = rt;
			if (activeProfile.terrainFit)
			{
				ScheduleHeightmapCapture();
			}
			else
			{
				cam.enabled = false;
			}
		}

		public void ScheduleHeightmapCapture()
		{
			if (cam != null)
			{
				cam.enabled = true;
				camStartFrameCount = Time.frameCount;
				if (!fogMat.IsKeywordEnabled("VF2_SURFACE"))
				{
					fogMat.EnableKeyword("VF2_SURFACE");
				}
			}
		}

		private void SetupCameraCaptureMatrix()
		{
			Vector3 position = base.transform.position + new Vector3(0f, base.transform.lossyScale.y * 0.51f, 0f);
			cam.farClipPlane = 10000f;
			cam.transform.position = position;
			cam.transform.eulerAngles = new Vector3(90f, 0f, 0f);
			Vector3 lossyScale = base.transform.lossyScale;
			cam.orthographicSize = Mathf.Max(lossyScale.x * 0.5f, lossyScale.z * 0.5f);
			ComputeSufaceTransform(cam.projectionMatrix, cam.worldToCameraMatrix);
			fogMat.SetMatrix(ShaderParams.SurfaceCaptureMatrix, camMatrix);
			fogMat.SetTexture(ShaderParams.SurfaceDepthTexture, cam.targetTexture);
			fogMat.SetVector(ShaderParams.SurfaceData, new Vector4(position.y, activeProfile.terrainFogHeight, activeProfile.terrainFogMinAltitude, activeProfile.terrainFogMaxAltitude));
		}

		private void SurfaceCaptureUpdate()
		{
			if (activeProfile.terrainFit && !(cam == null))
			{
				SetupCameraCaptureMatrix();
				if (!cam.enabled && lastCamPos != cam.transform.position)
				{
					lastCamPos = cam.transform.position;
					ScheduleHeightmapCapture();
					requireUpdateMaterial = true;
				}
				else if (Time.frameCount > camStartFrameCount + 1 && Application.isPlaying)
				{
					cam.enabled = false;
				}
			}
		}

		private void ComputeSufaceTransform(Matrix4x4 proj, Matrix4x4 view)
		{
			if (SystemInfo.usesReversedZBuffer)
			{
				proj.m20 = 0f - proj.m20;
				proj.m21 = 0f - proj.m21;
				proj.m22 = 0f - proj.m22;
				proj.m23 = 0f - proj.m23;
			}
			Matrix4x4 matrix4x = proj * view;
			Matrix4x4 matrix4x2 = identityMatrix;
			matrix4x2.m00 = 0.5f;
			matrix4x2.m11 = 0.5f;
			matrix4x2.m22 = 0.5f;
			matrix4x2.m03 = 0.5f;
			matrix4x2.m23 = 0.5f;
			matrix4x2.m13 = 0.5f;
			camMatrix = matrix4x2 * matrix4x;
		}
	}
}
