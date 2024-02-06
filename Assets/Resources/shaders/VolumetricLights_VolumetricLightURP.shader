Shader "VolumetricLights/VolumetricLightURP" {
	Properties {
		[HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
		[HideInInspector] _NoiseTex ("Noise Texture", any) = "white" {}
		[HideInInspector] _NoiseScale ("Noise Scale", Range(0.001, 0.04)) = 0.025
		[HideInInspector] _Color ("Color", Vector) = (1,1,1,1)
		[HideInInspector] _Density ("Density", Float) = 1
		[HideInInspector] _NoiseStrength ("Noise Strength", Float) = 1
		[HideInInspector] _NoiseScale ("Noise Scale", Float) = 1
		[HideInInspector] _NoiseFinalMultiplier ("Noise Final Multiplier", Float) = 1
		[HideInInspector] _RayMarchSettings ("Raymarch Settings", Vector) = (2,0.01,1,0.1)
		[HideInInspector] _WindDirection ("Wind Direction", Vector) = (1,0,0,1)
		[HideInInspector] _BoundsCenter ("Bounds Center", Vector) = (0,0,0,1)
		[HideInInspector] _BoundsExtents ("Bounds Size", Vector) = (0,0,0,1)
		[HideInInspector] _ConeBoundsCenter ("Cone Bounds Center", Vector) = (0,0,0,1)
		[HideInInspector] _ConeBoundsExtents ("Cone Bounds Extents", Vector) = (1,1,1,1)
		[HideInInspector] _ConeTipData ("Cone Tip Data", Vector) = (0,0,0,0.1)
		[HideInInspector] _ExtraGeoData ("Extra Geometry Data", Vector) = (1,0,0,1)
		[HideInInspector] _Border ("Border", Float) = 0.1
		[HideInInspector] _DistanceFallOff ("Length Falloff", Float) = 0
		[HideInInspector] _FallOff ("FallOff Physical", Vector) = (1,2,1,1)
		[HideInInspector] _ConeAxis ("Cone Axis", Vector) = (0,0,0,0.5)
		[HideInInspector] _AreaExtents ("Area Extents", Vector) = (0,0,0,1)
		[HideInInspector] _LightColor ("Light Color", Vector) = (1,1,1,1)
		[HideInInspector] _ToLightDir ("To Light Dir", Vector) = (1,1,1,0)
		[HideInInspector] _BlendSrc ("Blend Src", Float) = 1
		[HideInInspector] _BlendDest ("Blend Dest", Float) = 1
		[HideInInspector] _ShadowIntensity ("Shadow Intensity", Vector) = (0,1,0,0)
		[HideInInspector] _BlueNoise ("Blue Noise", 2D) = "black" {}
		[HideInInspector] _Cookie2D ("Cookie (2D)", 2D) = "black" {}
		[HideInInspector] _ShadowCubemap ("Shadow Texture (Cubemap)", any) = "" {}
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