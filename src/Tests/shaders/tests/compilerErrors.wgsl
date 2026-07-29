
struct EmptyStruct {
}

struct HasMissingType {
    missingType: MissingType,
}

struct HasInvalidPrimitive {
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

struct HasUnmappedType {
	unmappedType:  vec3h,
}

struct HasMissingElementType {
    missingElementType: array<MissingElementType, 4>
}

struct HasUnmappedElementType {
    unmappedElementType: array<vec3h, 4>
}


@group(0) @binding(0)   var<uniform> emptyStruct    : EmptyStruct;
@group(0) @binding(1)   var<uniform> uniform1       : HasMissingType;
@group(0) @binding(2)   var<uniform> uniform2       : InconsistentStruct;
@group(0) @binding(3)   var<uniform> uniform3       : HasInvalidPrimitive;
@group(0) @binding(4)   var<storage> trianglesData  : DynamicSizedStruct;
@group(0) @binding(5)   var<uniform> unmappedType   : HasUnmappedType;
@group(0) @binding(6)   var<uniform> uniform6       : HasMissingElementType;
@group(0) @binding(7)   var<uniform> uniform7       : HasUnmappedElementType;


