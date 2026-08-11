struct Particle {
    position: vec4<f32>,
    velocity: vec4<f32>,
}

struct FrameUniform {
    time: f32,
    deltaTime: f32,
    padding0: f32,
    padding1: f32,
}

@group(0) @binding(0) var<storage, read> particles: array<Particle>;
@group(1) @binding(0) var<uniform> frameData: FrameUniform;

struct VertexOutput {
    @builtin(position) clip_position: vec4<f32>,
    @location(0) color: vec4<f32>,
    @location(1) uv: vec2<f32>,
}

@vertex
fn vs_main(
    @builtin(vertex_index) vertexIndex: u32,
    @builtin(instance_index) instanceIndex: u32
) -> VertexOutput {
    let particle = particles[instanceIndex];

    // Billboard Quad
    var offsets = array<vec2<f32>, 6>(
        vec2<f32>(-0.008,  0.008),
        vec2<f32>(-0.008, -0.008),
        vec2<f32>( 0.008, -0.008),
        vec2<f32>(-0.008,  0.008),
        vec2<f32>( 0.008, -0.008),
        vec2<f32>( 0.008,  0.008)
    );

    var uvs = array<vec2<f32>, 6>(
        vec2<f32>(0.0, 1.0),
        vec2<f32>(0.0, 0.0),
        vec2<f32>(1.0, 0.0),
        vec2<f32>(0.0, 1.0),
        vec2<f32>(1.0, 0.0),
        vec2<f32>(1.0, 1.0)
    );

    let offset = offsets[vertexIndex];
    let pos = particle.position.xyz + vec3<f32>(offset, 0.0);

    var out: VertexOutput;
    out.clip_position = vec4<f32>(pos.x, pos.y, pos.z, 1.0);
    out.uv = uvs[vertexIndex];

    // color gradient based on remaining life time (orange -> red -> transparent)
    let lifeProgress = clamp(particle.position.w / 2.5, 0.0, 1.0);
    let red   = 1.0;
    let green = lifeProgress * 0.6;
    let blue  = 0.1;

    out.color = vec4<f32>(red, green, blue, lifeProgress);

    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // smooth glow
    let dist = length(in.uv - vec2<f32>(0.5));
    if (dist > 0.5) {
        discard;
    }

    let alpha = (1.0 - dist * 2.0) * in.color.a;
    return vec4<f32>(in.color.rgb, alpha);
}