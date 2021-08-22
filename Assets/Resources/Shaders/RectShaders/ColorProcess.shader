Shader "Hidden/ColorProcess"
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

	float4 _MainTex_TexelSize;

	int _direction;
	int _isLinearColorSpace;

	float _screenWidth;
	float _screenHeight;
	float _texSize;
	float _LOD;

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
		float sx = _texSize / _screenWidth;
		float sy = _texSize / _screenHeight;

		if (_direction == 1) {
			output.uv = float2(input.uv.x * sx, input.uv.y * sy);
		}
		else if (_direction == -1) {
			output.uv = float2(input.uv.x / sx, input.uv.y / sy);

		}
		else
			output.uv = input.uv;
#if UNITY_UV_STARTS_AT_TOP
		if (_MainTex_TexelSize.y < 0)
			output.uv.y = 1. - input.uv.y;
#endif

		return output;
	}

	float4 fragment(in Varyings input) : SV_Target
	{
		float4 color = _MainTex.SampleLevel(sampler_MainTex, input.uv, _LOD);
		if (_direction == -1)
		{
			if(_isLinearColorSpace ==1)
				return float4(srgb2Linear(YCrCb2rgb(color.xyz)), color.a);
			else
				return float4(YCrCb2rgb(color.xyz), color.a);

		}
		if (_direction == 1)
		{
			if (_isLinearColorSpace == 1)
				return float4(rgb2YCrCb(linear2Srgb(color.xyz)),color.a);
			else
				return float4(rgb2YCrCb(color.xyz), color.a);

		}
		return color;
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
