
struct DirectUniform2 {
    someValue: f32
}

struct UniformWithArray {
    vectors: array<vec4<f32>, 8>
}

@group(0) @binding(0)  var<uniform>     uniform0 : array<vec4<f32>, 8>;
@group(0) @binding(1)  var<uniform>     uniform1 : array<DirectUniform2, 8>;
@group(0) @binding(2)  var<uniform>     uniform2 : UniformWithArray;



