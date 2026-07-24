
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



@group(0) @binding(0)   var<uniform> uniforms3 : HasMissingType;

@group(0) @binding(1)   var<uniform> uniforms3 : InconsistentStruct;

@group(0) @binding(2)   var<uniform> uniforms3 : HasInvalidPrimitve;


