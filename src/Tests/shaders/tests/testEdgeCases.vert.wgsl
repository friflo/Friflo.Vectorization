
struct EmptyStruct {
}

struct TestStruct {
    vector1: vec3<f32>,
    vector2: vec3<f32>,
}

@group(0) @binding(0)   var<uniform> uniforms : EmptyStruct;
@group(0) @binding(1)   var<uniform> uniforms : TestStruct;


