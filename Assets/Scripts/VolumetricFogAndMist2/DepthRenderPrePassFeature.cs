using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFogAndMist2
{
	public class DepthRenderPrePassFeature : ScriptableRendererFeature
	{
		public class DepthRenderPass : ScriptableRenderPass
		{
			public static readonly List<Renderer> cutOutRenderers = new List<Renderer>();

			public static int transparentLayerMask;

			public static int alphaCutoutLayerMask;

			private const string m_ProfilerTag = "CustomDepthPrePass";

			private const string m_DepthOnlyShader = "Hidden/VolumetricFog2/DepthOnly";

			private FilteringSettings m_FilteringSettings;

			private int currentCutoutLayerMask;

			private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();

			private RTHandle m_Depth;

			private Material depthOnlyMaterial;

			private Material depthOnlyMaterialCutOff;

			private Material[] depthOverrideMaterials;

			public DepthRenderPass()
			{
				RenderTargetIdentifier tex = new RenderTargetIdentifier(ShaderParams.CustomDepthTexture, 0, CubemapFace.Unknown, -1);
				m_Depth = RTHandles.Alloc(tex, "_CustomDepthTexture");
				m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
				m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
				m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
				m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, 0);
				SetupKeywords();
				FindAlphaClippingRenderers();
			}

			private void SetupKeywords()
			{
				if (transparentLayerMask != 0 || alphaCutoutLayerMask != 0)
				{
					Shader.EnableKeyword("VF2_DEPTH_PREPASS");
				}
				else
				{
					Shader.DisableKeyword("VF2_DEPTH_PREPASS");
				}
			}

			public static void SetupLayerMasks(int transparentLayerMask, int alphaCutoutLayerMask)
			{
				DepthRenderPass.transparentLayerMask = transparentLayerMask;
				DepthRenderPass.alphaCutoutLayerMask = alphaCutoutLayerMask;
				if (alphaCutoutLayerMask != 0)
				{
					FindAlphaClippingRenderers();
				}
			}

			public static void FindAlphaClippingRenderers()
			{
				cutOutRenderers.Clear();
				if (alphaCutoutLayerMask == 0)
				{
					return;
				}
				Renderer[] array = Object.FindObjectsOfType<Renderer>();
				for (int i = 0; i < array.Length; i++)
				{
					if (((1 << array[i].gameObject.layer) & alphaCutoutLayerMask) != 0)
					{
						cutOutRenderers.Add(array[i]);
					}
				}
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				if (transparentLayerMask != m_FilteringSettings.layerMask || alphaCutoutLayerMask != currentCutoutLayerMask)
				{
					m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent, transparentLayerMask);
					currentCutoutLayerMask = alphaCutoutLayerMask;
					SetupKeywords();
				}
				RenderTextureDescriptor desc = cameraTextureDescriptor;
				desc.colorFormat = RenderTextureFormat.Depth;
				desc.depthBufferBits = 24;
				desc.msaaSamples = 1;
				cmd.GetTemporaryRT(ShaderParams.CustomDepthTexture, desc, FilterMode.Point);
				cmd.SetGlobalTexture(ShaderParams.CustomDepthTexture, m_Depth);
				ConfigureTarget(m_Depth);
				ConfigureClear(ClearFlag.All, Color.black);
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				if (transparentLayerMask == 0 && alphaCutoutLayerMask == 0)
				{
					return;
				}
				CommandBuffer commandBuffer = CommandBufferPool.Get("CustomDepthPrePass");
				context.ExecuteCommandBuffer(commandBuffer);
				commandBuffer.Clear();
				if (alphaCutoutLayerMask != 0)
				{
					VolumetricFogManager managerIfExists = VolumetricFogManager.GetManagerIfExists();
					if (managerIfExists != null)
					{
						if (depthOnlyMaterialCutOff == null)
						{
							Shader shader = Shader.Find("Hidden/VolumetricFog2/DepthOnly");
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
								material.SetFloat(ShaderParams.CustomDepthAlphaCutoff, managerIfExists.alphaCutOff);
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
				}
				if (transparentLayerMask != 0)
				{
					SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
					DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
					drawingSettings.perObjectData = PerObjectData.None;
					if (depthOnlyMaterial == null)
					{
						Shader shader2 = Shader.Find("Hidden/VolumetricFog2/DepthOnly");
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
				cmd?.ReleaseTemporaryRT(ShaderParams.CustomDepthTexture);
			}

			public void CleanUp()
			{
				RTHandles.Release(m_Depth);
			}
		}

		private DepthRenderPass m_ScriptablePass;

		public static bool installed;

		[Tooltip("Specify which cameras can execute this render feature. If you have several cameras in your scene, make sure only the correct cameras use this feature in order to optimize performance.")]
		public LayerMask cameraLayerMask = -1;

		[Tooltip("Ignores reflection probes from executing this render feature")]
		public bool ignoreReflectionProbes = true;

		public override void Create()
		{
			m_ScriptablePass = new DepthRenderPass
			{
				renderPassEvent = RenderPassEvent.AfterRenderingOpaques
			};
		}

		private void OnDestroy()
		{
			installed = false;
			if (m_ScriptablePass != null)
			{
				m_ScriptablePass.CleanUp();
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;
			if (((int)cameraLayerMask & (1 << camera.gameObject.layer)) != 0 && (!ignoreReflectionProbes || camera.cameraType != CameraType.Reflection) && (!(camera.targetTexture != null) || camera.targetTexture.format != RenderTextureFormat.Depth))
			{
				installed = true;
				renderer.EnqueuePass(m_ScriptablePass);
			}
		}
	}
}
