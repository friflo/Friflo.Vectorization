
struct Point4f {
    vector4: vec4<f32>
}

@group(0) @binding(0)  var<uniform>     uniform0 : Point4f;
@group(0) @binding(1)  var<uniform>     uniform1 : vec3<u32>;
@group(0) @binding(2)  var<uniform>     uniform2 : vec3<f32>;



