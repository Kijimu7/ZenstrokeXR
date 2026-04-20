
Shader "Custom/BristleDeform"
{
    Properties
    {
        _MainTex ("Bristle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Roughness ("Roughness", Range(0,1)) = 0.6
        
        [Header(Deformation)]
        _BendAmount ("Bend Amount", Float) = 0
        _BendDirection ("Bend Direction", Vector) = (0,0,0,0)
        _SplayAmount ("Splay Amount", Float) = 0
        _PressAmount ("Press Amount", Float) = 0
        _BrushTipWorld ("Brush Tip World", Vector) = (0,0,0,0)
        _BristleHeight ("Bristle Height", Float) = 0.48
        
        [Header(Bristle Look)]
        _BristleStiffness ("Stiffness Curve Power", Range(0.5, 4)) = 2.0
        _BristleTipSoftness ("Tip Softness", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Roughness;
                float _BendAmount;
                float4 _BendDirection;
                float _SplayAmount;
                float _PressAmount;
                float4 _BrushTipWorld;
                float _BristleHeight;
                float _BristleStiffness;
                float _BristleTipSoftness;
            CBUFFER_END
            
            // Deform bristle vertices
            float3 DeformBristle(float3 posOS, float3 normalOS)
            {
                // Use local Z as the bristle height factor (0 = base, 1 = tip)
                // Bristles go from base (lower Z) to tip (higher Z)
                float heightFactor = saturate(posOS.z / max(_BristleHeight, 0.01));
                
                // Stiffness curve: base is stiff, tip is flexible
                // Power curve makes base resist bending more
                float bendInfluence = pow(heightFactor, _BristleStiffness);
                
                // === BEND ===
                // Bend the bristle along the bend direction
                float bendRad = radians(_BendAmount) * bendInfluence;
                float3 bendDir = _BendDirection.xyz;
                
                if (length(bendDir) > 0.001)
                {
                    bendDir = normalize(bendDir);
                    // Offset position along bend direction, scaled by height
                    posOS.xyz += bendDir * sin(bendRad) * heightFactor * _BristleHeight;
                    // Shorten slightly when bending (arc compression)
                    posOS.z -= (1.0 - cos(bendRad)) * heightFactor * _BristleHeight * 0.3;
                }
                
                // === SPLAY ===
                // Spread bristles outward from center when pressed
                float2 radialDir = posOS.xy;
                float radialDist = length(radialDir);
                if (radialDist > 0.0001)
                {
                    float2 radialNorm = radialDir / radialDist;
                    // Splay increases toward the tip
                    float splayInfluence = heightFactor * _SplayAmount;
                    posOS.xy += radialNorm * splayInfluence * 0.05;
                }
                
                // === PRESS (compression) ===
                // Shorten bristles when pressed against surface
                float compression = _PressAmount * 0.15 * heightFactor;
                posOS.z -= compression * _BristleHeight;
                
                // === TIP SOFTNESS ===
                // Extra waviness at the tip for organic feel
                float tipWave = sin(posOS.x * 100.0 + posOS.y * 80.0) * _BristleTipSoftness;
                float tipInfluence = smoothstep(0.7, 1.0, heightFactor) * _PressAmount;
                posOS.xy += normalOS.xy * tipWave * tipInfluence * 0.005;
                
                return posOS;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Apply bristle deformation in object space
                float3 deformedPos = DeformBristle(input.positionOS.xyz, input.normalOS);
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(deformedPos);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // Simple lighting
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = texColor.rgb * mainLight.color * (NdotL * 0.7 + 0.3); // Wrap lighting
                
                // Simple specular
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                half spec = pow(saturate(dot(normalWS, halfDir)), lerp(4, 64, 1.0 - _Roughness)) * (1.0 - _Roughness) * 0.3;
                
                half3 finalColor = diffuse + spec * mainLight.color;
                finalColor = MixFog(finalColor, input.fogFactor);
                
                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Roughness;
                float _BendAmount;
                float4 _BendDirection;
                float _SplayAmount;
                float _PressAmount;
                float4 _BrushTipWorld;
                float _BristleHeight;
                float _BristleStiffness;
                float _BristleTipSoftness;
            CBUFFER_END
            
            float3 DeformBristleShadow(float3 posOS)
            {
                float heightFactor = saturate(posOS.z / max(_BristleHeight, 0.01));
                float bendInfluence = pow(heightFactor, _BristleStiffness);
                float bendRad = radians(_BendAmount) * bendInfluence;
                float3 bendDir = _BendDirection.xyz;
                
                if (length(bendDir) > 0.001)
                {
                    bendDir = normalize(bendDir);
                    posOS.xyz += bendDir * sin(bendRad) * heightFactor * _BristleHeight;
                    posOS.z -= (1.0 - cos(bendRad)) * heightFactor * _BristleHeight * 0.3;
                }
                
                float2 radialDir = posOS.xy;
                float radialDist = length(radialDir);
                if (radialDist > 0.0001)
                {
                    float2 radialNorm = radialDir / radialDist;
                    posOS.xy += radialNorm * heightFactor * _SplayAmount * 0.05;
                }
                
                posOS.z -= _PressAmount * 0.15 * heightFactor * _BristleHeight;
                
                return posOS;
            }
            
            Varyings shadowVert(Attributes input)
            {
                Varyings output;
                float3 deformedPos = DeformBristleShadow(input.positionOS.xyz);
                float3 posWS = TransformObjectToWorld(deformedPos);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, float3(0,0,0)));
                return output;
            }
            
            half4 shadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
