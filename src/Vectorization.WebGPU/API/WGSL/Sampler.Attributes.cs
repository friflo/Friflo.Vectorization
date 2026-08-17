// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedTypeParameter
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


/// <summary> Annotates a shader method parameter passing a <see cref="GpuSampler"/>. </summary>
/// <remarks>
/// See: <b><a href="https://www.w3.org/TR/WGSL/#sampler-type">
/// WGSL: Sampler Types
/// </a></b><br/>
/// <b><c>sampler</c></b><br/>
/// - <see cref="samplerAttribute"/><br/>
/// <b><c>sampler_comparison</c></b><br/>
/// - <see cref="sampler_comparisonAttribute"/><br/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public abstract class SamplerTypeAttribute : Attribute;


/// <summary> WGSL Sampler Type:  <b><c>sampler</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class samplerAttribute : SamplerTypeAttribute
{
    public samplerAttribute(bool filtering = true) { }
}

/// <summary> WGSL Sampler Type:  <b><c>sampler_comparison</c></b><br/> </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class sampler_comparisonAttribute : SamplerTypeAttribute;

