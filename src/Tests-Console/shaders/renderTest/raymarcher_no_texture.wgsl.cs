using System;
using System.Numerics;
using Friflo.Vectorization.WebGPU;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
namespace Shaders.RenderTest;


[Source("~/shaders/renderTest/raymarcher_no_texture.wgsl")]
public struct ShadertoyUniforms (
    Vector3 iResolution,
    float _pad,
    float iTime,
    Vector3 _pad2)
{
    public Vector3 iResolution = iResolution;
    public float _pad = _pad;
    public float iTime = iTime;
    public Vector3 _pad2 = _pad2;
}

