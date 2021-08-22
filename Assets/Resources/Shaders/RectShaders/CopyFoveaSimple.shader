Shader "Hidden/CopyFoveaSimple"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		CGINCLUDE
#include "UnityCG.cginc"
#include "..\Utils.cginc"

		Texture2D _MainTex;
	SamplerState sampler_MainTex;

	Texture2D _SecondTex;
	SamplerState sampler_SecondTex;

	Texture2D _FoveationLUT;
	SamplerState sampler_FoveationLUT;

	float4 _MainTex_TexelSize;

	float _FoveaSize;
	float _FoveaX;
	float _FoveaY;
	float _MeanDepth;

	float _screenWidth;
	float _screenHeight;
	float _texSize;
	int _useLUT;

	int _Blend;
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
		
		if (input.uv.x > _screenWidth / _texSize + 0.02 || input.uv.y > _screenHeight / _texSize + 0.02)
			return float4(1, 0, 0, 1);
		_FoveaX = _FoveaX * (_screenWidth / _texSize);
		_FoveaY = _FoveaY * (_screenHeight / _texSize);
		_FoveaSize = _FoveaSize * min((_screenWidth / _texSize), (_screenHeight / _texSize));

		float lod = 0;
		if (_useLUT == 0)
			lod = calculateFoveationBlending(input.uv, _FoveaSize, float2(_FoveaX, _FoveaY));
		else{
			float sx = _texSize / _screenWidth;
			float sy = _texSize / _screenHeight;
			float2 lutuv = float2(input.uv.x * sx, input.uv.y * sy);
			lod = _FoveationLUT.SampleLevel(sampler_FoveationLUT, lutuv, 0).r;

			float blendTo =0.25;
			if (lod > 0){
				if(lod <= blendTo)
					lod = 1 - (blendTo - lod) / blendTo;
				else
					lod = 1;
			}
			lod = 1 - lod;
		}


	
		float4 output;

		if (_Blend == 1) 
		{
			float3 a = _MainTex.SampleLevel(sampler_MainTex, input.uv, 0).xyz;
			float3 b = _SecondTex.SampleLevel(sampler_SecondTex, input.uv, 0);
			output = float4((lod * a) + ((1 - lod) * b), 1.0);
		}
		else 
		{
			output = _MainTex.SampleLevel(sampler_MainTex, input.uv, 0);
		}

		return output;


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
