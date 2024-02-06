Shader "Hidden/Kronnect/RadiantGI_URP" {
	Properties {
		[NoScaleOffset] _NoiseTex ("Noise Tex", any) = "" {}
		_StencilValue ("Stencil Value", Float) = 0
		_StencilCompareFunction ("Stencil Compare Function", Float) = 8
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