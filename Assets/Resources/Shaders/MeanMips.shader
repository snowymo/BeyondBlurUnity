Shader "Hidden/MeanMips"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		CGINCLUDE
#include "UnityCG.cginc"
#include "Utils.cginc"

		Texture2D _MainTex;
	SamplerState my_trilinear_clamp_sampler;
	Texture2D _FoveationLUT;
	SamplerState sampler_FoveationLUT;

	int _useLUT;
	float4 _MainTex_TexelSize;
	float _MeanDepth = 3;
	float _FoveaSize = 0.1;
	float _FoveaX = 0.5;
	float _FoveaY = 0.5;

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
		if (_MainTex_TexelSize.y < 0)
			output.uv.y = 1. - input.uv.y;
#endif

		return output;
	}

	float4 fragment(in Varyings input) : SV_Target
	{
		
		float lod = 0;
		if (_useLUT == 0)
			lod = calculateFoveationLOD(input.uv, _MeanDepth, _FoveaSize, float2(_FoveaX, _FoveaY));
		else
			lod = _FoveationLUT.SampleLevel(sampler_FoveationLUT, input.uv, 0).r;

		return _MainTex.SampleLevel(my_trilinear_clamp_sampler, input.uv, lod);
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
