
struct EmptyStruct {
}

struct StructWithStructs {
    child1: ChildStruct,
    child2: ChildStruct,
}

struct ChildStruct {
    vector1: vec3<f32>,
    vector2: vec3<f32>,
}

struct TestStruct {
    vector1: vec3<f32>,
    vector2: vec3<f32>,
}

@group(0) @binding(0)   var<uniform> uniforms1 : EmptyStruct;
@group(0) @binding(2)   var<uniform> uniforms2 : TestStruct;
@group(0) @binding(1)   var<uniform> uniforms3 : StructWithStructs;


