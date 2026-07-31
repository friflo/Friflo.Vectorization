
// ------ edge case test struct - used as uniform
struct EdgeCaseUniform {
                header:     u32,    
                scalars:    array<f32, 3>,    
    @size(24)   padded_vec: vec2f,
                nested:     SubItem,
    @align(32)  tail_flag:  u32,
}

struct SubItem {
    flag:       u32,
    direction:  vec3f,
}


// ------ edge case test struct - used via storage buffer
struct EdgeCaseStorage {
                        packed_header:  u32,
                        packed_floats:  array<f32, 3>,
                        position:       vec3f,
                        struct_array:   array<SmallData, 2>,
    @align(16) @size(12)end_marker:     u32,
   }

struct SmallData {
    id_and_pad: u32,
    value:      f32,
}

@group(0) @binding(0)   var<uniform>        edgeCaseUniform : EdgeCaseUniform;  // intentional error
@group(0) @binding(1)   var<storage, read>  edgeCaseStorage : EdgeCaseStorage;