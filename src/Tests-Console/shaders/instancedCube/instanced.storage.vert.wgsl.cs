using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.InstancedCube;


[Source("~/shaders/instancedCube/instanced.storage.vert.wgsl")]
public struct Uniforms (
    Matrix4x4 modelViewProjectionMatrix)
{
    public Matrix4x4 modelViewProjectionMatrix = modelViewProjectionMatrix;
}

