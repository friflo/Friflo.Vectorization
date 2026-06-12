// ==========================================
// 1. DATA STRUCTURES (AST: Structs)
// ==========================================

// Defines a single vertex payload
struct VertexData {
    position: vec3<f32>,
    color: vec4<f32>,
    uv: vec2<f32>,
}

// Defines the top-level buffer layout holding a runtime-sized array of vertices
struct TriangleStorage {
    triangles: array<VertexData>,
}

// ==========================================
// 2. RESOURCE BINDINGS (AST: Bindings)
// ==========================================

// The storage buffer containing all triangle data.
// 'storage, read' marks it as a read-only structured storage buffer.
@group(0) @binding(0) 
var<storage, read> mesh_data: TriangleStorage;

// ==========================================
// 3. PIPELINE STAGE INPUT/OUTPUT
// ==========================================

struct VertexOutput {
    @builtin(position) clip_position: vec4<f32>,
    @location(0) color: vec4<f32>,
}

// ==========================================
// 4. ENTRY POINTS (AST: EntryPoints)
// ==========================================

@vertex
fn vs_main(@builtin(vertex_index) vertex_id: u32) -> VertexOutput {
    var out: VertexOutput;
    
    // Fetch individual vertex data safely from the storage buffer using the vertex ID
    let vertex = mesh_data.triangles[vertex_id];
    
    // Pass transformed position and color data down the pipeline
    out.clip_position = vec4<f32>(vertex.position, 1.0);
    out.color = vertex.color;
    
    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Simply output the interpolated vertex color
    return in.color;
}