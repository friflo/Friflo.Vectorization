// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using JetBrains.Annotations;


// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


[AttributeUsage(AttributeTargets.Method)]
public sealed class ShaderAttribute : Attribute
{
    public ShaderAttribute([PathReference] string wgsl, string vert = null, string frag = null) { }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class VertexShaderAttribute : Attribute
{
    public VertexShaderAttribute([PathReference] string wgsl, string vert = null) { }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class FragmentShaderAttribute : Attribute
{
    public FragmentShaderAttribute([PathReference] string wgsl, string frag = null) { }
}

// --- Generator Draw Call Rules ---
// 1. [BindIndex]  present              -> pass.DrawIndexed(indices.Length, [BindInstance] ?? 1, 0, 0, 0);
// 2. [BindVertex] only                 -> pass.Draw(vertices.Length, [BindInstance] ?? 1, 0, 0);
// 3. No geometry (Fullscreen/Compute)  -> pass.Draw(3, 1, 0, 0);

/// <summary>
/// Uses programmable vertex pulling via storage buffers instead of the fixed vertex input pipeline.<br/>
/// Vertices are fetched directly from an indexed buffer range, driven by the Draw() offset.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindVertexAttribute : Attribute
{
    public BindVertexAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindUniformAttribute : Attribute
{
    public BindUniformAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindTextureAttribute : Attribute
{
    public BindTextureAttribute (int groupIndex, int bindingIndex) { }
}

/// <summary> Annotates a shader method parameter passing a GPU sampler. </summary>
/// <remarks>
/// Valid parameter types and their corresponding WGSL type:<br/>
/// - <see cref="FilteringSampler"/>   -> WGSL: <c>sampler</c><br/>
/// - <see cref="NonFilteringSampler"/>-> WGSL: <c>sampler</c><br/>
/// - <see cref="ComparisonSampler"/>  -> WGSL: <c>sampler_comparison</c><br/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindSamplerAttribute : Attribute
{
    public BindSamplerAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindStorageAttribute : Attribute
{
    public BindStorageAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindIndexAttribute : Attribute { }



