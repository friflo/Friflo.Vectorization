// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedTypeParameter
// ReSharper disable UnusedType.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


/// <summary> Annotates a shader method parameter passing a <see cref="GpuTextureView"/>. </summary>
/// <remarks>
/// The attribute names match exactly their corresponding WGSL texture type.<br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#sampled-texture-type">
/// WGSL: Sampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_1dAttribute"/><br/>
/// - <see cref="texture_2dAttribute"/><br/>
/// - <see cref="texture_2d_arrayAttribute"/><br/>
/// - <see cref="texture_3dAttribute"/><br/>
/// - <see cref="texture_cubeAttribute"/><br/>
/// - <see cref="texture_cube_arrayAttribute"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#multisampled-texture-type">
/// WGSL: Multisampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_multisampled_2dAttribute"/><br/>
/// - <see cref="texture_depth_multisampled_2dAttribute"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-storage">
/// WGSL: Storage Texture Types
/// </a></b><br/>
/// - <see cref="texture_storage_1dAttribute"/><br/>
/// - <see cref="texture_storage_2dAttribute"/><br/>
/// - <see cref="texture_storage_2d_arrayAttribute"/><br/>
/// - <see cref="texture_storage_3dAttribute"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-depth">
/// WGSL: Depth Texture Types</a></b><br/>
/// - <see cref="texture_depth_2dAttribute"/><br/>
/// - <see cref="texture_depth_2d_arrayAttribute"/><br/>
/// - <see cref="texture_depth_cubeAttribute"/><br/>
/// - <see cref="texture_depth_cube_arrayAttribute"/><br/>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public abstract class TextureTypeAttribute : Attribute;



#region --- Sampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#sampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_1dAttribute : TextureTypeAttribute
{
    public texture_1dAttribute           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2dAttribute : TextureTypeAttribute
{
    public texture_2dAttribute           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d_arrayAttribute : TextureTypeAttribute
{
    public texture_2d_arrayAttribute     (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_3dAttribute : TextureTypeAttribute
{
    public texture_3dAttribute           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cubeAttribute : TextureTypeAttribute
{
    public texture_cubeAttribute         (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube_arrayAttribute : TextureTypeAttribute
{
    public texture_cube_arrayAttribute   (ST sampleType) { }
}
#endregion




#region --- Multisampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_multisampled_2dAttribute : TextureTypeAttribute
{
    public texture_multisampled_2dAttribute      (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_multisampled_2dAttribute : TextureTypeAttribute;
#endregion




#region --- Storage Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-storage

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_1dAttribute : TextureTypeAttribute
{
    public texture_storage_1dAttribute       (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2dAttribute : TextureTypeAttribute
{
    public texture_storage_2dAttribute       (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d_arrayAttribute : TextureTypeAttribute
{
    public texture_storage_2d_arrayAttribute (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_3dAttribute : TextureTypeAttribute
{
    public texture_storage_3dAttribute       (TextureFormat format, TSA access) { }
}
#endregion




#region --- Depth Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-depth

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2dAttribute : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d_arrayAttribute : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cubeAttribute : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube_arrayAttribute : TextureTypeAttribute;

#endregion

/// <summary>
/// ST - Sampled type. See: <a href="https://www.w3.org/TR/WGSL/#sampled-texture-type">WGSL: Sampled Texture Types</a>.
/// </summary>
public enum ST {
    i32 = 1,
    u32 = 2,
    f32 = 3
}


/// <summary>
/// TSA - Texture Storage Access. See: <a href="https://www.w3.org/TR/WGSL/#access-mode">WGSL: Access Mode</a>.
/// </summary>
public enum TSA {
    read        = 1,
    write       = 2,
    read_write  = 3
}
