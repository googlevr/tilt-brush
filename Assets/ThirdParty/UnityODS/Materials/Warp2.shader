
Shader "Hidden/Warp2"
{
      Properties {
        _MainTex ("Base (RGB)", 2D) = "" {}
    }

    SubShader {
        ZTest Always Cull Off ZWrite Off Fog { Mode Off }
        //Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert_img_rect
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"
           
            float4 _Rect;
               
            v2f_img vert_img_rect( appdata_img v )
            {
                v2f_img o;
                
                // Position quad in rect
                v.vertex.x *= _Rect.z; //width 
                v.vertex.y *= _Rect.w; //height
                v.vertex.x += _Rect.x; //x position
                v.vertex.y += _Rect.y; //y position
                
                o.pos = UnityObjectToClipPos (v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            uniform sampler2D _MainTex;
            // float4(1 / width, 1 / height, width, height)
            uniform float4 _MainTex_TexelSize;

            fixed4 frag(v2f_img i) : COLOR {
                const float fov = 3.1415927 / 2;      // 90 degrees
                const float half_fov = fov/2;  // 45 degrees
                const float tan_half_fov = 1;  // tan(45) == 1
                
                float theta = i.uv.y * 2 - 1;  // from 0..1 to -1..1
                theta = theta * half_fov;      // from -1..1 to -45..45
                
                float y = tan( theta ) / tan_half_fov;
                y = (y + 1)/2.0;                // back to 0..1 uv space
                
                i.uv.y = y;
                float dx = _MainTex_TexelSize.x;
                float dy = _MainTex_TexelSize.y;
                fixed4 renderTex = fixed4(0,0,0,0);
                for (int j = 0; j < 8; j++) {
                    for (int k = -4; k < 4; k++) {
                        renderTex += tex2D(_MainTex, float2(j * dx + dx * .5,
                                                            i.uv.y + k*dy + dy * .5));
                    }
                }

                renderTex /= 8.0 * 8.0;
                return renderTex;
            }
           
            ENDCG
        }  
    }
}