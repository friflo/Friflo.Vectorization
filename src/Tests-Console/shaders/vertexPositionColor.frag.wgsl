@fragment
fn main(
  @location(0) fragUV: vec2f,
  @location(1) fragPosition: vec4f
) -> @location(0) vec4f {
  return fragPosition;
  // return vec4f(1.0, 0.0, 0.0, 1.0); // check hot-reload
}
