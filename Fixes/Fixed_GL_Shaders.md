# Fixed GL Shaders for Unity 6

These shaders were failing because they had implicit HLSLSupport dependency.
The fix is to ensure they only include UnityCG.cginc.

All GL shaders should look like this pattern:

```shader
Shader "GL/LineDepthCheckeredColor"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            // ... rest of shader
            ENDCG
        }
    }
}
```

If your shader has:
```
#include "HLSLSupport.cginc"
```
Remove it. Unity 6 auto-includes necessary macros.

If it has webgpu pragma like:
```
#pragma exclude_renderers: webgpu
```
Remove webgpu or comment out the line.

The Python script fix_all.py does this automatically.
