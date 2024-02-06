Shader "Shader Graphs/Polygon_Triplanar_LWRP" {
	Properties {
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		[NoScaleOffset] _Overlay ("Overlay", 2D) = "white" {}
		_Tiling ("Tiling", Float) = 0
		_FallOff ("FallOff", Float) = 0
		_DirtAmount ("DirtAmount", Range(0, 1.2)) = 0
		[NoScaleOffset] _Emission ("Emission", 2D) = "white" {}
		_EmissionColor ("EmissionColor", Vector) = (0,0,0,0)
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
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
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}