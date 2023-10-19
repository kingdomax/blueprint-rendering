Shader "Unlit/DepthPeeler"
{
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex   vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv     : TEXCOORD0;
				float4 normal : NORMAL;
			};

			struct v2f
			{
				float2 uv      : TEXCOORD0;
				float4 vertex  : SV_POSITION;
				float4 proj    : TEXCOORD2;
				half3  wNormal : TEXCOORD1;
			};

			sampler2D _PreviusLayer;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv     = v.uv;
				
				o.proj   = ComputeScreenPos(o.vertex);
				o.proj.z = COMPUTE_DEPTH_01;

				o.wNormal = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col    = tex2D(_PreviusLayer, i.proj.xy/i.proj.w);
				clip(i.proj.z - (col.a + 0.00001)); // clip() Discards the current pixel if the specified value is less than zero.
													// for example: if 1st layer pixel is the same value as 2nd layer pixel, discard it 
													//           => so we will get 0 value in 2nd layer pixel to compare with 3rd and so on
				
				// Store depth in alpha value & Store normal in rgb value
				col.a  = i.proj.z;	   
				col.rgb = normalize(i.wNormal).xyz;
					
				return col;
			}

			ENDCG
		}
	}
}
