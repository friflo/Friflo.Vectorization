// ----------------------------------------------------------------------------
// Uniforms & Bindings
// ----------------------------------------------------------------------------

struct ImUniforms {
    // Orthographic projection matrix (converts pixel coordinates to NDC)
    projection: mat4x4<f32>,
};

@group(0) @binding(0) var<uniform>  u_globals: ImUniforms;
@group(0) @binding(1) var           u_texture: texture_2d<f32>;
@group(0) @binding(2) var           u_sampler: sampler;

// ----------------------------------------------------------------------------
// Vertex Shader Stage
// ----------------------------------------------------------------------------

struct VertexInput {
    @location(0) position:  vec2<f32>,
    @location(1) uv:        vec2<f32>,
    // Color passed as uint (RGBA8) from C# using Unorm8x4 format in Pipeline Layout
    @location(2) color:     vec4<f32>,
};

struct VertexOutput {
    @builtin(position)  position:   vec4<f32>,
    @location(0)        uv:         vec2<f32>,
    @location(1)        color:      vec4<f32>,
};

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;
    
    // Transform 2D pixel position by orthogonal projection matrix
    out.position = u_globals.projection * vec4<f32>(in.position, 0.0, 1.0);
    out.uv = in.uv;
    out.color = in.color;
    
    return out;
}

// ----------------------------------------------------------------------------
// Fragment Shader Stage
// ----------------------------------------------------------------------------

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Multiply texture color with vertex tint color
    return textureSample(u_texture, u_sampler, in.uv) * in.color;
}
