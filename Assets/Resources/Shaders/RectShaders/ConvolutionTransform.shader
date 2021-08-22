Shader "Hidden/ConvolutionTransform"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

		CGINCLUDE
#include "UnityCG.cginc"
#include "..\Utils.cginc"

		Texture2D _MainTex;
	SamplerState sampler_MainTex;

	Texture2D _FoveationLUT;
	SamplerState sampler_FoveationLUT;

	float4 _MainTex_TexelSize;

	float2 _Direction;


	float _MeanDepth = 3;
	float _FoveaSize = 0.1;
	float _FoveaX = 0.5;
	float _FoveaY = 0.5;

#define MAX_KERNEL_SIZE 1000

	float _Kernel[MAX_KERNEL_SIZE];
	int _KernelWidth;
	float2 _TexelSize;
	float _K = 1;
	float _LOD;

	int _CurrentLevel;
	int _transform = 0;
	float _screenWidth;
	float _screenHeight;
	float _texSize;
	int _useLUT;
	int _L0Approach; 

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
		
	if (outOfBounds(input.uv,_screenWidth,_screenHeight,_texSize) == 1)
		return float4(0, 0, 0, 1);
	_FoveaX = _FoveaX * (_screenWidth / _texSize);
	_FoveaY = _FoveaY * (_screenHeight / _texSize);
	_FoveaSize = _FoveaSize * min((_screenWidth / _texSize), (_screenHeight / _texSize));

	float lod = 0;
	if (_useLUT == 0)
		lod = calculateFoveationRect(input.uv, _MeanDepth, _FoveaSize, float2(_screenWidth, _screenHeight), float2(_FoveaX, _FoveaY));
	else
		lod = _FoveationLUT.SampleLevel(sampler_FoveationLUT, input.uv, 0).r;

	//if (_L0Approach == 1 && lod +2 < _CurrentLevel)
	//{
	//	return float4(0, 0, 0, 0);
	//}

	float3 value = float3(0,0,0);
	float spread = (_KernelWidth - 1) / 2.0;
	int k = 0;
	float centerAlpha = 0;
	for (float i = -spread; i <= spread; i++)
	{
		for (float j = -spread; j <= spread; j++)
		{
			float4 sampled = _MainTex.SampleLevel(sampler_MainTex, input.uv + float2(i, j) * _TexelSize, _LOD);
			if (i == 0 && j == 0)
				centerAlpha = sampled.a;
			value = value + sampled.xyz * _Kernel[k] * _K;
			k = k + 1;
		}
	}

	return float4(value,centerAlpha);

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
