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
/// - <see cref="texture_1d"/><br/>
/// - <see cref="texture_2d"/><br/>
/// - <see cref="texture_2d_array"/><br/>
/// - <see cref="texture_3d"/><br/>
/// - <see cref="texture_cube"/><br/>
/// - <see cref="texture_cube_array"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#multisampled-texture-type">
/// WGSL: Multisampled Texture Types
/// </a></b><br/>
/// - <see cref="texture_multisampled_2d"/><br/>
/// - <see cref="texture_depth_multisampled_2d"/><br/>
/// <br/>
/// <b><a href="https://www.w3.org/TR/WGSL/#texture-storage">
/// WGSL: Storage Texture Types
/// </a></b><br/>
/// - <see cref="texture_storage_1d"/><br/>
/// - <see cref="texture_storage_2d"/><br/>
/// - <see cref="texture_storage_2d_array"/><br/>
/// - <see cref="texture_storage_3d"/><br/>
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
public sealed class texture_1d : TextureTypeAttribute
{
    public texture_1d           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d : TextureTypeAttribute
{
    public texture_2d           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_2d_array : TextureTypeAttribute
{
    public texture_2d_array     (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_3d : TextureTypeAttribute
{
    public texture_3d           (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube : TextureTypeAttribute
{
    public texture_cube         (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_cube_array : TextureTypeAttribute
{
    public texture_cube_array   (ST sampleType) { }
}
#endregion




#region --- Multisampled Texture Types
// See:  https://www.w3.org/TR/WGSL/#multisampled-texture-type

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_multisampled_2d : TextureTypeAttribute
{
    public texture_multisampled_2d      (ST sampleType) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_multisampled_2d : TextureTypeAttribute;
#endregion




#region --- Storage Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-storage

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_1d : TextureTypeAttribute
{
    public texture_storage_1d       (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d : TextureTypeAttribute
{
    public texture_storage_2d       (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_2d_array : TextureTypeAttribute
{
    public texture_storage_2d_array (TextureFormat format, TSA access) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_storage_3d : TextureTypeAttribute
{
    public texture_storage_3d       (TextureFormat format, TSA access) { }
}
#endregion




#region --- Depth Texture Types
// See:  https://www.w3.org/TR/WGSL/#texture-depth

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_2d_array : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube : TextureTypeAttribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class texture_depth_cube_array : TextureTypeAttribute;

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
