Shader "VolumetricFog2/VolumetricFog2DURP" {
	Properties {
		[HideInInspector] _MainTex ("Noise Texture", 2D) = "white" {}
		[HideInInspector] _DetailTex ("Detail Texture", 3D) = "white" {}
		[HideInInspector] _NoiseScale ("Noise Scale", Float) = 0.025
		[HideInInspector] _NoiseFinalMultiplier ("Noise Scale", Float) = 1
		[HideInInspector] _NoiseStrength ("Noise Strength", Float) = 1
		[HideInInspector] _Color ("Color", Vector) = (1,1,1,1)
		[HideInInspector] _Density ("Density", Float) = 1
		[HideInInspector] _DeepObscurance ("Deep Obscurance", Range(0, 2)) = 0.7
		[HideInInspector] _LightColor ("Light Color", Vector) = (1,1,1,1)
		[HideInInspector] _LightDiffusionPower ("Sun Diffusion Power", Range(1, 64)) = 32
		[HideInInspector] _LightDiffusionIntensity ("Sun Diffusion Intensity", Range(0, 1)) = 0.4
		[HideInInspector] _ShadowIntensity ("Sun Shadow Intensity", Range(0, 1)) = 0.5
		[HideInInspector] _WindDirection ("Wind Direction", Vector) = (1,0,0,1)
		[HideInInspector] _RayMarchSettings ("Raymarch Settings", Vector) = (2,0.01,1,0.1)
		[HideInInspector] _SunDir ("Sun Direction", Vector) = (1,0,0,1)
		[HideInInspector] _BoundsCenter ("Bounds Center", Vector) = (0,0,0,1)
		[HideInInspector] _BoundsExtents ("Bounds Size", Vector) = (0,0,0,1)
		[HideInInspector] _BoundsBorder ("Bounds Border", Vector) = (0,1,0,1)
		[HideInInspector] _BoundsData ("Bounds Data", Vector) = (0,0,1,1)
		[HideInInspector] _DetailData ("Detail Data", Vector) = (0.5,4,-0.5,0)
		[HideInInspector] _DetailColor ("Detail Color", Vector) = (0.5,0.5,0.5,0)
		[HideInInspector] _DetailOffset ("Detail Offset", Float) = -0.5
		[HideInInspector] _DistanceData ("Distance Data", Vector) = (0,5,1,1)
		[HideInInspector] _DepthGradientTex ("Depth Gradient Texture", 2D) = "white" {}
		[HideInInspector] _HeightGradientTex ("Height Gradient Texture", 2D) = "white" {}
		[HideInInspector] _SpecularThreshold ("Specular Threshold", Float) = 0.5
		[HideInInspector] _SpecularIntensity ("Specular Intensity", Float) = 0
		[HideInInspector] _SpecularColor ("Specular Color", Vector) = (0.5,0.5,0.5,0)
		[HideInInspector] _FogOfWarCenterAdjusted ("FoW Center Adjusted", Vector) = (0,0,0,1)
		[HideInInspector] _FogOfWarSize ("FoW Size", Vector) = (0,0,0,1)
		[HideInInspector] _FogOfWarCenter ("FoW Center", Vector) = (0,0,0,1)
		[HideInInspector] _FogOfWar ("FoW Texture", 2D) = "white" {}
		[HideInInspector] _BlueNoise ("_Blue Noise Texture", 2D) = "white" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
}