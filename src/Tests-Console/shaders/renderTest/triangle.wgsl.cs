using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.RenderTest;


[Source("~/shaders/renderTest/triangle.wgsl")]
public struct VertexData(Vector4 position, Vector4 color)
{
    public Vector4 position = position;
    public Vector4 color = color;
}

[Source("~/shaders/renderTest/triangle.wgsl")]
public struct TriangleStorage {
    public VertexData triangles;
}

[Source("~/shaders/renderTest/triangle.wgsl")]
public struct MyUniforms {
    public Vector4 tint_color;
    public Vector4 model_offset;
}

