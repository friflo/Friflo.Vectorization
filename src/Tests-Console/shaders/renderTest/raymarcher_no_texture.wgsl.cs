using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.RenderTest;


[Source("~/shaders/renderTest/raymarcher_no_texture.wgsl")]
public struct ShadertoyUniforms {
    public Vector3 iResolution;
    public float _pad;
    public float iTime;
    public Vector3 _pad2;
}

