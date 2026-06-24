// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedTypeParameter
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;



#region ========================= General & Multiline Texture Types

// ------ GpuTexture1D
public sealed class GpuTexture1D : GpuTexture
{
    internal unsafe GpuTexture1D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_1d<T> texture_1d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_1d<T>(CreateView(desc, TextureViewDimension.D1D, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_1d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_1d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTexture2D
public sealed class GpuTexture2D : GpuTexture
{
    internal unsafe GpuTexture2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_2d<T> texture_2d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_2d<T>(CreateView(desc, TextureViewDimension.D2D, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_2d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_2d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTexture2DArray
public sealed class GpuTexture2DArray : GpuTexture
{
    internal unsafe GpuTexture2DArray(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_2d_array<T> texture_2d_array<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_2d_array<T>(CreateView(desc, TextureViewDimension.D2DArray, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_2d_array&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_2d_array<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTexture3D
public sealed class GpuTexture3D : GpuTexture
{
    internal unsafe GpuTexture3D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_3d<T> texture_3d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_3d<T>(CreateView(desc, TextureViewDimension.D3D, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_3d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_3d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureCube
public sealed class GpuTextureCube : GpuTexture
{
    internal unsafe GpuTextureCube(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_cube<T> texture_cube<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_cube<T>(CreateView(desc, TextureViewDimension.Cube, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_cube&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_cube<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureCubeArray
public sealed class GpuTextureCubeArray : GpuTexture
{
    internal unsafe GpuTextureCubeArray(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_cube_array<T> texture_cube_array<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_cube_array<T>(CreateView(desc, TextureViewDimension.CubeArray, GetType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_cube_array&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_cube_array<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}
#endregion



#region =========================  Depth Texture Types

// ------ GpuTextureDepth2D
public sealed class GpuTextureDepth2D : GpuTexture
{
    internal unsafe GpuTextureDepth2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_depth_2d texture_depth_2d(in TextureViewDescriptor desc = default)
    {
        return new texture_depth_2d(CreateView(desc, TextureViewDimension.D2D, TextureSampleType.Depth), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_depth_2d</c>. </summary>
public readonly unsafe struct texture_depth_2d(TextureView* handle, GpuTexture texture) : ITextureView
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureDepth2DArray
public sealed class GpuTextureDepth2DArray : GpuTexture
{
    internal unsafe GpuTextureDepth2DArray(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_depth_2d_array texture_depth_2d_array(in TextureViewDescriptor desc = default)
    {
        return new texture_depth_2d_array(CreateView(desc, TextureViewDimension.D2DArray, TextureSampleType.Depth), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_depth_2d_array</c>. </summary>
public readonly unsafe struct texture_depth_2d_array(TextureView* handle, GpuTexture texture) : ITextureView
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureDepthCube
public sealed class GpuTextureDepthCube : GpuTexture
{
    internal unsafe GpuTextureDepthCube(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_depth_cube texture_depth_cube(in TextureViewDescriptor desc = default)
    {
        return new texture_depth_cube(CreateView(desc, TextureViewDimension.Cube, TextureSampleType.Depth), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_depth_cube</c>. </summary>
public readonly unsafe struct texture_depth_cube(TextureView* handle, GpuTexture texture) : ITextureView
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureDepthCubeArray
public sealed class GpuTextureDepthCubeArray : GpuTexture
{
    internal unsafe GpuTextureDepthCubeArray(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_depth_cube_array texture_depth_cube_array(in TextureViewDescriptor desc = default)
    {
        return new texture_depth_cube_array(CreateView(desc, TextureViewDimension.CubeArray, TextureSampleType.Depth), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_depth_cube_array</c>. </summary>
public readonly unsafe struct texture_depth_cube_array(TextureView* handle, GpuTexture texture) : ITextureView
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}
#endregion



#region =========================  Multisampled Texture Types

// ------ GpuTextureMultisampled2D
public sealed class GpuTextureMultisampled2D : GpuTexture
{
    internal unsafe GpuTextureMultisampled2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_multisampled_2d<T> texture_multisampled_2d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_multisampled_2d<T>(CreateView(desc, TextureViewDimension.D2D, GetUnfilterableType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_multisampled_2d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_multisampled_2d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureDepthMultisampled2D
public sealed class GpuTextureDepthMultisampled2D : GpuTexture
{
    internal unsafe GpuTextureDepthMultisampled2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_depth_multisampled_2d texture_depth_multisampled_2d(in TextureViewDescriptor desc = default)
    {
        return new texture_depth_multisampled_2d(CreateView(desc, TextureViewDimension.D2D, TextureSampleType.Depth), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_depth_multisampled_2d</c>. </summary>
public readonly unsafe struct texture_depth_multisampled_2d(TextureView* handle, GpuTexture texture) : ITextureView
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}
#endregion



#region =========================  Storage Texture Types

// ------ GpuTextureStorage1D
public sealed class GpuTextureStorage1D : GpuTexture
{
    internal unsafe GpuTextureStorage1D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_storage_1d<T> texture_storage_1d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_storage_1d<T>(CreateView(desc, TextureViewDimension.D1D, GetUnfilterableType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_storage_1d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_storage_1d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureStorage2D
public sealed class GpuTextureStorage2D : GpuTexture
{
    internal unsafe GpuTextureStorage2D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_storage_2d<T> texture_storage_2d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_storage_2d<T>(CreateView(desc, TextureViewDimension.D2D, GetUnfilterableType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_storage_2d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_storage_2d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureStorage2DArray
public sealed class GpuTextureStorage2DArray : GpuTexture
{
    internal unsafe GpuTextureStorage2DArray(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_storage_2d_array<T> texture_storage_2d_array<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_storage_2d_array<T>(CreateView(desc, TextureViewDimension.D2DArray, GetUnfilterableType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_storage_2d_array&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_storage_2d_array<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}

// ------ GpuTextureStorage3D
public sealed class GpuTextureStorage3D : GpuTexture
{
    internal unsafe GpuTextureStorage3D(WgpuDevice device, in TextureDescriptor desc, Texture* handle, string label) : base(device, desc, handle, label) { }
    
    public unsafe texture_storage_3d<T> texture_storage_3d<T>(in TextureViewDescriptor desc = default) where T : unmanaged
    {
        return new texture_storage_3d<T>(CreateView(desc, TextureViewDimension.D3D, GetUnfilterableType<T>()), this);
    }
}

/// <summary> A texture view that maps to the WGSL type: <c>texture_storage_3d&lt;T&gt;</c>. </summary>
public readonly unsafe struct texture_storage_3d<T>(TextureView* handle, GpuTexture texture) : ITextureView where T : unmanaged
{
    public          nint    Handle      => TextureViewUtils.GetHandle(handle, texture);
    public override string  ToString()  => texture.Label;
}
#endregion
