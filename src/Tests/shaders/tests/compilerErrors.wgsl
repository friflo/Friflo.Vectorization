
struct EmptyStruct {
}

struct HasMissingType {
    missingType: MissingType,
}

struct HasInvalidPrimitve {
    invalidVec3: vec3<i16>,
}

struct InconsistentStruct {
    value1:  i32,
}

struct InconsistentStruct {
    value1:  i32,
    value2:  i32,
}

struct DynamicSizedStruct {
	position:  vec3<f32>,
    triangles: array<vec3f>,
}


@group(0) @binding(0)   var<uniform> emptyStruct    : EmptyStruct;
@group(0) @binding(1)   var<uniform> uniforms1      : HasMissingType;
@group(0) @binding(2)   var<uniform> uniforms2      : InconsistentStruct;
@group(0) @binding(3)   var<uniform> uniforms3      : HasInvalidPrimitve;
@group(0) @binding(4)   var<storage> trianglesData  : DynamicSizedStruct;


