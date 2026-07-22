using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.ShadowMapping;


/// Skipped identical duplicate of struct <see cref="Scene"/>
internal partial struct _info;

[Source("~/shaders/shadowMapping/vertex.wgsl")]
public struct Model {
    public Matrix4x4 modelMatrix;
}

