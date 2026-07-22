using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.ShadowMapping;


[Source("~/shaders/shadowMapping/fragment.wgsl")]
public struct Scene {
    public Matrix4x4 lightViewProjMatrix;
    public Matrix4x4 cameraViewProjMatrix;
    public Vector3 lightPos;
}

