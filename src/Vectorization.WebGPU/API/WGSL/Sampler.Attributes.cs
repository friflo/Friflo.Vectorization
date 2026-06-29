// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedTypeParameter
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


/// <summary> Annotates a shader method parameter passing a <see cref="GpuSampler"/>. </summary>
/// <remarks>
/// The attribute names match exactly their corresponding WGSL sampler type.<br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#sampler-type">
/// WGSL: Sampler Types
/// </a></b><br/>
/// - <see cref="sampler"/><br/>
/// - <see cref="sampler_comparison"/><br/>
/// </remarks>
public class SamplerAttribute : Attribute;



#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class sampler : SamplerAttribute
{
    public sampler (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class sampler_comparison : SamplerAttribute
{
    public sampler_comparison (int groupIndex, int bindingIndex) { }
}

#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
