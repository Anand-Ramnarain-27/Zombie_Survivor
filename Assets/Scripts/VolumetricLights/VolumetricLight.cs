using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace VolumetricLights
{
	[ExecuteAlways]
	[RequireComponent(typeof(Light))]
	[AddComponentMenu("Effects/Volumetric Light", 1000)]
	public class VolumetricLight : MonoBehaviour
	{
		private static class ShaderParams
		{
			public static int RayMarchSettings = Shader.PropertyToID("_RayMarchSettings");

			public static int Density = Shader.PropertyToID("_Density");

			public static int FallOff = Shader.PropertyToID("_FallOff");

			public static int RangeFallOff = Shader.PropertyToID("_DistanceFallOff");

			public static int Penumbra = Shader.PropertyToID("_Border");

			public static int NoiseFinalMultiplier = Shader.PropertyToID("_NoiseFinalMultiplier");

			public static int NoiseScale = Shader.PropertyToID("_NoiseScale");

			public static int NoiseStrength = Shader.PropertyToID("_NoiseStrength");

			public static int NoiseTex = Shader.PropertyToID("_NoiseTex");

			public static int BlendDest = Shader.PropertyToID("_BlendDest");

			public static int BlendSrc = Shader.PropertyToID("_BlendSrc");

			public static int FlipDepthTexture = Shader.PropertyToID("_FlipDepthTexture");

			public static int AreaExtents = Shader.PropertyToID("_AreaExtents");

			public static int BoundsExtents = Shader.PropertyToID("_BoundsExtents");

			public static int BoundsCenter = Shader.PropertyToID("_BoundsCenter");

			public static int ExtraGeoData = Shader.PropertyToID("_ExtraGeoData");

			public static int ConeAxis = Shader.PropertyToID("_ConeAxis");

			public static int ConeTipData = Shader.PropertyToID("_ConeTipData");

			public static int WorldToLocalMatrix = Shader.PropertyToID("_WorldToLocal");

			public static int ToLightDir = Shader.PropertyToID("_ToLightDir");

			public static int WindDirection = Shader.PropertyToID("_WindDirection");

			public static int LightColor = Shader.PropertyToID("_LightColor");

			public static int ParticleLightColor = Shader.PropertyToID("_ParticleLightColor");

			public static int ParticleDistanceAtten = Shader.PropertyToID("_ParticleDistanceAtten");

			public static int CookieTexture = Shader.PropertyToID("_Cookie2D");

			public static int BlueNoiseTexture = Shader.PropertyToID("_BlueNoise");

			public static int ShadowTexture = Shader.PropertyToID("_ShadowTexture");

			public static int ShadowCubemap = Shader.PropertyToID("_ShadowCubemap");

			public static int ShadowIntensity = Shader.PropertyToID("_ShadowIntensity");

			public static int ShadowMatrix = Shader.PropertyToID("_ShadowMatrix");

			public static int LightPos = Shader.PropertyToID("_LightPos");

			public static int InvVPMatrix = Shader.PropertyToID("_I_VP_Matrix");

			public const string SKW_NOISE = "VL_NOISE";

			public const string SKW_BLUENOISE = "VL_BLUENOISE";

			public const string SKW_SPOT = "VL_SPOT";

			public const string SKW_SPOT_COOKIE = "VL_SPOT_COOKIE";

			public const string SKW_POINT = "VL_POINT";

			public const string SKW_AREA_RECT = "VL_AREA_RECT";

			public const string SKW_AREA_DISC = "VL_AREA_DISC";

			public const string SKW_SHADOWS = "VL_SHADOWS";

			public const string SKW_SHADOWS_CUBEMAP = "VL_SHADOWS_CUBEMAP";

			public const string SKW_DIFFUSION = "VL_DIFFUSION";

			public const string SKW_PHYSICAL_ATTEN = "VL_PHYSICAL_ATTEN";

			public const string SKW_CUSTOM_BOUNDS = "VL_CUSTOM_BOUNDS";
		}

		public bool profileSync = true;

		public bool useCustomBounds;

		public Bounds bounds;

		public VolumetricLightProfile profile;

		public float customRange = 1f;

		[Tooltip("Used for point light occlusion orientation and checking camera distance when autoToggle options are enabled. If not assigned, it will try to use the main camera.")]
		public Transform targetCamera;

		public bool useCustomSize;

		public float areaWidth = 1f;

		public float areaHeight = 1f;

		[NonSerialized]
		public Light lightComp;

		private const float GOLDEN_RATIO = 0.618034f;

		private MeshFilter mf;

		[NonSerialized]
		public MeshRenderer meshRenderer;

		private Material fogMat;

		private Material fogMatLight;

		private Material fogMatInvisible;

		private Vector4 windDirectionAcum;

		private bool requireUpdateMaterial;

		private List<string> keywords;

		private static Texture2D blueNoiseTex;

		private float distanceToCameraSqr;

		[NonSerialized]
		public static Transform mainCamera;

		private float lastDistanceCheckTime;

		private bool wasInRange;

		public static List<VolumetricLight> volumetricLights = new List<VolumetricLight>();

		private const int SIDES = 32;

		private readonly List<Vector3> vertices = new List<Vector3>(32);

		private readonly List<int> indices = new List<int>(32);

		private float generatedRange = -1f;

		private float generatedTipRadius = -1f;

		private float generatedSpotAngle = -1f;

		private float generatedBaseRadius;

		private float generatedAreaWidth;

		private float generatedAreaHeight;

		private float generatedAreaFrustumAngle;

		private float generatedAreaFrustumMultiplier;

		private LightType generatedType;

		private static Vector3[] faceVerticesForward = new Vector3[4]
		{
			new Vector3(0.5f, -0.5f, 1f),
			new Vector3(0.5f, 0.5f, 1f),
			new Vector3(-0.5f, -0.5f, 1f),
			new Vector3(-0.5f, 0.5f, 1f)
		};

		private static Vector3[] faceVerticesBack = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f),
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f)
		};

		private static Vector3[] faceVerticesLeft = new Vector3[4]
		{
			new Vector3(-0.5f, -0.5f, 1f),
			new Vector3(-0.5f, 0.5f, 1f),
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f)
		};

		private static Vector3[] faceVerticesRight = new Vector3[4]
		{
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f),
			new Vector3(0.5f, -0.5f, 1f),
			new Vector3(0.5f, 0.5f, 1f)
		};

		private static Vector3[] faceVerticesTop = new Vector3[4]
		{
			new Vector3(-0.5f, 0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 1f),
			new Vector3(0.5f, 0.5f, 0f),
			new Vector3(0.5f, 0.5f, 1f)
		};

		private static Vector3[] faceVerticesBottom = new Vector3[4]
		{
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(0.5f, -0.5f, 1f),
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(-0.5f, -0.5f, 1f)
		};

		private const string PARTICLE_SYSTEM_NAME = "DustParticles";

		private Material particleMaterial;

		[NonSerialized]
		public ParticleSystem ps;

		private ParticleSystemRenderer psRenderer;

		private Vector3 psLastPos;

		private Quaternion psLastRot;

		[Header("Rendering")]
		public BlendMode blendMode;

		public RaymarchPresets raymarchPreset;

		[Tooltip("Determines the general accuracy of the effect. The greater this value, the more accurate effect (shadow occlusion as well). Try to keep this value as low as possible while maintainig a good visual result. If you need better performance increase the 'Raymarch Min Step' and then 'Jittering' amount to improve quality.")]
		[Range(1f, 256f)]
		public int raymarchQuality = 8;

		[Tooltip("Determines the minimum step size. Increase to improve performance / decrease to improve accuracy. When increasing this value, you can also increase 'Jittering' amount to improve quality.")]
		public float raymarchMinStep = 0.1f;

		[Tooltip("Increase to reduce inaccuracy due to low number of samples (due to a high raymarch step size).")]
		public float jittering = 0.5f;

		[Tooltip("Increase to reduce banding artifacts. Usually jittering has a bigger impact in reducing artifacts.")]
		[Range(0f, 2f)]
		public float dithering = 1f;

		[Tooltip("Uses blue noise for jittering computation reducing moiré pattern of normal jitter. Usually not needed unless you use shadow occlusion. It adds an additional texture fetch so use only if it provides you a clear visual improvement.")]
		public bool useBlueNoise;

		[Tooltip("The render queue for this renderer. By default, all transparent objects use a render queue of 3000. Use a lower value to render before all transparent objects.")]
		public int renderQueue = 3101;

		[Tooltip("Optional sorting layer Id (number) for this renderer. By default 0. Usually used to control the order with other transparent renderers, like Sprite Renderer.")]
		public int sortingLayerID;

		[Tooltip("Optional sorting order for this renderer. Used to control the order with other transparent renderers, like Sprite Renderer.")]
		public int sortingOrder;

		[Tooltip("Use only if depth texture is inverted. Alternatively you can enable MSAA, HDR or change the render scale in the pipeline asset.")]
		public bool flipDepthTexture;

		[Tooltip("Ignores light enable state. Always show volumetric fog. This option is useful to produce fake volumetric lights.")]
		public bool alwaysOn;

		[Tooltip("Fully enable/disable volumetric effect when far from main camera in order to optimize performance")]
		public bool autoToggle;

		[Tooltip("Distance to the light source at which the volumetric effect starts to dim. Note that the distance is to the light position regardless of its light range or volume so you should consider the light area or range into this distance as well.")]
		public float distanceStartDimming = 70f;

		[Tooltip("Distance to the light source at which the volumetric effect is fully deactivated. Note that the distance is to the light position regardless of its light range or volume so you should consider the light area or range into this distance as well.")]
		public float distanceDeactivation = 100f;

		[Tooltip("Minimum time between distance checks")]
		public float autoToggleCheckInterval = 1f;

		[Header("Appearance")]
		public bool useNoise = true;

		public Texture3D noiseTexture;

		[Range(0f, 3f)]
		public float noiseStrength = 1f;

		public float noiseScale = 5f;

		public float noiseFinalMultiplier = 1f;

		public float density = 0.2f;

		public Color mediumAlbedo = Color.white;

		[Tooltip("Overall brightness multiplier.")]
		public float brightness = 1f;

		[Tooltip("Attenuation Mode")]
		public AttenuationMode attenuationMode;

		[Tooltip("Constant coefficient (a) of the attenuation formula. By modulating these coefficients, you can tweak the attenuation quadratic curve 1/(a + b*x + c*x*x).")]
		public float attenCoefConstant = 1f;

		[Tooltip("Linear coefficient (b) of the attenuation formula. By modulating these coefficients, you can tweak the attenuation quadratic curve 1/(a + b*x + c*x*x).")]
		public float attenCoefLinear = 2f;

		[Tooltip("Quadratic coefficient (c) of the attenuation formula. By modulating these coefficients, you can tweak the attenuation quadratic curve 1/(a + b*x + c*x*x).")]
		public float attenCoefQuadratic = 1f;

		[Tooltip("Attenuation of light intensity based on square of distance. Plays with brightness to achieve a more linear or realistic (quadratic attenuation effect).")]
		[FormerlySerializedAs("distanceFallOff")]
		public float rangeFallOff = 1f;

		[Tooltip("Brightiness increase when looking against light source.")]
		public float diffusionIntensity;

		[Range(0f, 1f)]
		[Tooltip("Smooth edges")]
		[FormerlySerializedAs("border")]
		public float penumbra = 0.5f;

		[Tooltip("Radius of the tip of the cone. Only applies to spot lights.")]
		public float tipRadius;

		[Tooltip("Custom cookie texture (RGB).")]
		public Texture2D cookieTexture;

		[Range(0f, 80f)]
		public float frustumAngle;

		[Header("Animation")]
		[Tooltip("Noise animation direction and speed.")]
		public Vector3 windDirection = new Vector3(0.03f, 0.02f, 0f);

		[Header("Dust Particles")]
		public bool enableDustParticles;

		public float dustBrightness = 0.6f;

		public float dustMinSize = 0.01f;

		public float dustMaxSize = 0.02f;

		public float dustWindSpeed = 1f;

		[Tooltip("Dims particle intensity beyond this distance to camera")]
		public float dustDistanceAttenuation = 10f;

		[Tooltip("Fully enable/disable particle system renderer when far from main camera in order to optimize performance")]
		public bool dustAutoToggle;

		[Tooltip("Distance to the light source at which the particule system is fully deactivated. Note that the distance is to the light position regardless of its light range or volume so you should consider the light area or range into this distance as well.")]
		public float dustDistanceDeactivation = 70f;

		[Header("Shadow Occlusion")]
		public bool enableShadows;

		public float shadowIntensity = 0.7f;

		public ShadowResolution shadowResolution = ShadowResolution._256;

		public LayerMask shadowCullingMask = -3;

		public ShadowBakeInterval shadowBakeInterval;

		public float shadowNearDistance = 0.1f;

		[Tooltip("Fully enable/disable shadows when far from main camera in order to optimize performance")]
		public bool shadowAutoToggle;

		[Tooltip("Max distance to main camera to render shadows for this volumetric light.")]
		public float shadowDistanceDeactivation = 250f;

		[Tooltip("Compute shadows in a half-sphere oriented to camera (faster) or in a cubemap but render one face per frame (slower) or all 6 faces per frame (slowest).")]
		public ShadowBakeMode shadowBakeMode;

		[Tooltip("Only for point lights: specify the direction for the baked shadows (shadows are captured in a half sphere or 180º). You can choose a fixed direction or make the shadows be aligned with the direction to the player camera.")]
		public ShadowOrientation shadowOrientation;

		[Tooltip("For performance reasons, point light shadows are captured on half a sphere (180º). By default, the shadows are captured in the direction to the user camera but you can specify a fixed direction using this option.")]
		public Vector3 shadowDirection = Vector3.down;

		private const string SHADOW_CAM_NAME = "OcclusionCam";

		private Camera cam;

		private RenderTexture rt;

		private int camStartFrameCount;

		private Vector3 lastCamPos;

		private Quaternion lastCamRot;

		private bool usesReversedZBuffer;

		private static Matrix4x4 textureScaleAndBias;

		private Matrix4x4 shadowMatrix;

		private bool camTransformChanged;

		private bool shouldOrientToCamera;

		private RenderTexture shadowCubemap;

		private static readonly Vector3[] camFaceDirections = new Vector3[6]
		{
			Vector3.right,
			Vector3.left,
			Vector3.up,
			Vector3.down,
			Vector3.forward,
			Vector3.back
		};

		private Material copyDepthIntoCubemap;

		private int currentCubemapFace;

		[Obsolete("Settings property is now deprecated. Settings are now part of the Volumetric Light component itself, for example: VolumetricLight.density instead of VolumetricLight.settings.density.")]
		public VolumetricLightProfile settings
		{
			get
			{
				return profile;
			}
			set
			{
				Debug.Log("Changing values through settings is deprecated. If you want to get or set the profile for this light, use the profile property. Or simply set the properties now directly to the volumetric light component. For example: VolumetricLight.density = xxx.");
			}
		}

		private bool usesCubemap
		{
			get
			{
				if (shadowBakeMode != 0)
				{
					return generatedType == LightType.Point;
				}
				return false;
			}
		}

		public event PropertiesChangedEvent OnPropertiesChanged;

		private void OnEnable()
		{
			Init();
		}

		public void Init()
		{
			volumetricLights.Add(this);
			lightComp = GetComponent<Light>();
			if (base.gameObject.layer == 0)
			{
				base.gameObject.layer = 1;
			}
			SettingsInit();
			Refresh();
		}

		public void Refresh()
		{
			if (base.enabled)
			{
				CheckProfile();
				DestroyMesh();
				CheckMesh();
				CheckShadows();
				UpdateMaterialPropertiesNow();
			}
		}

		private void OnValidate()
		{
			requireUpdateMaterial = true;
		}

		public void OnDidApplyAnimationProperties()
		{
			requireUpdateMaterial = true;
		}

		private void OnDisable()
		{
			if (volumetricLights.Contains(this))
			{
				volumetricLights.Remove(this);
			}
			TurnOff();
		}

		private void TurnOff()
		{
			if (meshRenderer != null)
			{
				meshRenderer.enabled = false;
			}
			ShadowsDisable();
			ParticlesDisable();
		}

		private void OnDestroy()
		{
			if (fogMatInvisible != null)
			{
				UnityEngine.Object.DestroyImmediate(fogMatInvisible);
				fogMatInvisible = null;
			}
			if (fogMatLight != null)
			{
				UnityEngine.Object.DestroyImmediate(fogMatLight);
				fogMatLight = null;
			}
			if (meshRenderer != null)
			{
				meshRenderer.enabled = false;
			}
			ShadowsDispose();
		}

		private void LateUpdate()
		{
			if (lightComp.isActiveAndEnabled || alwaysOn)
			{
				if (meshRenderer != null && !meshRenderer.enabled)
				{
					requireUpdateMaterial = true;
				}
				if (CheckMesh())
				{
					if (!Application.isPlaying)
					{
						ParticlesDisable();
					}
					ScheduleShadowCapture();
					requireUpdateMaterial = true;
				}
				if (requireUpdateMaterial)
				{
					requireUpdateMaterial = false;
					UpdateMaterialPropertiesNow();
				}
				if (fogMat == null || meshRenderer == null)
				{
					return;
				}
				UpdateVolumeGeometry();
				float time = Time.time;
				if ((dustAutoToggle || shadowAutoToggle || autoToggle) && (!Application.isPlaying || time - lastDistanceCheckTime >= autoToggleCheckInterval))
				{
					lastDistanceCheckTime = time;
					ComputeDistanceToCamera();
				}
				float num = brightness;
				if (autoToggle)
				{
					float num2 = distanceDeactivation * distanceDeactivation;
					float num3 = distanceStartDimming * distanceStartDimming;
					if (num3 > num2)
					{
						num3 = num2;
					}
					float num4 = 1f - Mathf.Clamp01((distanceToCameraSqr - num3) / (num2 - num3));
					num *= num4;
					bool flag = num4 > 0f;
					if (flag != wasInRange)
					{
						wasInRange = flag;
						meshRenderer.enabled = flag;
					}
				}
				UpdateDiffusionTerm();
				if (enableDustParticles)
				{
					if (!Application.isPlaying)
					{
						ParticlesResetIfTransformChanged();
					}
					UpdateParticlesVisibility();
				}
				fogMat.SetColor(ShaderParams.LightColor, lightComp.color * mediumAlbedo * (lightComp.intensity * num));
				float deltaTime = Time.deltaTime;
				windDirectionAcum.x += windDirection.x * deltaTime;
				windDirectionAcum.y += windDirection.y * deltaTime;
				windDirectionAcum.z += windDirection.z * deltaTime;
				windDirectionAcum.w = 0.618034f * (float)(Time.frameCount % 480);
				fogMat.SetVector(ShaderParams.WindDirection, windDirectionAcum);
				ShadowsUpdate();
			}
			else if (meshRenderer != null && meshRenderer.enabled)
			{
				TurnOff();
			}
		}

		private void ComputeDistanceToCamera()
		{
			if (mainCamera == null)
			{
				mainCamera = targetCamera;
				if (mainCamera == null && Camera.main != null)
				{
					mainCamera = Camera.main.transform;
				}
				if (mainCamera == null)
				{
					return;
				}
			}
			Vector3 position = mainCamera.position;
			Vector3 center = bounds.center;
			distanceToCameraSqr = (position - center).sqrMagnitude;
		}

		private void UpdateDiffusionTerm()
		{
			Vector4 value = -base.transform.forward;
			value.w = diffusionIntensity;
			fogMat.SetVector(ShaderParams.ToLightDir, value);
		}

		public void UpdateVolumeGeometry()
		{
			UpdateVolumeGeometryMaterial(fogMat);
			if (enableDustParticles && particleMaterial != null)
			{
				UpdateVolumeGeometryMaterial(particleMaterial);
				particleMaterial.SetMatrix(ShaderParams.WorldToLocalMatrix, base.transform.worldToLocalMatrix);
			}
			NormalizeScale();
		}

		private void UpdateVolumeGeometryMaterial(Material mat)
		{
			if (!(mat == null))
			{
				Vector4 value = base.transform.position;
				value.w = tipRadius;
				mat.SetVector(ShaderParams.ConeTipData, value);
				Vector4 value2 = base.transform.forward * generatedRange;
				float num = (value2.w = generatedRange * generatedRange);
				mat.SetVector(ShaderParams.ConeAxis, value2);
				float num2 = Mathf.Max(0.0001f, rangeFallOff);
				float y = -1f / (num * num2);
				float z = num / (num * num2);
				mat.SetVector(ShaderParams.ExtraGeoData, new Vector4(generatedBaseRadius, y, z, generatedRange));
				if (!useCustomBounds)
				{
					bounds = meshRenderer.bounds;
				}
				mat.SetVector(ShaderParams.BoundsCenter, bounds.center);
				mat.SetVector(ShaderParams.BoundsExtents, bounds.extents);
				if (generatedType == LightType.Area)
				{
					float w = (generatedAreaFrustumMultiplier - 1f) / generatedRange;
					mat.SetVector(ShaderParams.AreaExtents, new Vector4(areaWidth * 0.5f, areaHeight * 0.5f, generatedRange, w));
				}
				else if (generatedType == LightType.Disc)
				{
					float w2 = (generatedAreaFrustumMultiplier - 1f) / generatedRange;
					mat.SetVector(ShaderParams.AreaExtents, new Vector4(areaWidth * areaWidth, areaHeight, generatedRange, w2));
				}
			}
		}

		public void UpdateMaterialProperties()
		{
			requireUpdateMaterial = true;
		}

		private void UpdateMaterialPropertiesNow()
		{
			wasInRange = false;
			lastDistanceCheckTime = -999f;
			mainCamera = null;
			ComputeDistanceToCamera();
			if (this == null || !base.isActiveAndEnabled || lightComp == null || (!lightComp.isActiveAndEnabled && !alwaysOn))
			{
				ShadowsDisable();
				return;
			}
			SettingsValidate();
			if (meshRenderer == null)
			{
				meshRenderer = GetComponent<MeshRenderer>();
			}
			if (fogMatLight == null)
			{
				fogMatLight = new Material(Shader.Find("VolumetricLights/VolumetricLightURP"));
				fogMatLight.hideFlags = HideFlags.DontSave;
			}
			fogMat = fogMatLight;
			if (fogMat == null)
			{
				return;
			}
			SetFogMaterial();
			if (customRange < 0.001f)
			{
				customRange = 0.001f;
			}
			if (meshRenderer != null)
			{
				meshRenderer.sortingLayerID = sortingLayerID;
				meshRenderer.sortingOrder = sortingOrder;
			}
			fogMat.renderQueue = renderQueue;
			switch (blendMode)
			{
			case BlendMode.Additive:
				fogMat.SetInt(ShaderParams.BlendSrc, 1);
				fogMat.SetInt(ShaderParams.BlendDest, 1);
				break;
			case BlendMode.Blend:
				fogMat.SetInt(ShaderParams.BlendSrc, 1);
				fogMat.SetInt(ShaderParams.BlendDest, 10);
				break;
			case BlendMode.PreMultiply:
				fogMat.SetInt(ShaderParams.BlendSrc, 5);
				fogMat.SetInt(ShaderParams.BlendDest, 1);
				break;
			}
			fogMat.SetTexture(ShaderParams.NoiseTex, noiseTexture);
			fogMat.SetFloat(ShaderParams.NoiseStrength, noiseStrength);
			fogMat.SetFloat(ShaderParams.NoiseScale, 0.1f / noiseScale);
			fogMat.SetFloat(ShaderParams.NoiseFinalMultiplier, noiseFinalMultiplier);
			fogMat.SetFloat(ShaderParams.Penumbra, penumbra);
			fogMat.SetFloat(ShaderParams.RangeFallOff, rangeFallOff);
			fogMat.SetVector(ShaderParams.FallOff, new Vector3(attenCoefConstant, attenCoefLinear, attenCoefQuadratic));
			fogMat.SetFloat(ShaderParams.Density, density);
			fogMat.SetVector(ShaderParams.RayMarchSettings, new Vector4(raymarchQuality, dithering * 0.001f, jittering, raymarchMinStep));
			if (jittering > 0f)
			{
				if (blueNoiseTex == null)
				{
					blueNoiseTex = Resources.Load<Texture2D>("Textures/blueNoiseVL");
				}
				fogMat.SetTexture(ShaderParams.BlueNoiseTexture, blueNoiseTex);
			}
			fogMat.SetInt(ShaderParams.FlipDepthTexture, flipDepthTexture ? 1 : 0);
			if (keywords == null)
			{
				keywords = new List<string>();
			}
			else
			{
				keywords.Clear();
			}
			if (useBlueNoise)
			{
				keywords.Add("VL_BLUENOISE");
			}
			if (useNoise)
			{
				keywords.Add("VL_NOISE");
			}
			switch (lightComp.type)
			{
			case LightType.Spot:
				if (cookieTexture != null)
				{
					keywords.Add("VL_SPOT_COOKIE");
					fogMat.SetTexture(ShaderParams.CookieTexture, cookieTexture);
				}
				else
				{
					keywords.Add("VL_SPOT");
				}
				break;
			case LightType.Point:
				keywords.Add("VL_POINT");
				break;
			case LightType.Area:
				keywords.Add("VL_AREA_RECT");
				break;
			case LightType.Disc:
				keywords.Add("VL_AREA_DISC");
				break;
			}
			if (attenuationMode == AttenuationMode.Quadratic)
			{
				keywords.Add("VL_PHYSICAL_ATTEN");
			}
			if (diffusionIntensity > 0f)
			{
				keywords.Add("VL_DIFFUSION");
			}
			if (useCustomBounds)
			{
				keywords.Add("VL_CUSTOM_BOUNDS");
			}
			ShadowsSupportCheck();
			if (enableShadows)
			{
				if (usesCubemap)
				{
					keywords.Add("VL_SHADOWS_CUBEMAP");
				}
				else
				{
					keywords.Add("VL_SHADOWS");
				}
			}
			fogMat.enabledKeywords = null;
			fogMat.shaderKeywords = keywords.ToArray();
			ParticlesCheckSupport();
			if (this.OnPropertiesChanged != null)
			{
				this.OnPropertiesChanged(this);
			}
		}

		private void SetFogMaterial()
		{
			if (!(meshRenderer != null))
			{
				return;
			}
			if (density <= 0f || mediumAlbedo.a == 0f)
			{
				if (fogMatInvisible == null)
				{
					fogMatInvisible = new Material(Shader.Find("VolumetricLights/Invisible"));
					fogMatInvisible.hideFlags = HideFlags.DontSave;
				}
				meshRenderer.sharedMaterial = fogMatInvisible;
			}
			else
			{
				meshRenderer.sharedMaterial = fogMat;
			}
		}

		public void CheckProfile()
		{
			if (profile != null)
			{
				if ("Auto".Equals(profile.name))
				{
					profile.ApplyTo(this);
					profile = null;
				}
				else if (profileSync)
				{
					profile.ApplyTo(this);
				}
			}
		}

		private void DestroyMesh()
		{
			mf = GetComponent<MeshFilter>();
			if (mf != null && mf.sharedMesh != null)
			{
				//UnityEngine.Object.DestroyImmediate(mf.sharedMesh);
			}
		}

		private bool CheckMesh()
		{
			if (meshRenderer != null && !autoToggle)
			{
				bool flag = lightComp.enabled || alwaysOn;
				meshRenderer.enabled = flag;
			}
			if (!useCustomSize)
			{
				customRange = lightComp.range;
			}
			bool flag2 = false;
			MeshFilter component = GetComponent<MeshFilter>();
			if (component == null || component.sharedMesh == null)
			{
				flag2 = true;
			}
			switch (lightComp.type)
			{
			case LightType.Spot:
				if (flag2 || generatedType != 0 || customRange != generatedRange || lightComp.spotAngle != generatedSpotAngle || tipRadius != generatedTipRadius)
				{
					GenerateConeMesh();
					return true;
				}
				break;
			case LightType.Point:
				if (flag2 || generatedType != LightType.Point || customRange != generatedRange)
				{
					GenerateSphereMesh();
					return true;
				}
				break;
			case LightType.Area:
				if (flag2 || generatedType != LightType.Area || customRange != generatedRange || areaWidth != generatedAreaWidth || areaHeight != generatedAreaHeight || frustumAngle != generatedAreaFrustumAngle)
				{
					GenerateCubeMesh();
					return true;
				}
				break;
			case LightType.Disc:
				if (flag2 || generatedType != LightType.Disc || customRange != generatedRange || areaWidth != generatedAreaWidth || frustumAngle != generatedAreaFrustumAngle)
				{
					GenerateCylinderMesh();
					return true;
				}
				break;
			default:
				if (meshRenderer != null)
				{
					meshRenderer.enabled = false;
				}
				break;
			}
			return false;
		}

		private void UpdateMesh(string name)
		{
			mf = GetComponent<MeshFilter>();
			if (mf == null)
			{
				mf = base.gameObject.AddComponent<MeshFilter>();
			}
			Mesh mesh = mf.sharedMesh;
			if (mesh == null)
			{
				mesh = new Mesh();
			}
			else
			{
				mesh.Clear();
			}
			mesh.name = name;
			mesh.SetVertices(vertices);
			mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			mf.mesh = mesh;
			meshRenderer = GetComponent<MeshRenderer>();
			if (meshRenderer == null)
			{
				meshRenderer = base.gameObject.AddComponent<MeshRenderer>();
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
			}
			else if (!autoToggle)
			{
				meshRenderer.enabled = true;
			}
		}

		private void NormalizeScale()
		{
			Transform parent = base.transform.parent;
			if (parent == null)
			{
				return;
			}
			Vector3 lossyScale = parent.lossyScale;
			if ((lossyScale.x != 1f || lossyScale.y != 1f || lossyScale.z != 1f) && lossyScale.x != 0f && lossyScale.y != 0f && lossyScale.z != 0f)
			{
				lossyScale.x = 1f / lossyScale.x;
				lossyScale.y = 1f / lossyScale.y;
				lossyScale.z = 1f / lossyScale.z;
				if (base.transform.localScale != lossyScale)
				{
					base.transform.localScale = lossyScale;
				}
			}
		}

		private void GenerateConeMesh()
		{
			NormalizeScale();
			vertices.Clear();
			indices.Clear();
			generatedType = LightType.Spot;
			generatedTipRadius = tipRadius;
			generatedSpotAngle = lightComp.spotAngle;
			generatedRange = customRange;
			generatedBaseRadius = Mathf.Tan(lightComp.spotAngle * (MathF.PI / 180f) * 0.5f) * customRange;
			Vector3 zero = Vector3.zero;
			for (int i = 0; i < 32; i++)
			{
				float f = MathF.PI * 2f * (float)i / 32f;
				float num = Mathf.Cos(f);
				float num2 = Mathf.Sin(f);
				zero.x = num * generatedTipRadius;
				zero.y = num2 * generatedTipRadius;
				zero.z = 0f;
				vertices.Add(zero);
				zero.x = num * generatedBaseRadius;
				zero.y = num2 * generatedBaseRadius;
				zero.z = customRange;
				vertices.Add(zero);
			}
			int num3 = 64;
			for (int j = 0; j < num3; j += 2)
			{
				int item = j;
				int item2 = j + 1;
				int item3 = (j + 2) % num3;
				int item4 = (j + 3) % num3;
				indices.Add(item);
				indices.Add(item3);
				indices.Add(item2);
				indices.Add(item3);
				indices.Add(item4);
				indices.Add(item2);
			}
			vertices.Add(Vector3.zero);
			int item5 = num3;
			for (int k = 0; k < num3; k += 2)
			{
				indices.Add(k);
				indices.Add(item5);
				indices.Add((k + 2) % num3);
			}
			vertices.Add(new Vector3(0f, 0f, customRange));
			int item6 = num3 + 1;
			for (int l = 0; l < num3; l += 2)
			{
				indices.Add(item6);
				indices.Add(l + 1);
				indices.Add((l + 3) % num3);
			}
			UpdateMesh("Capped Cone");
		}

		private void GenerateCubeMesh()
		{
			NormalizeScale();
			generatedType = LightType.Area;
			generatedRange = customRange;
			generatedAreaWidth = areaWidth;
			generatedAreaHeight = areaHeight;
			generatedAreaFrustumAngle = frustumAngle;
			generatedAreaFrustumMultiplier = 1f + Mathf.Tan(frustumAngle * (MathF.PI / 180f));
			vertices.Clear();
			indices.Clear();
			AddFace(faceVerticesBack);
			AddFace(faceVerticesForward);
			AddFace(faceVerticesLeft);
			AddFace(faceVerticesRight);
			AddFace(faceVerticesTop);
			AddFace(faceVerticesBottom);
			UpdateMesh("Box");
		}

		private void AddFace(Vector3[] faceVertices)
		{
			int count = vertices.Count;
			for (int i = 0; i < faceVertices.Length; i++)
			{
				Vector3 item = faceVertices[i];
				item.x *= generatedAreaWidth;
				if (item.z > 0f)
				{
					item.x *= generatedAreaFrustumMultiplier;
					item.y *= generatedAreaFrustumMultiplier;
				}
				item.y *= generatedAreaHeight;
				item.z *= generatedRange;
				vertices.Add(item);
			}
			indices.Add(count);
			indices.Add(count + 1);
			indices.Add(count + 3);
			indices.Add(count + 3);
			indices.Add(count + 2);
			indices.Add(count);
		}

		private void GenerateSphereMesh()
		{
			NormalizeScale();
			generatedRange = customRange;
			generatedType = LightType.Point;
			vertices.Clear();
			indices.Clear();
			vertices.Add(Vector3.up * generatedRange);
			for (int i = 0; i < 16; i++)
			{
				float f = MathF.PI * (float)(i + 1) / 17f;
				float num = Mathf.Sin(f);
				float y = Mathf.Cos(f);
				for (int j = 0; j <= 24; j++)
				{
					float f2 = MathF.PI * 2f * (float)((j != 24) ? j : 0) / 24f;
					float num2 = Mathf.Sin(f2);
					float num3 = Mathf.Cos(f2);
					vertices.Add(new Vector3(num * num3, y, num * num2) * generatedRange);
				}
			}
			vertices.Add(Vector3.down * generatedRange);
			for (int k = 0; k < 24; k++)
			{
				indices.Add(k + 2);
				indices.Add(k + 1);
				indices.Add(0);
			}
			for (int l = 0; l < 15; l++)
			{
				for (int m = 0; m < 24; m++)
				{
					int num4 = m + l * 25 + 1;
					int num5 = num4 + 24 + 1;
					indices.Add(num4);
					indices.Add(num4 + 1);
					indices.Add(num5 + 1);
					indices.Add(num4);
					indices.Add(num5 + 1);
					indices.Add(num5);
				}
			}
			int count = vertices.Count;
			for (int n = 0; n < 24; n++)
			{
				indices.Add(count - 1);
				indices.Add(count - (n + 2) - 1);
				indices.Add(count - (n + 1) - 1);
			}
			UpdateMesh("Sphere");
		}

		private void GenerateCylinderMesh()
		{
			NormalizeScale();
			generatedType = LightType.Disc;
			generatedRange = customRange;
			generatedAreaWidth = (generatedAreaHeight = areaWidth);
			generatedAreaFrustumAngle = frustumAngle;
			generatedAreaFrustumMultiplier = 1f + Mathf.Tan(frustumAngle * (MathF.PI / 180f));
			vertices.Clear();
			indices.Clear();
			Vector3 zero = Vector3.zero;
			for (int i = 0; i < 32; i++)
			{
				float f = MathF.PI * 2f * (float)i / 32f;
				float num = Mathf.Cos(f);
				float num2 = Mathf.Sin(f);
				zero.x = num * generatedAreaWidth;
				zero.y = num2 * generatedAreaWidth;
				zero.z = 0f;
				vertices.Add(zero);
				zero.x *= generatedAreaFrustumMultiplier;
				zero.y *= generatedAreaFrustumMultiplier;
				zero.z = generatedRange;
				vertices.Add(zero);
			}
			int num3 = 64;
			for (int j = 0; j < num3; j += 2)
			{
				int item = j;
				int item2 = j + 1;
				int item3 = (j + 2) % num3;
				int item4 = (j + 3) % num3;
				indices.Add(item);
				indices.Add(item3);
				indices.Add(item2);
				indices.Add(item3);
				indices.Add(item4);
				indices.Add(item2);
			}
			vertices.Add(Vector3.zero);
			int item5 = num3;
			for (int k = 0; k < num3; k += 2)
			{
				indices.Add(k);
				indices.Add(item5);
				indices.Add((k + 2) % num3);
			}
			vertices.Add(new Vector3(0f, 0f, generatedRange));
			int item6 = num3 + 1;
			for (int l = 0; l < num3; l += 2)
			{
				indices.Add(item6);
				indices.Add(l + 1);
				indices.Add((l + 3) % num3);
			}
			UpdateMesh("Cylinder");
		}

		private void ParticlesDisable()
		{
			if (Application.isPlaying)
			{
				if (psRenderer != null)
				{
					psRenderer.enabled = false;
				}
			}
			else if (ps != null)
			{
				ps.gameObject.SetActive(value: false);
			}
		}

		private void ParticlesResetIfTransformChanged()
		{
			if (ps != null && (ps.transform.position != psLastPos || ps.transform.rotation != psLastRot))
			{
				ParticlesPopulate();
			}
		}

		private void ParticlesPopulate()
		{
			ps.Clear();
			ps.Simulate(100f);
			psLastPos = ps.transform.position;
			psLastRot = ps.transform.rotation;
		}

		private void ParticlesCheckSupport()
		{
			if (!enableDustParticles)
			{
				ParticlesDisable();
				return;
			}
			bool flag = false;
			if (ps == null)
			{
				Transform transform = base.transform.Find("DustParticles");
				if (transform != null)
				{
					ps = transform.GetComponent<ParticleSystem>();
					if (ps == null)
					{
						UnityEngine.Object.DestroyImmediate(transform.gameObject);
					}
				}
				if (ps == null)
				{
					GameObject gameObject = Resources.Load<GameObject>("Prefabs/DustParticles");
					if (gameObject == null)
					{
						return;
					}
					gameObject = UnityEngine.Object.Instantiate(gameObject);
					gameObject.name = "DustParticles";
					gameObject.transform.SetParent(base.transform, worldPositionStays: false);
					ps = gameObject.GetComponent<ParticleSystem>();
				}
				ps.gameObject.layer = 1;
				flag = true;
			}
			if (particleMaterial == null)
			{
				particleMaterial = UnityEngine.Object.Instantiate(Resources.Load<Material>("Materials/DustParticle"));
			}
			if (keywords == null)
			{
				keywords = new List<string>();
			}
			else
			{
				keywords.Clear();
			}
			if (useCustomBounds)
			{
				keywords.Add("VL_CUSTOM_BOUNDS");
			}
			switch (generatedType)
			{
			case LightType.Spot:
				if (cookieTexture != null)
				{
					keywords.Add("VL_SPOT_COOKIE");
					particleMaterial.SetTexture(ShaderParams.CookieTexture, cookieTexture);
				}
				else
				{
					keywords.Add("VL_SPOT");
				}
				break;
			case LightType.Point:
				keywords.Add("VL_POINT");
				break;
			case LightType.Area:
				keywords.Add("VL_AREA_RECT");
				break;
			case LightType.Disc:
				keywords.Add("VL_AREA_DISC");
				break;
			}
			if (attenuationMode == AttenuationMode.Quadratic)
			{
				keywords.Add("VL_PHYSICAL_ATTEN");
			}
			if (enableShadows)
			{
				if (usesCubemap)
				{
					keywords.Add("VL_SHADOWS_CUBEMAP");
				}
				else
				{
					keywords.Add("VL_SHADOWS");
				}
			}
			particleMaterial.shaderKeywords = keywords.ToArray();
			particleMaterial.renderQueue = renderQueue + 1;
			particleMaterial.SetFloat(ShaderParams.Penumbra, penumbra);
			particleMaterial.SetFloat(ShaderParams.RangeFallOff, rangeFallOff);
			particleMaterial.SetVector(ShaderParams.FallOff, new Vector3(attenCoefConstant, attenCoefLinear, attenCoefQuadratic));
			particleMaterial.SetColor(ShaderParams.ParticleLightColor, lightComp.color * mediumAlbedo * (lightComp.intensity * dustBrightness));
			particleMaterial.SetFloat(ShaderParams.ParticleDistanceAtten, dustDistanceAttenuation * dustDistanceAttenuation);
			if (psRenderer == null)
			{
				psRenderer = ps.GetComponent<ParticleSystemRenderer>();
			}
			psRenderer.material = particleMaterial;
			ParticleSystem.MainModule main = ps.main;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			ParticleSystem.MinMaxCurve startSize = main.startSize;
			startSize.mode = ParticleSystemCurveMode.TwoConstants;
			startSize.constantMin = dustMinSize;
			startSize.constantMax = dustMaxSize;
			main.startSize = startSize;
			ParticleSystem.ShapeModule shape = ps.shape;
			switch (generatedType)
			{
			case LightType.Spot:
				shape.shapeType = ParticleSystemShapeType.ConeVolume;
				shape.angle = generatedSpotAngle * 0.5f;
				shape.position = Vector3.zero;
				shape.radius = tipRadius;
				shape.length = generatedRange;
				shape.scale = Vector3.one;
				break;
			case LightType.Point:
				shape.shapeType = ParticleSystemShapeType.Sphere;
				shape.position = Vector3.zero;
				shape.scale = Vector3.one;
				shape.radius = generatedRange;
				break;
			case LightType.Area:
			case LightType.Disc:
				shape.shapeType = ParticleSystemShapeType.Box;
				shape.position = new Vector3(0f, 0f, generatedRange * 0.5f);
				shape.scale = GetComponent<MeshFilter>().sharedMesh.bounds.size;
				break;
			}
			ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = ps.velocityOverLifetime;
			Vector3 vector = base.transform.InverseTransformDirection(windDirection);
			shape.position -= vector * dustWindSpeed * 10f;
			ParticleSystem.MinMaxCurve x = velocityOverLifetime.x;
			x.constantMin = -0.1f + vector.x * dustWindSpeed;
			x.constantMax = 0.1f + vector.x * dustWindSpeed;
			velocityOverLifetime.x = x;
			ParticleSystem.MinMaxCurve y = velocityOverLifetime.y;
			y.constantMin = -0.1f + vector.y * dustWindSpeed;
			y.constantMax = 0.1f + vector.y * dustWindSpeed;
			velocityOverLifetime.y = y;
			ParticleSystem.MinMaxCurve z = velocityOverLifetime.z;
			z.constantMin = -0.1f + vector.z * dustWindSpeed;
			z.constantMax = 0.1f + vector.z * dustWindSpeed;
			velocityOverLifetime.z = z;
			if (!ps.gameObject.activeSelf)
			{
				ps.gameObject.SetActive(value: true);
			}
			UpdateParticlesVisibility();
			if (flag || !Application.isPlaying || ps.particleCount == 0)
			{
				ParticlesPopulate();
			}
			if (!ps.isPlaying)
			{
				ps.Play();
			}
		}

		private void UpdateParticlesVisibility()
		{
			if (!Application.isPlaying || psRenderer == null)
			{
				return;
			}
			bool flag = meshRenderer.isVisible;
			if (flag && dustAutoToggle)
			{
				float num = dustDistanceDeactivation * dustDistanceDeactivation;
				flag = distanceToCameraSqr <= num;
			}
			if (flag)
			{
				if (!psRenderer.enabled)
				{
					psRenderer.enabled = true;
				}
			}
			else if (psRenderer.enabled)
			{
				psRenderer.enabled = false;
			}
		}

		private void SettingsInit()
		{
			if (noiseTexture == null)
			{
				noiseTexture = Resources.Load<Texture3D>("Textures/NoiseTex3D1");
			}
		}

		private void SettingsValidate()
		{
			switch (raymarchPreset)
			{
			case RaymarchPresets.Default:
				raymarchQuality = 8;
				raymarchMinStep = 0.1f;
				jittering = 0.5f;
				break;
			case RaymarchPresets.Faster:
				raymarchQuality = 4;
				raymarchMinStep = 0.2f;
				jittering = 1f;
				break;
			case RaymarchPresets.EvenFaster:
				raymarchQuality = 2;
				raymarchMinStep = 1f;
				jittering = 4f;
				break;
			case RaymarchPresets.LightSpeed:
				raymarchQuality = 1;
				raymarchMinStep = 8f;
				jittering = 4f;
				break;
			}
			tipRadius = Mathf.Max(0f, tipRadius);
			density = Mathf.Max(0f, density);
			noiseScale = Mathf.Max(0.1f, noiseScale);
			diffusionIntensity = Mathf.Max(0f, diffusionIntensity);
			dustMaxSize = Mathf.Max(dustMaxSize, dustMinSize);
			rangeFallOff = Mathf.Max(rangeFallOff, 0f);
			brightness = Mathf.Max(brightness, 0f);
			penumbra = Mathf.Max(0.002f, penumbra);
			attenCoefConstant = Mathf.Max(0.0001f, attenCoefConstant);
			attenCoefLinear = Mathf.Max(0f, attenCoefLinear);
			attenCoefQuadratic = Mathf.Max(0f, attenCoefQuadratic);
			dustBrightness = Mathf.Max(0f, dustBrightness);
			dustMinSize = Mathf.Max(0f, dustMinSize);
			dustMaxSize = Mathf.Max(0f, dustMaxSize);
			shadowNearDistance = Mathf.Max(0f, shadowNearDistance);
			dustDistanceAttenuation = Mathf.Max(0f, dustDistanceAttenuation);
			raymarchMinStep = Mathf.Max(0.1f, raymarchMinStep);
			jittering = Mathf.Max(0f, jittering);
			distanceStartDimming = Mathf.Max(0f, distanceStartDimming);
			distanceDeactivation = Mathf.Max(0f, distanceDeactivation);
			distanceStartDimming = Mathf.Min(distanceStartDimming, distanceDeactivation);
			shadowIntensity = Mathf.Max(0f, shadowIntensity);
			if (shadowDirection == Vector3.zero)
			{
				shadowDirection = Vector3.down;
			}
			else
			{
				shadowDirection.Normalize();
			}
		}

		private void CheckShadows()
		{
			if (!(cam == null))
			{
				return;
			}
			Transform transform = base.transform.Find("OcclusionCam");
			if (transform != null)
			{
				cam = transform.GetComponent<Camera>();
				if (cam == null)
				{
					UnityEngine.Object.DestroyImmediate(transform.gameObject);
				}
			}
		}

		private void ShadowsDisable()
		{
			if (cam != null)
			{
				cam.enabled = false;
			}
		}

		private void ShadowsDispose()
		{
			if (cam != null)
			{
				cam.targetTexture = null;
				cam.enabled = false;
			}
			if (rt != null)
			{
				rt.Release();
				UnityEngine.Object.DestroyImmediate(rt);
			}
			if (shadowCubemap != null)
			{
				shadowCubemap.Release();
				UnityEngine.Object.DestroyImmediate(shadowCubemap);
			}
		}

		private void ShadowsSupportCheck()
		{
			bool flag = cookieTexture != null && lightComp.type == LightType.Spot;
			if (!enableShadows && !flag)
			{
				ShadowsDispose();
				return;
			}
			usesReversedZBuffer = SystemInfo.usesReversedZBuffer;
			textureScaleAndBias = Matrix4x4.identity;
			textureScaleAndBias.m00 = 0.5f;
			textureScaleAndBias.m11 = 0.5f;
			textureScaleAndBias.m22 = 0.5f;
			textureScaleAndBias.m03 = 0.5f;
			textureScaleAndBias.m13 = 0.5f;
			textureScaleAndBias.m23 = 0.5f;
			if (cam == null)
			{
				Transform transform = base.transform.Find("OcclusionCam");
				if (transform != null)
				{
					cam = transform.GetComponent<Camera>();
					if (cam == null)
					{
						UnityEngine.Object.DestroyImmediate(transform.gameObject);
					}
				}
				if (cam == null)
				{
					GameObject gameObject = new GameObject("OcclusionCam", typeof(Camera));
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
				component.volumeLayerMask = 0;
			}
			switch (generatedType)
			{
			case LightType.Spot:
				cam.transform.localRotation = Quaternion.identity;
				cam.orthographic = false;
				cam.fieldOfView = generatedSpotAngle;
				break;
			case LightType.Point:
				cam.orthographic = false;
				if (shadowBakeMode != 0)
				{
					cam.fieldOfView = 90f;
				}
				else
				{
					cam.fieldOfView = 160f;
				}
				break;
			case LightType.Area:
			case LightType.Disc:
				cam.transform.localRotation = Quaternion.identity;
				cam.orthographic = true;
				break;
			}
			cam.nearClipPlane = shadowNearDistance;
			cam.orthographicSize = Mathf.Max(generatedAreaWidth, generatedAreaHeight);
			if (rt != null && rt.width != (int)shadowResolution)
			{
				if (cam.targetTexture == rt)
				{
					cam.targetTexture = null;
				}
				rt.Release();
				UnityEngine.Object.DestroyImmediate(rt);
				if (shadowCubemap != null)
				{
					shadowCubemap.Release();
					UnityEngine.Object.DestroyImmediate(shadowCubemap);
				}
			}
			if (rt == null)
			{
				rt = new RenderTexture((int)shadowResolution, (int)shadowResolution, 16, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
				rt.antiAliasing = 1;
				rt.useMipMap = false;
			}
			if (shadowCubemap == null && usesCubemap)
			{
				shadowCubemap = new RenderTexture((int)shadowResolution, (int)shadowResolution, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
				shadowCubemap.dimension = TextureDimension.Cube;
				shadowCubemap.antiAliasing = 1;
				shadowCubemap.useMipMap = false;
			}
			fogMat.SetVector(ShaderParams.ShadowIntensity, new Vector3(shadowIntensity, 1f - shadowIntensity));
			if (((uint)(int)shadowCullingMask & 2u) != 0)
			{
				shadowCullingMask = (int)shadowCullingMask & -3;
			}
			cam.cullingMask = shadowCullingMask;
			cam.targetTexture = rt;
			if (enableShadows)
			{
				shouldOrientToCamera = true;
				ScheduleShadowCapture();
			}
			else
			{
				cam.enabled = false;
			}
		}

		public void ScheduleShadowCapture()
		{
			if (cam == null)
			{
				return;
			}
			if (usesCubemap)
			{
				if (copyDepthIntoCubemap == null)
				{
					copyDepthIntoCubemap = new Material(Shader.Find("Hidden/VolumetricLights/CopyDepthIntoCubemap"));
				}
				copyDepthIntoCubemap.SetVector(ShaderParams.LightPos, cam.transform.position);
				RenderTexture active = RenderTexture.active;
				int num = ((shadowBakeMode == ShadowBakeMode.CubemapOneFacePerFrame && shadowBakeInterval == ShadowBakeInterval.EveryFrame) ? 1 : 6);
				for (int i = 0; i < num; i++)
				{
					int num2 = currentCubemapFace % 6;
					cam.transform.forward = camFaceDirections[num2];
					cam.Render();
					copyDepthIntoCubemap.SetMatrix(ShaderParams.InvVPMatrix, cam.cameraToWorldMatrix * GL.GetGPUProjectionMatrix(cam.projectionMatrix, renderIntoTexture: false).inverse);
					copyDepthIntoCubemap.SetTexture(ShaderParams.ShadowTexture, rt, RenderTextureSubElement.Depth);
					Graphics.SetRenderTarget(shadowCubemap, 0, (CubemapFace)num2);
					Graphics.Blit(rt, copyDepthIntoCubemap);
					currentCubemapFace++;
				}
				cam.enabled = false;
				RenderTexture.active = active;
				fogMat.SetTexture(ShaderParams.ShadowCubemap, shadowCubemap);
				if (enableDustParticles && particleMaterial != null)
				{
					particleMaterial.SetTexture(ShaderParams.ShadowCubemap, shadowCubemap);
				}
				if (!fogMat.IsKeywordEnabled("VL_SHADOWS_CUBEMAP"))
				{
					fogMat.EnableKeyword("VL_SHADOWS_CUBEMAP");
				}
			}
			else
			{
				cam.enabled = true;
				camStartFrameCount = Time.frameCount;
				if (!fogMat.IsKeywordEnabled("VL_SHADOWS"))
				{
					fogMat.EnableKeyword("VL_SHADOWS");
				}
			}
		}

		private void SetupShadowMatrix()
		{
			if (!usesCubemap)
			{
				ComputeShadowTransform(cam.projectionMatrix, cam.worldToCameraMatrix);
				fogMat.SetMatrix(ShaderParams.ShadowMatrix, shadowMatrix);
				fogMat.SetTexture(ShaderParams.ShadowTexture, cam.targetTexture, RenderTextureSubElement.Depth);
				if (enableDustParticles && particleMaterial != null)
				{
					particleMaterial.SetMatrix(ShaderParams.ShadowMatrix, shadowMatrix);
					particleMaterial.SetTexture(ShaderParams.ShadowTexture, cam.targetTexture, RenderTextureSubElement.Depth);
				}
			}
		}

		private void ShadowsUpdate()
		{
			bool flag = cookieTexture != null && lightComp.type == LightType.Spot;
			if ((!enableShadows && !flag) || cam == null)
			{
				return;
			}
			int frameCount = Time.frameCount;
			if (!meshRenderer.isVisible && frameCount - camStartFrameCount > 5)
			{
				if (cam.enabled)
				{
					ShadowsDisable();
				}
				return;
			}
			Transform transform = cam.transform;
			cam.farClipPlane = generatedRange;
			if (generatedType == LightType.Point && shadowBakeMode == ShadowBakeMode.HalfSphere)
			{
				if (shadowOrientation == ShadowOrientation.ToCamera)
				{
					if (enableShadows && mainCamera != null)
					{
						if (shadowBakeInterval != ShadowBakeInterval.EveryFrame && Vector3.Angle(transform.forward, mainCamera.position - lastCamPos) > 45f)
						{
							shouldOrientToCamera = true;
							ScheduleShadowCapture();
						}
						if (shouldOrientToCamera || shadowBakeInterval == ShadowBakeInterval.EveryFrame)
						{
							shouldOrientToCamera = false;
							transform.LookAt(mainCamera.position);
						}
					}
				}
				else
				{
					transform.forward = shadowDirection;
				}
			}
			camTransformChanged = false;
			if (lastCamPos != transform.position || lastCamRot != transform.rotation)
			{
				camTransformChanged = true;
				lastCamPos = transform.position;
				lastCamRot = transform.rotation;
			}
			if (enableShadows)
			{
				ShadowCamUpdate();
			}
			if (camTransformChanged || flag || cam.enabled)
			{
				SetupShadowMatrix();
			}
		}

		private void ShadowCamUpdate()
		{
			if (shadowAutoToggle)
			{
				float num = shadowDistanceDeactivation * shadowDistanceDeactivation;
				if (distanceToCameraSqr > num)
				{
					if (cam.enabled)
					{
						ShadowsDisable();
						if (fogMat.IsKeywordEnabled("VL_SHADOWS"))
						{
							fogMat.DisableKeyword("VL_SHADOWS");
						}
						if (fogMat.IsKeywordEnabled("VL_SHADOWS_CUBEMAP"))
						{
							fogMat.DisableKeyword("VL_SHADOWS_CUBEMAP");
						}
					}
					return;
				}
			}
			if (shadowBakeInterval == ShadowBakeInterval.OnStart)
			{
				if (!cam.enabled && camTransformChanged)
				{
					ScheduleShadowCapture();
				}
				else if (Application.isPlaying && Time.frameCount > camStartFrameCount + 1)
				{
					cam.enabled = false;
				}
			}
			else if (!cam.enabled)
			{
				ScheduleShadowCapture();
			}
		}

		private void ComputeShadowTransform(Matrix4x4 proj, Matrix4x4 view)
		{
			if (usesReversedZBuffer)
			{
				proj.m20 = 0f - proj.m20;
				proj.m21 = 0f - proj.m21;
				proj.m22 = 0f - proj.m22;
				proj.m23 = 0f - proj.m23;
			}
			Matrix4x4 matrix4x = proj * view;
			shadowMatrix = textureScaleAndBias * matrix4x;
		}
	}
}
