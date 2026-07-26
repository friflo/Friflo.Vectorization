
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


// example for align / size of nested struct
struct Inner {
    a : vec3<f32>, // Align: 16, Size: 12 (bytes 0..11)
    b : f32,       // Align: 4,  Size: 4  (bytes 12..15)
}                  // Inner layout: Size = 16, Align = 16

struct Outer {
    s1 : Inner,    // Offset 0:  bytes 0..15
    x  : f32,      // Offset 16: bytes 16..19
                   // Next available byte = 20

    s2 : Inner,    // Offset 32: padded from 20 to 32 (Inner align = 16)
}                  // Outer layout: Size = 48, Align = 16

struct FixeSizeArrayStruct1 {
    vectors:    array<vec3f, 16>,
}

struct FixeSizeArrayStruct2 {
    value1:     vec3f,
    vectors:    array<vec3f, 16>,
    value2:     vec3f,
}


struct CustomVector {
    vector2i:     vec2i,
    vectors2i:    array<vec2i, 8>,
    vector2u:     vec2u,
}

@group(0) @binding(0)   var<uniform> uniforms1 : EmptyStruct;
@group(0) @binding(1)   var<uniform> uniforms2 : TestStruct;
@group(0) @binding(2)   var<uniform> uniforms3 : StructWithStructs;
@group(0) @binding(3)   var<uniform> uniforms4 : Outer;
@group(0) @binding(4)   var<uniform> uniforms5 : FixeSizeArrayStruct1;
@group(0) @binding(5)   var<uniform> uniforms6 : FixeSizeArrayStruct2;
@group(0) @binding(6)   var<uniform> uniforms6 : CustomVector;


