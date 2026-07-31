
struct EmptyStruct {
}

struct HasMissingType {
    missingType: MissingType,
}

struct HasInvalidPrimitive {
    invalidVec3: vec3<i16>,     // intentional error
}

struct InconsistentStruct {
    value1:  i32,
}

struct InconsistentStruct {     // intentional error
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
    missingElementType: array<MissingElementType, 4>   // intentional error
}

struct HasUnmappedElementType {
    unmappedElementType: array<vec3h, 4>
}

struct RedefinedUniform {
  // other RedefinedUniform struct uses: array<mat4x4f, 16>
  mvps : array<mat4x4f>,
}


@group(0) @binding(0)   var<uniform> emptyStruct    : EmptyStruct;
@group(0) @binding(1)   var<uniform> uniform1       : HasMissingType;
@group(0) @binding(2)   var<uniform> uniform2       : InconsistentStruct;
@group(0) @binding(3)   var<uniform> uniform3       : HasInvalidPrimitive;  // intentional error
@group(0) @binding(4)   var<storage> trianglesData  : DynamicSizedStruct;
@group(0) @binding(5)   var<uniform> unmappedType   : HasUnmappedType;
@group(0) @binding(6)   var<uniform> uniform6       : HasMissingElementType;
@group(0) @binding(7)   var<uniform> uniform7       : HasUnmappedElementType;
@group(0) @binding(8)   var<uniform> uniform8       : RedefinedUniform;


