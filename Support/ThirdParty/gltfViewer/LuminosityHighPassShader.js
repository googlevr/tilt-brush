// TODO: This tries to preserve the bloomed colors despite using an LDR
// color buffer.  A better approach would be to simulate HDR color by using the
// alpha channel as an exponent. But that would be a more significant change.

THREE.LuminosityHighPassShader = {
  shaderID: "luminosityHighPass",

  uniforms: {
    "tDiffuse": { type: "t", value: null },
    "luminosityThreshold": { type: "f", value: 1.0 },
    "smoothWidth": { type: "f", value: 1.0 },
    "defaultColor": { type: "c", value: new THREE.Color( 0x000000 ) },
    "defaultOpacity":  { type: "f", value: 0.0 }
  },

  vertexShader: [
    "varying vec2 vUv;",
    "void main() {",
    "  vUv = uv;",
    "  gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 );",
    "}"
  ].join("\n"),
  fragmentShader: [
    "uniform sampler2D tDiffuse;",
    "uniform vec3 defaultColor;",
    "uniform float defaultOpacity;",
    "uniform float luminosityThreshold;",
    "uniform float smoothWidth;",
    "varying vec2 vUv;",

    "void main() {",
    "  vec4 texel = texture2D( tDiffuse, vUv );",
    "  vec4 outputColor = vec4( defaultColor.rgb, defaultOpacity );",
    "  float ar = max(texel.r - luminosityThreshold, 0.0);",
    "  float ag = max(texel.g - luminosityThreshold, 0.0);",
    "  float ab = max(texel.b - luminosityThreshold, 0.0);",
    "  float am = max(max(ar, ag), ab);",
    "  if (am > 0.0) {",
    "    ar /= am;",
    "    ag /= am;",
    "    ab /= am;",
    "  }",
    "  gl_FragColor.r = mix( outputColor.r, texel.r, ar );",
    "  gl_FragColor.g = mix( outputColor.g, texel.g, ag );",
    "  gl_FragColor.b = mix( outputColor.b, texel.b, ab );",
    "}"
  ].join("\n")
};
