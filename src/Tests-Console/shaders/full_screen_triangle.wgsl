// === full screen triangle - for fragment shaders from:  https://www.shadertoy.com/
struct VertexOutput {
    @builtin(position) position : vec4<f32>,
}

@vertex
fn vs_main(@builtin(vertex_index) vertex_index: u32) -> VertexOutput {
    var out: VertexOutput;
    // Berechnet die 3 Ecken des riesigen Bildschirmdreiecks (0, 1, 2)
    let x = f32(i32(vertex_index << 1u) & 2) * 2.0 - 1.0;
    let y = f32(i32(vertex_index & 2u)) * 2.0 - 1.0;
    
    out.position = vec4<f32>(x, y, 0.0, 1.0);
    return out;
}
