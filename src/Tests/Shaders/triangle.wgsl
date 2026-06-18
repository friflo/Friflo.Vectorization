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
    tint_color: vec4<f32>,
}


// --- bindings
@group(0) @binding(0) var<storage, read>    mesh_data:  TriangleStorage;
@group(1) @binding(0) var<uniform>          myUniforms: MyUniforms;

// ---  pipeline stage input/output
struct VertexOutput {
    @builtin(position)  clip_position:  vec4<f32>,
    @location(0)        color:          vec4<f32>,
}

@vertex
fn vs_main(@builtin(vertex_index) vertex_id: u32) -> VertexOutput {
    var out: VertexOutput;
    
    let vertex = mesh_data.triangles[vertex_id];
    
    // Pass transformed position and color data down the pipeline
    out.clip_position   = vertex.position;
    out.color           = vertex.color * myUniforms.tint_color;
    
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    return in.color;  // output the interpolated vertex color
}