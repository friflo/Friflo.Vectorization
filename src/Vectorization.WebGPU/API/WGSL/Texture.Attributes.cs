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
/// - <see cref="texture_1d{ST}"/><br/>
/// - <see cref="texture_2d{ST}"/><br/>
/// - <see cref="texture_2d_array{ST}"/><br/>
/// - <see cref="texture_3d{ST}"/><br/>
/// - <see cref="texture_cube{ST}"/><br/>
/// - <see cref="texture_cube_array{ST}"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#multisampled-texture-type">
/// WGSL: Multisampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_multisampled_2d{ST}"/><br/>
/// - <see cref="texture_depth_multisampled_2d"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-storage">
/// WGSL: Storage Texture Types
/// </a></b><br/>
/// - <see cref="texture_storage_1d{ST}"/><br/>
/// - <see cref="texture_storage_2d{ST}"/><br/>
/// - <see cref="texture_storage_2d_array{ST}"/><br/>
/// - <see cref="texture_storage_3d{ST}"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-depth">
/// WGSL: Depth Texture Types</a></b><br/>
/// - <see cref="texture_depth_2d"/><br/>
/// - <see cref="texture_depth_2d_array"/><br/>
/// - <see cref="texture_depth_cube"/><br/>
/// - <see cref="texture_depth_cube_array"/><br/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public abstract class TextureTypeAttribute : Attribute;



#region --- Sampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#sampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_1d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_1d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_2d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d_array<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_2d_array (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_3d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_3d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_cube (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube_array<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_cube_array (int group, int binding) { }
}
#endregion




#region --- Multisampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_multisampled_2d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_multisampled_2d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_multisampled_2d : TextureTypeAttribute
{
    public texture_depth_multisampled_2d (int group, int binding) { }
}
#endregion




#region --- Storage Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-storage

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_1d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_storage_1d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_storage_2d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d_array<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_storage_2d_array (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_3d<ST> : TextureTypeAttribute  where ST : unmanaged, ISampleType
{
    public texture_storage_3d (int group, int binding) { }
}
#endregion




#region --- Depth Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-depth

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d : TextureTypeAttribute
{
    public texture_depth_2d (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d_array : TextureTypeAttribute
{
    public texture_depth_2d_array (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube : TextureTypeAttribute
{
    public texture_depth_cube (int group, int binding) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube_array : TextureTypeAttribute
{
    public texture_depth_cube_array (int group, int binding) { }
}

#endregion


public interface ISampleType;

public struct i32 : ISampleType;
public struct u32 : ISampleType;
public struct f32 : ISampleType;


/// <remarks>
/// <b><a href="https://www.w3.org/TR/WGSL/#sampled-texture-type">
/// WGSL: Sampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_1d{ST}"/>                  - <see cref="TextureViewDimension.D1D"/><br/>
/// - <see cref="texture_2d{ST}"/>                  - <see cref="TextureViewDimension.D2D"/><br/>
/// - <see cref="texture_2d_array{ST}"/>            - <see cref="TextureViewDimension.D2DArray"/><br/>
/// - <see cref="texture_3d{ST}"/>                  - <see cref="TextureViewDimension.D3D"/><br/>
/// - <see cref="texture_cube{ST}"/>                - <see cref="TextureViewDimension.Cube"/><br/>
/// - <see cref="texture_cube_array{ST}"/>          - <see cref="TextureViewDimension.CubeArray"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#multisampled-texture-type">
/// WGSL: Multisampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_multisampled_2d{ST}"/>     - <see cref="TextureViewDimension.D2D"/><br/>
/// - <see cref="texture_depth_multisampled_2d"/>   - <see cref="TextureViewDimension.D2D"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-storage">
/// WGSL: Storage Texture Types
/// </a></b><br/>
/// - <see cref="texture_storage_1d{ST}"/>          - <see cref="TextureViewDimension.D1D"/><br/>
/// - <see cref="texture_storage_2d{ST}"/>          - <see cref="TextureViewDimension.D2D"/><br/>
/// - <see cref="texture_storage_2d_array{ST}"/>    - <see cref="TextureViewDimension.D2DArray"/><br/>
/// - <see cref="texture_storage_3d{ST}"/>          - <see cref="TextureViewDimension.D3D"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-depth">
/// WGSL: Depth Texture Types</a></b><br/>
/// - <see cref="texture_depth_2d"/>                - <see cref="TextureViewDimension.D2D"/><br/>
/// - <see cref="texture_depth_2d_array"/>          - <see cref="TextureViewDimension.D2DArray"/><br/>
/// - <see cref="texture_depth_cube"/>              - <see cref="TextureViewDimension.Cube"/><br/>
/// - <see cref="texture_depth_cube_array"/>        - <see cref="TextureViewDimension.CubeArray"/><br/>
/// </remarks>
internal interface ITextureViewDocs;
