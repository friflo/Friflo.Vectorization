
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


struct VectorInUniform {
    uniform_vector2i:   vec2i,
    uniform_vectors2i:  array<vec2i, 8>,
    uniform_vector2u:   vec2u,
}

// uses same fixed array - array<vec2i, 8> - but now in a storage buffer
struct VectorInStorage {
    storage_vector2i:   vec2i,
    storage_vectors2i:  array<vec2i, 8>,
    storage_vector2u:   vec2u,
}


struct Particle {
                a     : u32,  // 4 Bytes (Offset 0..3)
    @align(16)  flags : u32,  // 4 Bytes (normales align: 4, with @align(16) force: 16)
                speed : f32,  // 4 Bytes
    @size(32)   id    : u32,  // 4 Bytes size, but with @size(32) increased to 32 bytes
                count : u32,  // 4 Bytes
}

struct DirectUniform {
    someValue: f32
}

struct DirectStorage {
    someValue: f32
}


@group(0) @binding(1)   var<uniform>        uniform1 : TestStruct;
@group(0) @binding(2)   var<uniform>        uniform2 : StructWithStructs;
@group(0) @binding(3)   var<uniform>        uniform3 : Outer;
@group(0) @binding(4)   var<uniform>        uniform4 : FixeSizeArrayStruct1;
@group(0) @binding(5)   var<uniform>        uniform5 : FixeSizeArrayStruct2;
@group(0) @binding(6)   var<uniform>        uniform6 : VectorInUniform;
@group(0) @binding(7)   var<storage, read>  storage7 : VectorInStorage;
@group(0) @binding(8)   var<uniform>        uniform8 : Particle;
@group(0) @binding(9)   var<uniform>        uniform9 : array<vec4<f32>>;
@group(0) @binding(10)  var<uniform>        uniform10: array<DirectUniform>;
@group(0) @binding(11)  var<storage>        storage11: array<DirectStorage>;


