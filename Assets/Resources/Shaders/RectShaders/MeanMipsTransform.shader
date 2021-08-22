Shader "Hidden/MeanMipsTransform"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		CGINCLUDE
#include "UnityCG.cginc"
#include "..\Utils.cginc"

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
	float _screenWidth;
	float _screenHeight;
	float _texSize;
	int _CurrentLevel;
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

		float lod = 0;
		if (_useLUT == 0){
			_FoveaX = _FoveaX * (_screenWidth / _texSize);
			_FoveaY = _FoveaY * (_screenHeight / _texSize);
			_FoveaSize = _FoveaSize * min((_screenWidth / _texSize), (_screenHeight / _texSize));
			lod = calculateFoveationRect(input.uv, _MeanDepth, _FoveaSize, float2(_screenWidth, _screenHeight), float2(_FoveaX, _FoveaY));
		}
		else {
			float sx = _texSize / _screenWidth;
			float sy = _texSize / _screenHeight;
			float2 lutuv = float2(input.uv.x * sx, input.uv.y * sy);
			lod = _FoveationLUT.SampleLevel(sampler_FoveationLUT, lutuv, 0).r;
			
		}

		if (_L0Approach == 1 && ((int)lod) < (_CurrentLevel))
		{
			return float4(0, 0, 0, 1);
		}	
	
		float alpha = 1;
		if (_L0Approach ==1) {
			alpha = (lod) - _CurrentLevel;
			alpha = alpha > 1 ? 1 : alpha;
		}
		lod = max(lod - _CurrentLevel, 0);

	
		return float4(alpha*_MainTex.SampleLevel(my_trilinear_clamp_sampler, input.uv, lod).xyz,1);

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
