Shader "Hidden/VolumetricLights/Blur" {
	Properties {
		_BlueNoise ("BlueNoise Tex", 2D) = "black" {}
		[HideInInspector] _BlendSrc ("Blend Src", Float) = 1
		[HideInInspector] _BlendDest ("Blend Dest", Float) = 1
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		struct Input
		{
			float2 uv_MainTex;
		};

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			o.Albedo = 1;
		}
		ENDCG
	}
}