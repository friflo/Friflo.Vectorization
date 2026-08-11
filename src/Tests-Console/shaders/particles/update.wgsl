struct Particle {
    position: vec4<f32>, // xyz = Position, w = Remaining Lifetime
    velocity: vec4<f32>, // xyz = Direction/Speed, w = Unused Padding
}

struct FrameUniform {
    time: f32,
    deltaTime: f32,
    padding0: f32,
    padding1: f32,
}

@group(0) @binding(0) var<storage, read_write> particles: array<Particle>;
@group(0) @binding(1) var<uniform> frameData: FrameUniform;

fn rand(co: vec2<f32>) -> f32 {
    return fract(sin(dot(co, vec2<f32>(12.9898, 78.233))) * 43758.5453);
}

@compute @workgroup_size(256)
fn cs_main(@builtin(global_invocation_id) id: vec3<u32>) {
    let index = id.x;
    if (index >= arrayLength(&particles)) {
        return;
    }

    var p = particles[index];

    // Restlaufzeit verringern
    p.position.w -= frameData.deltaTime;

    // Respawn bei Ablauf der Lebenszeit
    if (p.position.w <= 0.0) {
        let seed = vec2<f32>(f32(index), frameData.time);
        
        // Startpunkt unten mittig mit kleiner Streuung
        p.position = vec4<f32>(
            (rand(seed + vec2<f32>(1.0, 0.0)) - 0.5) * 0.05,
            -0.5,
            0.0,
            1.5 + rand(seed + vec2<f32>(2.0, 0.0)) * 1.0 // 1.5s bis 2.5s Leben
        );

        // Breite Ausfächerung nach oben und zu den Seiten
        let speedX = (rand(seed + vec2<f32>(3.0, 0.0)) - 0.5) * 1.6;
        let speedY = rand(seed + vec2<f32>(4.0, 0.0)) * 0.9 + 0.6;
        let speedZ = (rand(seed + vec2<f32>(5.0, 0.0)) - 0.5) * 0.4;

        p.velocity = vec4<f32>(speedX, speedY, speedZ, 0.0);
    } else {
        // Schwerkraft-Einwirkung
        p.velocity.y -= 0.7 * frameData.deltaTime; 
        
        // Position aktualisieren
        p.position = vec4<f32>(
            p.position.xyz + p.velocity.xyz * frameData.deltaTime,
            p.position.w
        );
    }

    particles[index] = p;
}