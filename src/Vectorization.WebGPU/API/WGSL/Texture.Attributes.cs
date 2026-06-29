// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedTypeParameter
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


/// <summary> Annotates a shader method parameter passing a <see cref="GpuTextureView"/>. </summary>
/// <remarks>
/// The attribute names match exactly their corresponding WGSL texture type.<br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#sampled-texture-type">
/// WGSL: Sampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_1d{TSampleType}"/><br/>
/// - <see cref="texture_2d{TSampleType}"/><br/>
/// - <see cref="texture_2d_array{TSampleType}"/><br/>
/// - <see cref="texture_3d{TSampleType}"/><br/>
/// - <see cref="texture_cubeAttribute{TSampleType}"/><br/>
/// - <see cref="texture_cube_array{TSampleType}"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#multisampled-texture-type">
/// WGSL: Multisampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_multisampled_2d{TSampleType}"/><br/>
/// - <see cref="texture_depth_multisampled_2d"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-storage">
/// WGSL: Storage Texture Types
/// </a></b><br/>
/// - <see cref="texture_storage_1d{TSampleType}"/><br/>
/// - <see cref="texture_storage_2d{TSampleType}"/><br/>
/// - <see cref="texture_storage_2d_array{TSampleType}"/><br/>
/// - <see cref="texture_storage_3d{TSampleType}"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-depth">
/// WGSL: Depth Texture Types</a></b><br/>
/// - <see cref="texture_depth_2d"/><br/>
/// - <see cref="texture_depth_2d_array"/><br/>
/// - <see cref="texture_depth_cube"/><br/>
/// - <see cref="texture_depth_cube_array"/><br/>
/// </remarks>
public class TextureAttribute : Attribute;




#region --- Sampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#sampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_1d<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_1d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_2d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d_array<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_2d_array (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_3d<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_3d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cubeAttribute<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_cubeAttribute (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube_array<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_cube_array (int groupIndex, int bindingIndex) { }
}
#endregion




#region --- Multisampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_multisampled_2d<TSampleType> : TextureAttribute  where TSampleType : unmanaged, ISampleType
{
    public texture_multisampled_2d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_multisampled_2d : TextureAttribute
{
    public texture_depth_multisampled_2d (int groupIndex, int bindingIndex) { }
}
#endregion




#region --- Storage Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-storage

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_1d<TSampleType> : TextureAttribute  where TSampleType : unmanaged
{
    public texture_storage_1d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d<TSampleType> : TextureAttribute  where TSampleType : unmanaged
{
    public texture_storage_2d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d_array<TSampleType> : TextureAttribute  where TSampleType : unmanaged
{
    public texture_storage_2d_array (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_3d<TSampleType> : TextureAttribute  where TSampleType : unmanaged
{
    public texture_storage_3d (int groupIndex, int bindingIndex) { }
}
#endregion




#region --- Depth Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-depth

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d : TextureAttribute
{
    public texture_depth_2d (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d_array : TextureAttribute
{
    public texture_depth_2d_array (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube : TextureAttribute
{
    public texture_depth_cube (int groupIndex, int bindingIndex) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube_array : TextureAttribute
{
    public texture_depth_cube_array (int groupIndex, int bindingIndex) { }
}

#endregion


public interface ISampleType;

public struct i32 : ISampleType;
public struct u32 : ISampleType; 
public struct f32 : ISampleType;