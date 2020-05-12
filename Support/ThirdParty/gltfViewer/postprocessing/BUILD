# Description:
#   Selected modules from threejs/examples/js/postprocessing.
#
# These files are in the examples/ directory, but is licensed under the same
# terms as ThreeJS.

licenses(["notice"])  # MIT

package(default_visibility = ["//third_party/javascript/threejs/r84:allowed_users"])

load("//javascript/closure:builddefs.bzl", "gen_closurized_js")

js_library(
    name = "adaptivetonemappingpass",
    srcs = ["AdaptiveToneMappingPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "bloompass",
    srcs = ["BloomPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "bokehpass",
    srcs = ["BokehPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "clearpass",
    srcs = ["ClearPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "cubetexturepass",
    srcs = ["CubeTexturePass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "dotscreenpass",
    srcs = ["DotScreenPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "effectcomposer",
    srcs = ["EffectComposer.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "filmpass",
    srcs = ["FilmPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "glitchpass",
    srcs = ["GlitchPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "maskpass",
    srcs = ["MaskPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "outlinepass",
    srcs = ["OutlinePass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "renderpass",
    srcs = ["RenderPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "smaapass",
    srcs = ["SMAAPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "ssaarenderpass",
    srcs = ["SSAARenderPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "savepass",
    srcs = ["SavePass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "shaderpass",
    srcs = ["ShaderPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "taarenderpass",
    srcs = ["TAARenderPass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "texturepass",
    srcs = ["TexturePass.js"],
    deps_mgmt = "legacy",
)

js_library(
    name = "unrealbloompass",
    srcs = ["UnrealBloomPass.js"],
    deps_mgmt = "legacy",
)

# Provide Closure-wrapped sources for compatibility with Google Closure
# projects
js_library(
    name = "adaptivetonemappingpass-closurized-lib",
    srcs = [":adaptivetonemappingpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "bloompass-closurized-lib",
    srcs = [":bloompass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "bokehpass-closurized-lib",
    srcs = [":bokehpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "clearpass-closurized-lib",
    srcs = [":clearpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "cubetexturepass-closurized-lib",
    srcs = [":cubetexturepass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "dotscreenpass-closurized-lib",
    srcs = [":dotscreenpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "effectcomposer-closurized-lib",
    srcs = [":effectcomposer-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "filmpass-closurized-lib",
    srcs = [":filmpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "glitchpass-closurized-lib",
    srcs = [":glitchpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "maskpass-closurized-lib",
    srcs = [":maskpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "outlinepass-closurized-lib",
    srcs = [":outlinepass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "renderpass-closurized-lib",
    srcs = [":renderpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "smaapass-closurized-lib",
    srcs = [":smaapass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "ssaarenderpass-closurized-lib",
    srcs = [":ssaarenderpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "savepass-closurized-lib",
    srcs = [":savepass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "shaderpass-closurized-lib",
    srcs = [":shaderpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "taarenderpass-closurized-lib",
    srcs = [":taarenderpass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "texturepass-closurized-lib",
    srcs = [":texturepass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

js_library(
    name = "unrealbloompass-closurized-lib",
    srcs = [":unrealbloompass-all"],
    deps_mgmt = "legacy",
    deps = ["//javascript/closure:base"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:adaptivetonemappingpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "adaptivetonemappingpass-all",
    src = "AdaptiveToneMappingPass.js",
    provides_to_add = ["THREE.AdaptiveToneMappingPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:bloompass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "bloompass-all",
    src = "BloomPass.js",
    provides_to_add = ["THREE.BloomPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:bokehpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "bokehpass-all",
    src = "BokehPass.js",
    provides_to_add = ["THREE.BokehPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:clearpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "clearpass-all",
    src = "ClearPass.js",
    provides_to_add = ["THREE.ClearPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:cubetexturepass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "cubetexturepass-all",
    src = "CubeTexturePass.js",
    provides_to_add = ["THREE.CubeTexturePass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:dotscreenpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "dotscreenpass-all",
    src = "DotScreenPass.js",
    provides_to_add = ["THREE.DotScreenPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:effectcomposer-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "effectcomposer-all",
    src = "EffectComposer.js",
    provides_to_add = ["THREE.EffectComposer"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:filmpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "filmpass-all",
    src = "FilmPass.js",
    provides_to_add = ["THREE.FilmPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:glitchpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "glitchpass-all",
    src = "GlitchPass.js",
    provides_to_add = ["THREE.GlitchPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:maskpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "maskpass-all",
    src = "MaskPass.js",
    provides_to_add = ["THREE.MaskPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:outlinepass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "outlinepass-all",
    src = "OutlinePass.js",
    provides_to_add = ["THREE.OutlinePass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:renderpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "renderpass-all",
    src = "RenderPass.js",
    provides_to_add = ["THREE.RenderPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:smaapass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "smaapass-all",
    src = "SMAAPass.js",
    provides_to_add = ["THREE.SMAAPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:ssaarenderpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "ssaarenderpass-all",
    src = "SSAARenderPass.js",
    provides_to_add = ["THREE.SSAARenderPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:savepass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "savepass-all",
    src = "SavePass.js",
    provides_to_add = ["THREE.SavePass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:shaderpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "shaderpass-all",
    src = "ShaderPass.js",
    provides_to_add = ["THREE.ShaderPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:taarenderpass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "taarenderpass-all",
    src = "TAARenderPass.js",
    provides_to_add = ["THREE.TAARenderPass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:texturepass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "texturepass-all",
    src = "TexturePass.js",
    provides_to_add = ["THREE.TexturePass"],
)

# //third_party/javascript/threejs/r84/examples/js/postprocessing:unrealbloompass-closurized-lib
# to deps instead of this target to srcs.
gen_closurized_js(
    name = "unrealbloompass-all",
    src = "UnrealBloomPass.js",
    provides_to_add = ["THREE.UnrealBloomPass"],
)
