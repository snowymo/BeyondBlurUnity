Shader "Hidden/FoveationLUT"
{
    Properties
    {
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            int _useLUT;

            float4x4 invProjMat;
            float4 fixationVec;
            float alpha;
            int mode;
            float imPixelSize;
            float planeDist;

            float frag(v2f i) : SV_Target
            {

                //Find eccentricity relative to fixation point.
                float4 pos = float4(i.uv * 2.0 - 1.0, 1.0, 1.0);
                float4 vec = mul(invProjMat,pos);
                float3 vecNorm = vec.xyz / length(vec.xyz);
                float eccentricity = acos(dot(vecNorm, fixationVec.xyz));

                //Find eccentricity relative to center of screen.
                float3 straightAhead = float3(0.0, 0.0, -1.0);
                float centerEccentricity = acos(dot(vecNorm, straightAhead));

                //Find angular pooling size, based on mode selected
                float poolingSizeRad;
                if (mode == 0) // Linear
                {
                    poolingSizeRad = alpha * eccentricity;
                }
                else  // Quadratic
                { 
                    poolingSizeRad = alpha * eccentricity * eccentricity;
                }

                //Convert angular pooling size to pooling size in clip space
                float maxAngle = centerEccentricity + (0.5 * poolingSizeRad);
                float minAngle = centerEccentricity - (0.5 * poolingSizeRad);
                const float PI_OVER_2 = 3.14159274 * 0.5;
                float poolingSizeUV;
                if (maxAngle > PI_OVER_2 || minAngle < -PI_OVER_2)
                    poolingSizeUV = 1.0;
                else
                    poolingSizeUV = abs(tan(maxAngle) - tan(minAngle)) / planeDist;

                //The conversion finds the longest axis of the ellipse-like shape
                // that makes up the true angular pooling region. We need to find
                // the side length of a square with approx the same area

                //Approximate pooling region as a circle. Need to find side length of
                // square of same area
                const float ROOT_PI_OVER_2 = 0.8862269254527579;
                poolingSizeUV = poolingSizeUV * ROOT_PI_OVER_2;


                float poolingSizePixels = poolingSizeUV * imPixelSize *0.5;

                float lod = log2(poolingSizePixels);
                lod = max(lod, 0.0);

                return lod;
            }
            ENDCG
        }
    }
}
