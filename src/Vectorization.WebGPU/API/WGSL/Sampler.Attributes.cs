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
/// See: <b><a href="https://www.w3.org/TR/WGSL/#sampler-type">
/// WGSL: Sampler Types
/// </a></b><br/>
/// <b><c>sampler</c></b><br/>
/// - <see cref="SamplerFiltering"/><br/>
/// - <see cref="SamplerNonFiltering"/><br/>
/// <b><c>sampler_comparison</c></b><br/>
/// - <see cref="SamplerComparison"/><br/>
/// </remarks>
public class SamplerTypeAttribute : Attribute;



#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

/// <summary> WGSL Sampler Type:  <b><c>sampler</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerFiltering : SamplerTypeAttribute
{
    public SamplerFiltering (int group, int binding) { }
}

/// <summary> WGSL Sampler Type:  <b><c>sampler</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerNonFiltering : SamplerTypeAttribute
{
    public SamplerNonFiltering (int group, int binding) { }
}

/// <summary> WGSL Sampler Type:  <b><c>sampler_comparison</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerComparison : SamplerTypeAttribute
{
    public SamplerComparison (int group, int binding) { }
}

#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
