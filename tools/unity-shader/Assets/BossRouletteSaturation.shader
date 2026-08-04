Shader "Hidden/Gilomx/BossRouletteSaturation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Saturation ("Saturation", Range(0, 1)) = 1
        _FlipY ("Flip Y", Range(0, 1)) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Saturation;
            float _FlipY;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed2 uv = input.uv;
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipY);
                fixed4 color = tex2D(_MainTex, uv);
                fixed luminance = dot(
                    color.rgb, fixed3(0.299, 0.587, 0.114));
                color.rgb = lerp(
                    fixed3(luminance, luminance, luminance),
                    color.rgb, _Saturation);
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
