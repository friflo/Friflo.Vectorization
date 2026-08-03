struct VertexData {
    position:   vec4<f32>,
    color:      vec4<f32>,
}

@group(0) @binding(0) var<storage, read_write>  vertices:   array<VertexData>;
@group(1) @binding(0) var<storage, read>        time:       f32;

@compute @workgroup_size(64)
fn cs_main(@builtin(global_invocation_id) global_id: vec3<u32>) {
    let index = global_id.x;
    
    // Safety check: Nicht über das Array hinaus schreiben
    if (index >= arrayLength(&vertices)) {
        return;
    }

    // Beispiel-Manipulation: Schwingung der Y-Position basierend auf X & Zeit
    let base_x = vertices[index].position.x;
    vertices[index].position.y += sin(time * 3.0 + base_x * 4.0) * 0.005;

    // Optional: Ändere sanft die Alpha- oder Farbwerte
    vertices[index].color.r = 0.5 + 0.5 * sin(time + base_x);
}