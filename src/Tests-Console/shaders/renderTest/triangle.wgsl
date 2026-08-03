// --- buffer structs
struct VertexData {
    position:   vec4<f32>,
    color:      vec4<f32>
}

struct TriangleStorage {
    triangles: array<VertexData>,
}

// --- uniform structs
struct MyUniforms {
    modelViewProjectionMatrix : mat4x4f,
    tint_color :                vec4<f32>,
    model_offset :              vec4<f32>,
}


// --- bindings
@group(0) @binding(0) var<storage, read>    mesh_data:    TriangleStorage;
@group(2) @binding(0) var<uniform>          myUniforms:   MyUniforms;
@group(2) @binding(1) var<uniform>          model_offset: vec2<f32>;

// ---  pipeline stage input/output
struct VertexOutput {
    @builtin(position)  clip_position:  vec4<f32>,
    @location(0)        color:          vec4<f32>,
}

@vertex
fn vs_main(@builtin(vertex_index) vertex_id: u32) -> VertexOutput {
    var out: VertexOutput;
    
    let vertex = mesh_data.triangles[vertex_id];
    
    // pass transformed position and color and add model_offset
    out.clip_position = myUniforms.modelViewProjectionMatrix * (vertex.position + vec4<f32>(model_offset, 0.0, 0.0));    
    out.color 		  = vertex.color * myUniforms.tint_color;
    
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return in.color;  // output the interpolated vertex color
}