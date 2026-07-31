struct RedefinedUniform {
  // other RedefinedUniform struct uses: array<mat4x4f>
  mvps : array<mat4x4f, 16>,
}

@binding(0) @group(0) var<storage, read> uniforms : RedefinedUniform;

