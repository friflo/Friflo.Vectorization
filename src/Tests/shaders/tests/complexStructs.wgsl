

// ------ edge case test for uniform struct

// Uniform Buffer Testing Struct (std140 rules)
struct EdgeCaseUniform {
    header: u32,           // offset: 0, size: 4, align: 4
    // 12B padding (array std140 align 16)
    
    // std140 array rule: scalar element stride forced to 16B
    scalars: array<f32, 3>, // offset: 16, size: 48 (3 * 16B stride), align: 16
    
    // @size override: forces size to 24B (natural size: 8B)
    @size(24) 
    padded_vec: vec2f,     // offset: 64, size: 24, align: 8
    // 8B padding (nested struct align 16)
    
    // nested struct in std140: align bumped to 16B
    nested: SubItem,       // offset: 96, size: 32, align: 16
    
    // @align override: forces field alignment to 32B
    @align(32) 
    tail_flag: u32,        // offset: 128, size: 4, align: 32
    // 28B tail padding (rounds struct size up to multiple of max align 32)
}                          // total size: 160, max align: 32

struct SubItem {
    flag: u32,             // offset: 0, size: 4, align: 4
    // 12B padding (vec3f align 16)
    direction: vec3f,      // offset: 16, size: 12, align: 16
}                          // total size: 32, max align: 16



// ------ edge case test for storage struct

// Storage Buffer Testing Struct (std430 / packed rules)
struct EdgeCaseStorage {
    packed_header: u32,    // offset: 0, size: 4, align: 4 (holds 8-bit byte in lowest byte)
    
    // std430 array rule: natural packed stride (4B)
    packed_floats: array<f32, 3>, // offset: 4, size: 12 (3 * 4B stride), align: 4
    
    // vec3f alignment trap: size 12B, but requires 16B alignment
    position: vec3f,       // offset: 16, size: 12, align: 16
    
    // array of structs: packed tightly at next 4B boundary
    struct_array: array<SmallData, 2>, // offset: 28, size: 16 (2 * 8B), align: 4
    // 4B padding (@align 16 override)
    
    // combined @align and @size override
    @align(16) @size(12)
    end_marker: u32,       // offset: 48, size: 12, align: 16
    // 4B tail padding (rounds struct size up to multiple of max align 16)
}                          // total size: 64, max align: 16

struct SmallData {
    id_and_pad: u32,       // offset: 0, size: 4, align: 4 (holds 16-bit id in lower bits)
    value: f32,            // offset: 4, size: 4, align: 4
}                          // total size: 8, max align: 4


@group(0) @binding(0)   var<uniform>        edgeCaseUniform : EdgeCaseUniform;
@group(0) @binding(1)   var<storage, read>  edgeCaseStorage : EdgeCaseStorage;