// ======= copy from:  https://www.shadertoy.com/view/MdcSRj
struct ShadertoyUniforms {
    			iResolution : vec3<f32>,
    @size(4) 	_pad 		: f32,
    
    			iTime       : f32,
    @size(12) 	_pad2 		: vec3<f32>,
}

@group(0) @binding(0) var<uniform> inputs : ShadertoyUniforms;

const MAX_STEPS  : i32 = 200;
const NUM_SPHERES: i32 = 12;

fn glsl_mod(x: vec2<f32>, y: f32) -> vec2<f32> {
    return x - vec2<f32>(y) * floor(x / vec2<f32>(y));
}

fn sphere(pos: vec3<f32>, radius: f32, smpl: vec3<f32>) -> f32 {
    return length(pos - smpl) - radius;
}

fn plane(dir: vec3<f32>, offset: f32, smpl: vec3<f32>) -> f32 {
    return dot(dir, smpl) + offset;
}

fn dfDist(smpl_in: vec3<f32>) -> f32 {
    var smpl = smpl_in;
    let T1 : f32 = 10.0;
    let T2 : f32 = 2.0 * T1;
    
    var result : f32 = 10000.0;
    let iTime = inputs.iTime;
    
    smpl.y = smpl.y + sin(smpl.z * 0.2 + iTime) * sin(iTime * 1.33)
                    + sin(smpl.x * 0.3 + iTime) * sin(iTime * 3.22)
                    + sin(smpl.x * 0.5 + smpl.z * 0.22 + iTime) * sin(iTime * 2.22 + smpl.z * 0.1);
                    
    let o = floor((smpl.z + T1) / T2);
    smpl.x = smpl.x + o * 7.0;
    
    let mod_xz = glsl_mod(vec2<f32>(smpl.x, smpl.z), T2) - vec2<f32>(T1);
    smpl.x = mod_xz.x;
    smpl.z = mod_xz.y;
    
    for (var i : i32 = 0; i < NUM_SPHERES; i = i + 1) {
        let t = f32(i) / f32(NUM_SPHERES);
        let n = t + iTime * 0.25 + o * 0.5;
        let pos_sphere = vec3<f32>(sin(n * 5.0) * 5.0, cos(n * 3.0) * 9.0, cos(n * 2.0) * 3.0 + 5.0);
        
        // --- TEXTUR ERSETZT DURCH REINE MATHE ---
        // Statt iChannel0 zu samplen, animieren wir den Radius einfach per sin()
        let radius = (sin(t * 10.0 + iTime) * 0.5 + 0.5) * 2.0 + 1.4;
        
        result = min(result, sphere(pos_sphere, radius, smpl));
    }
    
    result = min(result, plane(vec3<f32>(0.0, -1.0, 0.0), 10.0, smpl));    
    result = min(result, plane(vec3<f32>(0.0, 1.0, 0.0), 10.0, smpl));    
    
    return result;
}

fn dfNormal(smpl: vec3<f32>) -> vec3<f32> {
    let E : f32 = 0.04;
    let d0 = dfDist(smpl);
    let dX = dfDist(smpl + vec3<f32>(E, 0.0, 0.0));
    let dY = dfDist(smpl + vec3<f32>(0.0, E, 0.0));
    let dZ = dfDist(smpl + vec3<f32>(0.0, 0.0, E));
    return normalize(vec3<f32>(dX - d0, dY - d0, dZ - d0));
}

fn dfOcclusion(smpl: vec3<f32>, normal: vec3<f32>) -> f32 {
    let N : f32 = 1.0;
    return clamp(dfDist(smpl + normal * N) / N, 0.0, 1.0);
}

struct TraceResult {
    pos    : vec3<f32>,
    normal : vec3<f32>,
    steps  : f32,
}

fn trace(pos_in: vec3<f32>, dir: vec3<f32>) -> TraceResult {
    var current_pos = pos_in;
    var steps : i32 = 0;
    
    for (var i : i32 = 0; i < MAX_STEPS; i = i + 1) {
        steps = steps + 1;
        let d = dfDist(current_pos);
        current_pos = current_pos + d * dir * 1.0;
        if (d < 0.001) { break; }
    }
    
    var result: TraceResult;
    result.pos = current_pos;
    result.normal = dfNormal(current_pos);
    result.steps = f32(steps) / f32(MAX_STEPS);
    return result;
}

struct FragmentInput {
    @builtin(position) fragCoord : vec4<f32>,
}

@fragment
fn fs_main(input: FragmentInput) -> @location(0) vec4<f32> {
    let iResolution = inputs.iResolution;
    let iTime       = inputs.iTime;
    let fragCoord   = input.fragCoord.xy;

    let opos = vec3<f32>(4.5, sin(iTime * 0.4) * 3.0 + 2.0, -7.0 + iTime * 3.0);
    let dir = normalize(vec3<f32>((fragCoord.x - iResolution.x * 0.5) / iResolution.y, fragCoord.y / iResolution.y - 0.5, 1.0));
    
    let traceRes = trace(opos, dir);
    let hit_pos  = traceRes.pos;
    let normal   = traceRes.normal;
    let steps    = traceRes.steps;
    
    let occ = dfOcclusion(hit_pos, normal);
    let fogAmt = 1.0 - exp(-distance(opos, hit_pos) * 0.01);
    let fogCol = vec3<f32>(0.2, 0.14, 0.18);
    
    let diffuse = vec3<f32>(0.4, 0.5, 0.6) * dot(normal, normalize(vec3<f32>(1.0, 0.3, -1.0)));
    let ambient = vec3<f32>(0.4, 0.2, 0.1);
    
    var color = (ambient + diffuse) * vec3<f32>(1.0 - steps) + pow(1.0 - occ, 1.5) * vec3<f32>(1.0, 0.9, 0.8) * 0.8;
    color = mix(color, fogCol, fogAmt);
    color = (1.0 - exp(-color * 1.5)) * 1.3;
    
    return vec4<f32>(color, 1.0);
}