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
/// - <see cref="SamplerFilteringAttribute"/><br/>
/// - <see cref="SamplerNonFilteringAttribute"/><br/>
/// <b><c>sampler_comparison</c></b><br/>
/// - <see cref="SamplerComparisonAttribute"/><br/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public abstract class SamplerTypeAttribute : Attribute;


/// <summary> WGSL Sampler Type:  <b><c>sampler</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerFilteringAttribute : SamplerTypeAttribute
{
    public SamplerFilteringAttribute (int group, int binding) { }
}

/// <summary> WGSL Sampler Type:  <b><c>sampler</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerNonFilteringAttribute : SamplerTypeAttribute
{
    public SamplerNonFilteringAttribute (int group, int binding) { }
}

/// <summary> WGSL Sampler Type:  <b><c>sampler_comparison</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SamplerComparisonAttribute : SamplerTypeAttribute
{
    public SamplerComparisonAttribute (int group, int binding) { }
}

