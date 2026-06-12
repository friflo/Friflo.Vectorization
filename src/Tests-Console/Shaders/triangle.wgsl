// --- VERTEX SHADER ---

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec4<f32>,
}

@vertex
fn vs_main(@builtin(vertex_index) in_vertex_index: u32) -> VertexOutput {
    // Hardcoded 2D vertex positions (X, Y)
    var positions = array<vec2<f32>, 3>(
        vec2<f32>( 0.0,  0.5),  // Top center
        vec2<f32>(-0.5, -0.5),  // Bottom left
        vec2<f32>( 0.5, -0.5)   // Bottom right
    );

    // Hardcoded RGB vertex colors
    var colors = array<vec3<f32>, 3>(
        vec3<f32>(1.0, 0.0, 0.0), // Red
        vec3<f32>(0.0, 1.0, 0.0), // Green
        vec3<f32>(0.0, 0.0, 1.0)  // Blue
    );

    var out: VertexOutput;
    // Convert 2D position to 4D clip space (Z = 0.0, W = 1.0)
    out.position = vec4<f32>(positions[in_vertex_index], 0.0, 1.0);
    // Add alpha channel (1.0 = fully opaque)
    out.color = vec4<f32>(colors[in_vertex_index], 1.0);
    
    return out;
}

// --- FRAGMENT SHADER ---

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Return interpolated color to the first render target
    return in.color;
}