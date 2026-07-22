using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.ShadowMapping;


[Source("~/shaders/shadowMapping/fragment.wgsl")]
public struct Scene (
    Matrix4x4 lightViewProjMatrix,
    Matrix4x4 cameraViewProjMatrix,
    Vector3 lightPos)
{
    public Matrix4x4 lightViewProjMatrix = lightViewProjMatrix;
    public Matrix4x4 cameraViewProjMatrix = cameraViewProjMatrix;
    public Vector3 lightPos = lightPos;
}

