using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricFogAndMist2
{
	public class VolumetricFogRenderFeature : ScriptableRendererFeature
	{
		private static class ShaderParams
		{
			public const string LightBufferName = "_LightBuffer";

			public static int LightBuffer = Shader.PropertyToID("_LightBuffer");

			public static int LightBufferSize = Shader.PropertyToID("_VFRTSize");

			public static int MainTex = Shader.PropertyToID("_MainTex");

			public static int BlurRT = Shader.PropertyToID("_BlurTex");

			public static int BlurRT2 = Shader.PropertyToID("_BlurTex2");

			public static int MiscData = Shader.PropertyToID("_MiscData");

			public static int ForcedInvisible = Shader.PropertyToID("_ForcedInvisible");

			public static int DownsampledDepth = Shader.PropertyToID("_DownsampledDepth");

			public static int BlueNoiseTexture = Shader.PropertyToID("_BlueNoise");

			public static int BlurScale = Shader.PropertyToID("_BlurScale");

			public static int Downscaling = Shader.PropertyToID("_Downscaling");

			public const string SKW_DITHER = "DITHER";

			public const string SKW_EDGE_PRESERVE = "EDGE_PRESERVE";

			public const string SKW_EDGE_PRESERVE_UPSCALING = "EDGE_PRESERVE_UPSCALING";
		}

		private class VolumetricFogRenderPass : ScriptableRenderPass
		{
			private FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent);

			private readonly List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();

			private const int renderingLayer = 262144;

			private const string m_ProfilerTag = "Volumetric Fog Light Buffer Rendering";

			private RTHandle m_LightBuffer;

			public VolumetricFogRenderPass()
			{
				shaderTagIdList.Clear();
				shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
				RenderTargetIdentifier tex = new RenderTargetIdentifier(ShaderParams.LightBuffer, 0, CubemapFace.Unknown, -1);
				m_LightBuffer = RTHandles.Alloc(tex, "_LightBuffer");
			}

			public void CleanUp()
			{
				RTHandles.Release(m_LightBuffer);
			}

			public void Setup(VolumetricFogRenderFeature settings)
			{
				base.renderPassEvent = settings.renderPassEvent;
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				RenderTextureDescriptor desc = cameraTextureDescriptor;
				VolumetricFogManager managerIfExists = VolumetricFogManager.GetManagerIfExists();
				if (managerIfExists != null)
				{
					if (managerIfExists.downscaling > 1f)
					{
						int height = (desc.width = GetScaledSize(cameraTextureDescriptor.width, managerIfExists.downscaling));
						desc.height = height;
					}
					desc.colorFormat = (managerIfExists.blurHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
					cmd.SetGlobalVector(ShaderParams.LightBufferSize, new Vector4(desc.width, desc.height, (managerIfExists.downscaling > 1f) ? 1f : 0f, 0f));
				}
				desc.depthBufferBits = 0;
				desc.useMipMap = false;
				desc.msaaSamples = 1;
				cmd.GetTemporaryRT(ShaderParams.LightBuffer, desc, FilterMode.Bilinear);
				ConfigureTarget(m_LightBuffer);
				ConfigureClear(ClearFlag.Color, new Color(0f, 0f, 0f, 0f));
				ConfigureInput(ScriptableRenderPassInput.Depth);
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				VolumetricFogManager managerIfExists = VolumetricFogManager.GetManagerIfExists();
				CommandBuffer commandBuffer = CommandBufferPool.Get("Volumetric Fog Light Buffer Rendering");
				commandBuffer.SetGlobalInt(ShaderParams.ForcedInvisible, 0);
				context.ExecuteCommandBuffer(commandBuffer);
				if (managerIfExists == null || (managerIfExists.downscaling <= 1f && managerIfExists.blurPasses < 1))
				{
					CommandBufferPool.Release(commandBuffer);
					return;
				}
				foreach (VolumetricFog volumetricFog in VolumetricFog.volumetricFogs)
				{
					if (volumetricFog != null)
					{
						volumetricFog.meshRenderer.renderingLayerMask = 262144u;
					}
				}
				commandBuffer.Clear();
				SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
				DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
				FilteringSettings filteringSettings = this.filteringSettings;
				filteringSettings.layerMask = 1 << managerIfExists.fogLayer;
				filteringSettings.renderingLayerMask = 262144u;
				context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
				CommandBufferPool.Release(commandBuffer);
			}

			public override void FrameCleanup(CommandBuffer cmd)
			{
			}
		}

		private class BlurRenderPass : ScriptableRenderPass
		{
			private enum Pass
			{
				BlurHorizontal = 0,
				BlurVertical = 1,
				BlurVerticalAndBlend = 2,
				Blend = 3,
				DownscaleDepth = 4,
				BlurVerticalFinal = 5
			}

			private ScriptableRenderer renderer;

			private Material mat;

			private RenderTextureDescriptor rtSourceDesc;

			private static Mesh _fullScreenMesh;

			private Mesh fullscreenMesh
			{
				get
				{
					if (_fullScreenMesh != null)
					{
						return _fullScreenMesh;
					}
					float y = 1f;
					float y2 = 0f;
					_fullScreenMesh = new Mesh();
					_fullScreenMesh.SetVertices(new List<Vector3>
					{
						new Vector3(-1f, -1f, 0f),
						new Vector3(-1f, 1f, 0f),
						new Vector3(1f, -1f, 0f),
						new Vector3(1f, 1f, 0f)
					});
					_fullScreenMesh.SetUVs(0, new List<Vector2>
					{
						new Vector2(0f, y2),
						new Vector2(0f, y),
						new Vector2(1f, y2),
						new Vector2(1f, y)
					});
					_fullScreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, calculateBounds: false);
					_fullScreenMesh.UploadMeshData(markNoLongerReadable: true);
					return _fullScreenMesh;
				}
			}

			public void Setup(Shader shader, ScriptableRenderer renderer, VolumetricFogRenderFeature settings)
			{
				base.renderPassEvent = settings.renderPassEvent;
				this.renderer = renderer;
				if (mat == null)
				{
					mat = CoreUtils.CreateEngineMaterial(shader);
					Texture2D value = Resources.Load<Texture2D>("Textures/blueNoiseVF128");
					mat.SetTexture(ShaderParams.BlueNoiseTexture, value);
				}
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				rtSourceDesc = cameraTextureDescriptor;
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				VolumetricFogManager managerIfExists = VolumetricFogManager.GetManagerIfExists();
				if (managerIfExists == null || (managerIfExists.downscaling <= 1f && managerIfExists.blurPasses < 1))
				{
					Cleanup();
				}
				else
				{
					if ((renderingData.cameraData.camera.cullingMask & (1 << managerIfExists.fogLayer)) == 0)
					{
						return;
					}
					mat.SetVector(ShaderParams.MiscData, new Vector4(managerIfExists.ditherStrength * 0.1f, 0f, managerIfExists.blurEdgeDepthThreshold, 0f));
					if (managerIfExists.ditherStrength > 0f)
					{
						mat.EnableKeyword("DITHER");
					}
					else
					{
						mat.DisableKeyword("DITHER");
					}
					mat.DisableKeyword("EDGE_PRESERVE");
					mat.DisableKeyword("EDGE_PRESERVE_UPSCALING");
					if (managerIfExists.blurPasses > 0 && managerIfExists.blurEdgePreserve)
					{
						mat.EnableKeyword((managerIfExists.downscaling > 1f) ? "EDGE_PRESERVE_UPSCALING" : "EDGE_PRESERVE");
					}
					RenderTargetIdentifier cameraColorTarget = renderer.cameraColorTarget;
					CommandBuffer commandBuffer = CommandBufferPool.Get("Volumetric Fog Render Feature");
					commandBuffer.SetGlobalInt(ShaderParams.ForcedInvisible, 1);
					RenderTextureDescriptor renderTextureDescriptor = rtSourceDesc;
					renderTextureDescriptor.width = GetScaledSize(rtSourceDesc.width, managerIfExists.downscaling);
					renderTextureDescriptor.height = GetScaledSize(rtSourceDesc.height, managerIfExists.downscaling);
					renderTextureDescriptor.useMipMap = false;
					renderTextureDescriptor.colorFormat = (managerIfExists.blurHDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
					renderTextureDescriptor.msaaSamples = 1;
					renderTextureDescriptor.depthBufferBits = 0;
					bool flag = managerIfExists.downscaling > 1f;
					if (flag)
					{
						RenderTextureDescriptor desc = renderTextureDescriptor;
						desc.colorFormat = RenderTextureFormat.RFloat;
						commandBuffer.GetTemporaryRT(ShaderParams.DownsampledDepth, desc, FilterMode.Bilinear);
						FullScreenBlit(commandBuffer, cameraColorTarget, ShaderParams.DownsampledDepth, mat, 4);
					}
					if (managerIfExists.blurPasses < 1)
					{
						commandBuffer.SetGlobalFloat(ShaderParams.BlurScale, managerIfExists.blurSpread);
						FullScreenBlit(commandBuffer, ShaderParams.LightBuffer, cameraColorTarget, mat, 3);
					}
					else
					{
						renderTextureDescriptor.width = GetScaledSize(rtSourceDesc.width, managerIfExists.blurDownscaling);
						renderTextureDescriptor.height = GetScaledSize(rtSourceDesc.height, managerIfExists.blurDownscaling);
						commandBuffer.GetTemporaryRT(ShaderParams.BlurRT, renderTextureDescriptor, FilterMode.Bilinear);
						commandBuffer.GetTemporaryRT(ShaderParams.BlurRT2, renderTextureDescriptor, FilterMode.Bilinear);
						commandBuffer.SetGlobalFloat(ShaderParams.BlurScale, managerIfExists.blurSpread * managerIfExists.blurDownscaling);
						FullScreenBlit(commandBuffer, ShaderParams.LightBuffer, ShaderParams.BlurRT, mat, 0);
						commandBuffer.SetGlobalFloat(ShaderParams.BlurScale, managerIfExists.blurSpread);
						for (int i = 0; i < managerIfExists.blurPasses - 1; i++)
						{
							FullScreenBlit(commandBuffer, ShaderParams.BlurRT, ShaderParams.BlurRT2, mat, 1);
							FullScreenBlit(commandBuffer, ShaderParams.BlurRT2, ShaderParams.BlurRT, mat, 0);
						}
						if (flag)
						{
							FullScreenBlit(commandBuffer, ShaderParams.BlurRT, ShaderParams.BlurRT2, mat, 5);
							FullScreenBlit(commandBuffer, ShaderParams.BlurRT2, cameraColorTarget, mat, 3);
						}
						else
						{
							FullScreenBlit(commandBuffer, ShaderParams.BlurRT, cameraColorTarget, mat, 2);
						}
						commandBuffer.ReleaseTemporaryRT(ShaderParams.BlurRT2);
						commandBuffer.ReleaseTemporaryRT(ShaderParams.BlurRT);
					}
					commandBuffer.ReleaseTemporaryRT(ShaderParams.LightBuffer);
					if (flag)
					{
						commandBuffer.ReleaseTemporaryRT(ShaderParams.DownsampledDepth);
					}
					context.ExecuteCommandBuffer(commandBuffer);
					CommandBufferPool.Release(commandBuffer);
				}
			}

			private void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex)
			{
				destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
				cmd.SetRenderTarget(destination);
				cmd.SetGlobalTexture(ShaderParams.MainTex, source);
				cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
			}

			public override void FrameCleanup(CommandBuffer cmd)
			{
			}

			public void Cleanup()
			{
				CoreUtils.Destroy(mat);
				Shader.SetGlobalInt(ShaderParams.ForcedInvisible, 0);
			}
		}

		[SerializeField]
		[HideInInspector]
		private Shader shader;

		private VolumetricFogRenderPass fogRenderPass;

		private BlurRenderPass blurRenderPass;

		public static bool installed;

		public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

		[Tooltip("Specify which cameras can execute this render feature. If you have several cameras in your scene, make sure only the correct cameras use this feature in order to optimize performance.")]
		public LayerMask cameraLayerMask = -1;

		[Tooltip("Ignores reflection probes from executing this render feature")]
		public bool ignoreReflectionProbes = true;

		private static int GetScaledSize(int size, float factor)
		{
			size = (int)((float)size / factor);
			size /= 2;
			if (size < 1)
			{
				size = 1;
			}
			return size * 2;
		}

		private void OnDisable()
		{
			installed = false;
			if (blurRenderPass != null)
			{
				blurRenderPass.Cleanup();
			}
		}

		private void OnDestroy()
		{
			if (fogRenderPass != null)
			{
				fogRenderPass.CleanUp();
			}
		}

		public override void Create()
		{
			base.name = "Volumetric Fog 2";
			fogRenderPass = new VolumetricFogRenderPass();
			blurRenderPass = new BlurRenderPass();
			shader = Shader.Find("Hidden/VolumetricFog2/Blur");
			if (shader == null)
			{
				Debug.LogWarning("Could not load Volumetric Fog blur shader.");
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;
			if (((int)cameraLayerMask & (1 << camera.gameObject.layer)) != 0 && (!ignoreReflectionProbes || camera.cameraType != CameraType.Reflection) && (!(camera.targetTexture != null) || camera.targetTexture.format != RenderTextureFormat.Depth))
			{
				fogRenderPass.Setup(this);
				blurRenderPass.Setup(shader, renderer, this);
				renderer.EnqueuePass(fogRenderPass);
				renderer.EnqueuePass(blurRenderPass);
				installed = true;
			}
		}
	}
}
