Shader "Simple Color Texture" {
Properties {
 _Color ("Main Color", Color) = (1,1,1,1)
 _MainTex ("Base (RGB)", 2D) = "white" {}
}
SubShader { 
 LOD 100
 Tags { "RenderType"="Opaque" }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 Pass {
  Tags { "RenderType"="Opaque" }
  SetTexture [_MainTex] { ConstantColor [_Color] combine constant * texture }
 }
}
}
