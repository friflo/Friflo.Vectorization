using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders;


[Source("~/shaders/basic.vert.wgsl")]
public struct Uniforms {
    public Matrix4x4 modelViewProjectionMatrix;
}

