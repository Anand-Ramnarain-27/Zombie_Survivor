using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricLights
{
	public class VolumetricLightsDepthPrePassFeature : ScriptableRendererFeature
	{
		private static class ShaderParams
		{
			public static int MainTex = Shader.PropertyToID("_MainTex");

			public static int CustomDepthTexture = Shader.PropertyToID("_CustomDepthTexture");

			public static int CustomDepthAlphaCutoff = Shader.PropertyToID("_AlphaCutOff");

			public static int CustomDepthBaseMap = Shader.PropertyToID("_BaseMap");

			public const string SKW_DEPTH_PREPASS = "VF2_DEPTH_PREPASS";

			public const string SKW_CUSTOM_DEPTH_ALPHA_TEST = "DEPTH_PREPASS_ALPHA_TEST";
		}

		public class DepthRenderPass : ScriptableRenderPass
		{
			public VolumetricLightsDepthPrePassFeature settings;

			private const string m_ProfilerTag = "CustomDepthPrePass";

			private const string m_DepthOnlyShader = "Hidden/VolumetricLights/DepthOnly";

			private FilteringSettings m_FilteringSettings;

			private int currentCutoutLayerMask;

			private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

			private readonly List<Renderer> cutOutRenderers = new List<Renderer>();

			private RenderTargetHandle m_Depth;

			private Material depthOnlyMaterial;

			private Material depthOnlyMaterialCutOff;

			private Material[] depthOverrideMaterials;

			public DepthRenderPass(VolumetricLightsDepthPrePassFeature settings)
			{
				this.settings = settings;
				base.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
				m_Depth.Init("_CustomDepthTexture");
				m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
				m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
				m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
				m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, 0);
				SetupKeywords();
				FindAlphaClippingRenderers();
			}

			private void SetupKeywords()
			{
				if ((int)settings.transparentLayerMask != 0 || (int)settings.alphaCutoutLayerMask != 0)
				{
					Shader.EnableKeyword("VF2_DEPTH_PREPASS");
				}
				else
				{
					Shader.DisableKeyword("VF2_DEPTH_PREPASS");
				}
			}

			private void FindAlphaClippingRenderers()
			{
				cutOutRenderers.Clear();
				if ((int)settings.alphaCutoutLayerMask == 0)
				{
					return;
				}
				Renderer[] array = Object.FindObjectsOfType<Renderer>();
				for (int i = 0; i < array.Length; i++)
				{
					if (((1 << array[i].gameObject.layer) & (int)settings.alphaCutoutLayerMask) != 0)
					{
						cutOutRenderers.Add(array[i]);
					}
				}
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				if ((int)settings.transparentLayerMask != m_FilteringSettings.layerMask || (int)settings.alphaCutoutLayerMask != currentCutoutLayerMask)
				{
					m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, settings.transparentLayerMask);
					if ((int)settings.alphaCutoutLayerMask != currentCutoutLayerMask)
					{
						FindAlphaClippingRenderers();
					}
					currentCutoutLayerMask = settings.alphaCutoutLayerMask;
					SetupKeywords();
				}
				RenderTextureDescriptor desc = cameraTextureDescriptor;
				desc.colorFormat = RenderTextureFormat.Depth;
				desc.depthBufferBits = 32;
				desc.msaaSamples = 1;
				cmd.GetTemporaryRT(m_Depth.id, desc, FilterMode.Point);
				cmd.SetGlobalTexture(ShaderParams.CustomDepthTexture, m_Depth.Identifier());
				ConfigureTarget(m_Depth.Identifier());
				ConfigureClear(ClearFlag.All, Color.black);
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				if ((int)settings.transparentLayerMask == 0 && (int)settings.alphaCutoutLayerMask == 0)
				{
					return;
				}
				CommandBuffer commandBuffer = CommandBufferPool.Get("CustomDepthPrePass");
				context.ExecuteCommandBuffer(commandBuffer);
				commandBuffer.Clear();
				if ((int)settings.alphaCutoutLayerMask != 0)
				{
					if (depthOnlyMaterialCutOff == null)
					{
						Shader shader = Shader.Find("Hidden/VolumetricLights/DepthOnly");
						depthOnlyMaterialCutOff = new Material(shader);
					}
					int count = cutOutRenderers.Count;
					if (depthOverrideMaterials == null || depthOverrideMaterials.Length < count)
					{
						depthOverrideMaterials = new Material[count];
					}
					for (int i = 0; i < count; i++)
					{
						Renderer renderer = cutOutRenderers[i];
						if (!(renderer != null) || !renderer.isVisible)
						{
							continue;
						}
						Material sharedMaterial = renderer.sharedMaterial;
						if (sharedMaterial != null)
						{
							if (depthOverrideMaterials[i] == null)
							{
								depthOverrideMaterials[i] = Object.Instantiate(depthOnlyMaterialCutOff);
								depthOverrideMaterials[i].EnableKeyword("DEPTH_PREPASS_ALPHA_TEST");
							}
							Material material = depthOverrideMaterials[i];
							material.SetFloat(ShaderParams.CustomDepthAlphaCutoff, settings.alphaCutOff);
							if (sharedMaterial.HasProperty(ShaderParams.CustomDepthBaseMap))
							{
								material.SetTexture(ShaderParams.MainTex, sharedMaterial.GetTexture(ShaderParams.CustomDepthBaseMap));
							}
							else if (sharedMaterial.HasProperty(ShaderParams.MainTex))
							{
								material.SetTexture(ShaderParams.MainTex, sharedMaterial.GetTexture(ShaderParams.MainTex));
							}
							commandBuffer.DrawRenderer(renderer, material);
						}
					}
				}
				if ((int)settings.transparentLayerMask != 0)
				{
					SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
					DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
					drawingSettings.perObjectData = PerObjectData.None;
					if (depthOnlyMaterial == null)
					{
						Shader shader2 = Shader.Find("Hidden/VolumetricLights/DepthOnly");
						depthOnlyMaterial = new Material(shader2);
					}
					drawingSettings.overrideMaterial = depthOnlyMaterial;
					context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
				}
				context.ExecuteCommandBuffer(commandBuffer);
				CommandBufferPool.Release(commandBuffer);
			}

			public override void FrameCleanup(CommandBuffer cmd)
			{
				cmd?.ReleaseTemporaryRT(m_Depth.id);
			}
		}

		[Tooltip("Optionally specify which transparent layers must be included in the depth prepass. Use only to avoid fog clipping with certain transparent objects.")]
		public LayerMask transparentLayerMask;

		[Tooltip("Optionally specify which semi-transparent (materials using alpha clipping or cut-off) must be included in the depth prepass. Use only to avoid fog clipping with certain transparent objects.")]
		public LayerMask alphaCutoutLayerMask;

		[Tooltip("Optionally determines the alpha cut off for semitransparent objects")]
		[Range(0f, 1f)]
		public float alphaCutOff;

		private DepthRenderPass m_ScriptablePass;

		public static bool installed;

		public override void Create()
		{
			m_ScriptablePass = new DepthRenderPass(this);
		}

		private void OnDestroy()
		{
			installed = false;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;
			if (!(camera.targetTexture != null) || camera.targetTexture.format != RenderTextureFormat.Depth)
			{
				installed = true;
				renderer.EnqueuePass(m_ScriptablePass);
			}
		}
	}
}
