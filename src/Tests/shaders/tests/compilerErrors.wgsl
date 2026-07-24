
struct HasMissingType {
    missingType: MissingType,
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


