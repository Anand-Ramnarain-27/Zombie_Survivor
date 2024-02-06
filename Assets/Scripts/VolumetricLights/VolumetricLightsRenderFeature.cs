using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VolumetricLights
{
	public class VolumetricLightsRenderFeature : ScriptableRendererFeature
	{
		private static class ShaderParams
		{
			public static int LightBuffer = Shader.PropertyToID("_LightBuffer");

			public static int MainTex = Shader.PropertyToID("_MainTex");

			public static int BlurRT = Shader.PropertyToID("_BlurTex");

			public static int BlurRT2 = Shader.PropertyToID("_BlurTex2");

			public static int BlendDest = Shader.PropertyToID("_BlendDest");

			public static int BlendSrc = Shader.PropertyToID("_BlendSrc");

			public static int MiscData = Shader.PropertyToID("_MiscData");

			public static int ForcedInvisible = Shader.PropertyToID("_ForcedInvisible");

			public static int DownsampledDepth = Shader.PropertyToID("_DownsampledDepth");

			public static int BlueNoiseTexture = Shader.PropertyToID("_BlueNoise");

			public const string SKW_DITHER = "DITHER";
		}

		private class VolumetricLightsRenderPass : ScriptableRenderPass
		{
			private FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent);

			private readonly List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();

			private const int renderingLayer = 262144;

			private const string m_ProfilerTag = "Volumetric Lights Buffer Rendering";

			private VolumetricLightsRenderFeature settings;

			public void Setup(VolumetricLightsRenderFeature settings)
			{
				this.settings = settings;
				base.renderPassEvent = settings.renderPassEvent;
				shaderTagIdList.Clear();
				shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				RenderTextureDescriptor desc = cameraTextureDescriptor;
				int height = (desc.width = GetScaledSize(cameraTextureDescriptor.width, settings.downscaling));
				desc.height = height;
				desc.depthBufferBits = 0;
				desc.useMipMap = false;
				desc.msaaSamples = 1;
				cmd.GetTemporaryRT(ShaderParams.LightBuffer, desc, FilterMode.Bilinear);
				RenderTargetIdentifier renderTargetIdentifier = new RenderTargetIdentifier(ShaderParams.LightBuffer, 0, CubemapFace.Unknown, -1);
				ConfigureTarget(renderTargetIdentifier);
				ConfigureClear(ClearFlag.Color, new Color(0f, 0f, 0f, 0f));
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				CommandBuffer commandBuffer = CommandBufferPool.Get("Volumetric Lights Buffer Rendering");
				commandBuffer.SetGlobalFloat(ShaderParams.ForcedInvisible, 0f);
				context.ExecuteCommandBuffer(commandBuffer);
				if (settings.downscaling <= 1f && settings.blurPasses < 1)
				{
					CommandBufferPool.Release(commandBuffer);
					return;
				}
				foreach (VolumetricLight volumetricLight in VolumetricLight.volumetricLights)
				{
					if (volumetricLight != null)
					{
						volumetricLight.meshRenderer.renderingLayerMask = 262144u;
					}
				}
				commandBuffer.Clear();
				SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
				DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
				FilteringSettings filteringSettings = this.filteringSettings;
				filteringSettings.renderingLayerMask = 262144u;
				context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
				commandBuffer.SetGlobalTexture(value: new RenderTargetIdentifier(ShaderParams.LightBuffer, 0, CubemapFace.Unknown, -1), nameID: ShaderParams.LightBuffer);
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

			private static Matrix4x4 matrix4x4identity = Matrix4x4.identity;

			private VolumetricLightsRenderFeature settings;

			public void Setup(Shader shader, ScriptableRenderer renderer, VolumetricLightsRenderFeature settings)
			{
				this.settings = settings;
				base.renderPassEvent = settings.renderPassEvent;
				this.renderer = renderer;
				if (mat == null)
				{
					mat = CoreUtils.CreateEngineMaterial(shader);
					Texture2D value = Resources.Load<Texture2D>("Textures/blueNoiseVL128");
					mat.SetTexture(ShaderParams.BlueNoiseTexture, value);
				}
				switch (settings.blendMode)
				{
				case BlendMode.Additive:
					mat.SetInt(ShaderParams.BlendSrc, 1);
					mat.SetInt(ShaderParams.BlendDest, 1);
					break;
				case BlendMode.Blend:
					mat.SetInt(ShaderParams.BlendSrc, 1);
					mat.SetInt(ShaderParams.BlendDest, 10);
					break;
				case BlendMode.PreMultiply:
					mat.SetInt(ShaderParams.BlendSrc, 5);
					mat.SetInt(ShaderParams.BlendDest, 1);
					break;
				}
				mat.SetVector(ShaderParams.MiscData, new Vector4(settings.ditherStrength * 0.1f, settings.brightness, settings.blurSpread, 0f));
				if (settings.ditherStrength > 0f)
				{
					mat.EnableKeyword("DITHER");
				}
				else
				{
					mat.DisableKeyword("DITHER");
				}
			}

			public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
			{
				rtSourceDesc = cameraTextureDescriptor;
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				if (settings.downscaling <= 1f && settings.blurPasses < 1)
				{
					Cleanup();
					return;
				}
				RenderTargetIdentifier cameraColorTarget = renderer.cameraColorTarget;
				CommandBuffer commandBuffer = CommandBufferPool.Get("Volumetric Lights Render Feature");
				commandBuffer.SetGlobalFloat(ShaderParams.ForcedInvisible, 1f);
				RenderTextureDescriptor renderTextureDescriptor = rtSourceDesc;
				int height = (renderTextureDescriptor.width = GetScaledSize(rtSourceDesc.width, settings.downscaling));
				renderTextureDescriptor.height = height;
				renderTextureDescriptor.useMipMap = false;
				renderTextureDescriptor.msaaSamples = 1;
				renderTextureDescriptor.depthBufferBits = 0;
				bool flag = settings.downscaling > 1f;
				if (flag)
				{
					RenderTextureDescriptor desc = renderTextureDescriptor;
					desc.colorFormat = RenderTextureFormat.RHalf;
					commandBuffer.GetTemporaryRT(ShaderParams.DownsampledDepth, desc, FilterMode.Bilinear);
					FullScreenBlit(commandBuffer, cameraColorTarget, ShaderParams.DownsampledDepth, mat, 4);
				}
				if (settings.blurPasses < 1)
				{
					FullScreenBlit(commandBuffer, ShaderParams.LightBuffer, cameraColorTarget, mat, 3);
				}
				else
				{
					renderTextureDescriptor.width = GetScaledSize(rtSourceDesc.width, settings.blurDownscaling);
					renderTextureDescriptor.height = GetScaledSize(rtSourceDesc.height, settings.blurDownscaling);
					commandBuffer.GetTemporaryRT(ShaderParams.BlurRT, renderTextureDescriptor, FilterMode.Bilinear);
					commandBuffer.GetTemporaryRT(ShaderParams.BlurRT2, renderTextureDescriptor, FilterMode.Bilinear);
					FullScreenBlit(commandBuffer, ShaderParams.LightBuffer, ShaderParams.BlurRT, mat, 0);
					for (int i = 0; i < settings.blurPasses - 1; i++)
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

			private void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex)
			{
				destination = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
				cmd.SetRenderTarget(destination);
				cmd.SetGlobalTexture(ShaderParams.MainTex, source);
				cmd.DrawMesh(RenderingUtils.fullscreenMesh, matrix4x4identity, material, 0, passIndex);
			}

			public override void FrameCleanup(CommandBuffer cmd)
			{
			}

			public void Cleanup()
			{
				CoreUtils.Destroy(mat);
				Shader.SetGlobalFloat(ShaderParams.ForcedInvisible, 0f);
			}
		}

		[SerializeField]
		[HideInInspector]
		private Shader shader;

		private VolumetricLightsRenderPass vlRenderPass;

		private BlurRenderPass blurRenderPass;

		public static bool installed;

		public BlendMode blendMode;

		public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

		[Range(1f, 4f)]
		public float downscaling = 1f;

		[Range(0f, 4f)]
		public int blurPasses = 1;

		[Range(1f, 4f)]
		public float blurDownscaling = 1f;

		[Range(1f, 4f)]
		public float blurSpread = 1f;

		public float brightness = 1f;

		public float ditherStrength;

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

		private void OnValidate()
		{
			brightness = Mathf.Max(0f, brightness);
			ditherStrength = Mathf.Max(0f, ditherStrength);
		}

		public override void Create()
		{
			base.name = "Volumetric Lights";
			vlRenderPass = new VolumetricLightsRenderPass();
			blurRenderPass = new BlurRenderPass();
			shader = Shader.Find("Hidden/VolumetricLights/Blur");
			_ = shader == null;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;
			if (!(camera.targetTexture != null) || camera.targetTexture.format != RenderTextureFormat.Depth)
			{
				vlRenderPass.Setup(this);
				blurRenderPass.Setup(shader, renderer, this);
				renderer.EnqueuePass(vlRenderPass);
				renderer.EnqueuePass(blurRenderPass);
				installed = true;
			}
		}
	}
}
