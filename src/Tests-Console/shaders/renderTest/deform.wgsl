struct VertexData {
    position:   vec4<f32>,
    color:      vec4<f32>,
}

struct TimeUniform {
    time: f32,
}

@group(0) @binding(0) var<storage, read_write> vertices: array<VertexData>;
@group(1) @binding(0) var<uniform>              timeData: TimeUniform;

@compute @workgroup_size(64)
fn cs_main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;    

    if (index >= arrayLength(&vertices)) {
        return;
    }
	// oscillate y position based on x & time
    let t = timeData.time;
    vertices[index].position.y += sin(t * 3.0 + vertices[index].position.x * 4.0) * 0.005;
    
    // Optional: change vertex colors
    // vertices[index].color.r = 0.5 + 0.5 * sin(time + base_x);
}