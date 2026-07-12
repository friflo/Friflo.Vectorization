@group(1) @binding(0) var sampler0: sampler;
@group(1) @binding(1) var sampler1: sampler_comparison;

// Sampled Texture Types
@group(0) @binding(0) var texture0: texture_1d<f32>;
@group(0) @binding(1) var texture1: texture_2d<f32>;
@group(0) @binding(2) var texture2: texture_2d_array<i32>;
@group(0) @binding(3) var texture3: texture_3d<i32>;
@group(0) @binding(4) var texture4: texture_cube<u32>;
@group(0) @binding(5) var texture5: texture_cube_array<u32>;

// Multisampled Texture Types
@group(0) @binding(6) var texture6: texture_multisampled_2d<i32>;
@group(0) @binding(7) var texture7: texture_depth_multisampled_2d;

// Storage Texture Types - TODO


// Depth Texture Types
@group(0) @binding(12) var texture12: texture_depth_2d;
@group(0) @binding(13) var texture13: texture_depth_2d_array;
@group(0) @binding(14) var texture14: texture_depth_cube;
@group(0) @binding(15) var texture15: texture_depth_cube_array;


@fragment
fn main(
  @location(0) fragUV: vec2f,
  @location(1) fragPosition: vec4f
) -> @location(0) vec4f {
  return textureSample(texture0, sampler0, fragUV) * fragPosition;
}
