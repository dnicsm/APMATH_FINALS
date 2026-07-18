Shader "Custom/GPUInstancedVerticalGradient"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Top Color", Color) = (0, 0, 1, 1)      // Default Top (Blue)
        _ColorBottom ("Bottom Color", Color) = (0, 1, 1, 1) // Default Bottom (Cyan)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing 

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 personalColor : COLOR; // Pass calculated gradient color to fragment stage
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            // Declare both colors inside our Instancing Constant Buffer
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)       // Top Color
                UNITY_DEFINE_INSTANCED_PROP(float4, _ColorBottom) // Bottom Color
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o); 
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // --- THE MAGIC: Map local vertex height (-0.5 to 0.5) to a t-value (0 to 1) ---
                float tHeight = v.vertex.y + 0.5; // Maps -0.5 to 0, and 0.5 to 1

                // Retrieve the colors for this specific instance
                float4 topColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 bottomColor = UNITY_ACCESS_INSTANCED_PROP(Props, _ColorBottom);

                // Linearly interpolate vertically inside the cube!
                o.personalColor = lerp(bottomColor, topColor, saturate(tHeight));

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); 
                return i.personalColor;
            }
            ENDCG
        }
    }
}