Shader "VolumetricLights/DustParticles" {
	Properties {
		_BaseMap ("Base Map", 2D) = "white" {}
		_BaseColor ("Base Color", Vector) = (1,1,1,1)
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_EmissionColor ("Color", Vector) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "white" {}
		_SoftParticlesNearFadeDistance ("Soft Particles Near Fade", Float) = 0
		_SoftParticlesFarFadeDistance ("Soft Particles Far Fade", Float) = 1
		_CameraNearFadeDistance ("Camera Near Fade", Float) = 1
		_CameraFarFadeDistance ("Camera Far Fade", Float) = 2
		_DistortionBlend ("Distortion Blend", Float) = 0.5
		_DistortionStrength ("Distortion Strength", Float) = 1
		[HideInInspector] _Surface ("__surface", Float) = 0
		[HideInInspector] _Blend ("__mode", Float) = 0
		[HideInInspector] _AlphaClip ("__clip", Float) = 0
		[HideInInspector] _BlendOp ("__blendop", Float) = 0
		[HideInInspector] _SrcBlend ("__src", Float) = 1
		[HideInInspector] _DstBlend ("__dst", Float) = 0
		[HideInInspector] _ZWrite ("__zw", Float) = 1
		[HideInInspector] _Cull ("__cull", Float) = 2
		[HideInInspector] _ColorMode ("_ColorMode", Float) = 0
		[HideInInspector] _BaseColorAddSubDiff ("_ColorMode", Vector) = (0,0,0,0)
		[ToggleOff] _FlipbookBlending ("__flipbookblending", Float) = 0
		[HideInInspector] _SoftParticlesEnabled ("__softparticlesenabled", Float) = 0
		[HideInInspector] _CameraFadingEnabled ("__camerafadingenabled", Float) = 0
		[HideInInspector] _SoftParticleFadeParams ("__softparticlefadeparams", Vector) = (0,0,0,0)
		[HideInInspector] _CameraFadeParams ("__camerafadeparams", Vector) = (0,0,0,0)
		[HideInInspector] _DistortionEnabled ("__distortionenabled", Float) = 0
		[HideInInspector] _DistortionStrengthScaled ("Distortion Strength Scaled", Float) = 0.1
		[HideInInspector] _ParticleDistanceAtten ("Distance Atten", Float) = 10
		[HideInInspector] _QueueOffset ("Queue offset", Float) = 0
		[HideInInspector] _FlipbookMode ("flipbook", Float) = 0
		[HideInInspector] _Mode ("mode", Float) = 0
		[HideInInspector] _Color ("color", Vector) = (1,1,1,1)
		[HideInInspector] _BoundsCenter ("Bounds Center", Vector) = (0,0,0,1)
		[HideInInspector] _BoundsExtents ("Bounds Size", Vector) = (0,0,0,1)
		[HideInInspector] _ConeTipData ("Cone Tip Data", Vector) = (0,0,0,0.1)
		[HideInInspector] _ExtraGeoData ("Extra Geo Data", Vector) = (1,0,0,1)
		[HideInInspector] _Border ("Border", Float) = 0.1
		[HideInInspector] _DistanceFallOff ("Length Falloff", Float) = 0
		[HideInInspector] _FallOff ("FallOff Physical", Vector) = (1,2,1,1)
		[HideInInspector] _ConeAxis ("Cone Axis", Vector) = (0,0,0,0.5)
		[HideInInspector] _AreaExtents ("Area Extents", Vector) = (0,0,0,1)
		[HideInInspector] _ParticleLightColor ("Particle Light Color", Vector) = (1,1,1,1)
		[HideInInspector] _ShadowIntensity ("Shadow Intensity", Vector) = (0,1,0,0)
		[HideInInspector] _Cookie2D ("Cookie (2D)", 2D) = "black" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = _Color.rgb;
			o.Alpha = _Color.a;
		}
		ENDCG
	}
}