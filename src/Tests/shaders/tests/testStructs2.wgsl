
// Ensure fixed size array also adds the namespace
struct FixeSizeArray {
    vectors:    array<vec2i, 16>,
}

@group(0) @binding(0)   var<uniform> uniforms1 : FixeSizeArray;

// NOTE: Do not add other bindings or types. They may add also add required namespace 



