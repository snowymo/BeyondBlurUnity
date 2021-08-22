Shader "Hidden/MatchNoise"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		CGINCLUDE
#include "UnityCG.cginc"
#include "Utils.cginc"



	Texture2D _MeanTex;
	SamplerState sampler_MeanTex;

	Texture2D _StdevTex;
	SamplerState sampler_StdevTex;

	Texture2D _NoiseTex;
	SamplerState sampler_NoiseTex;
	

	float4 _NoiseTex_TexelSize;

	int _LOD;

	struct Input
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};

	struct Varyings
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	Varyings vertex(in Input input)
	{
		Varyings output;
		output.vertex = UnityObjectToClipPos(input.vertex.xyz);
		output.uv = input.uv;

#if UNITY_UV_STARTS_AT_TOP
		if (_NoiseTex_TexelSize.y < 0)
			output.uv.y = 1. - input.uv.y;
#endif

		return output;
	}

	float4 fragment(in Varyings input) : SV_Target
	{
		float3 res = float3(0,0,0);

		float3 noise = _NoiseTex.SampleLevel(sampler_NoiseTex, input.uv, _LOD).xyz;
		float3 mean = _MeanTex.SampleLevel(sampler_MeanTex, input.uv, _LOD).xyz;
		float3 std = _StdevTex.SampleLevel(sampler_StdevTex, input.uv, _LOD).xyz;
		res = res + ((noise)*std) + mean;
		return float4(res,1);
	}
		ENDCG

		SubShader
	{
		Cull Off ZWrite Off ZTest Always

			Pass
		{
			CGPROGRAM
			#pragma vertex vertex
			#pragma fragment fragment
			ENDCG
		}
	}
}
