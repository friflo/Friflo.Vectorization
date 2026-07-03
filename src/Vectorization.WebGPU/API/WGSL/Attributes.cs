// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;
using JetBrains.Annotations;


// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

[AttributeUsage(AttributeTargets.Method)]
public sealed class NoEmitAttribute : Attribute
{
    public NoEmitAttribute() { }
}

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

/// <summary> Specifies that the parameter binds a vertex buffer to the designated GPU input slot. </summary>
/// <remarks>
/// <para>
/// Vertex buffers are created with buffer type <see cref="BufferType.Vertex"/> using <c>GpuDevice.CreateBuffer()</c>.
/// </para>
/// <para> A vertex buffer requires a <see cref="WgpuVertexBufferLayout"/>. </para>
/// Example using a vertex buffer parameter:<br/>
/// <c>[VertexBuffer(0)] InBuffer&lt;float&gt; vertexBuffer, // slot = 0</c>
/// <code>
/// desc.VertexState.buffers = [
///     new WgpuVertexBufferLayout {  // buffers[0]  ->  slot = 0
///         arrayStride = Cube.cubeVertexSize,
///         attributes = [
///             new WgpuVertexAttribute {
///                 shaderLocation = 0,     // WGSL: @location(0) position : vec4f (Im Shader)
///                 offset = Cube.cubePositionOffset,
///                 format = VertexFormat.Float32x4
///             },
///             new WgpuVertexAttribute {
///                 shaderLocation = 1,     // WGSL: @location(1) uv       : vec2f (Im Shader)
///                 offset = Cube.cubeUVOffset,
///                 format = VertexFormat.Float32x2
///             },
///         ]
/// }];
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class VertexBufferAttribute : Attribute
{
    /// <param name="slot">Maps to the <see cref="WgpuVertexState.buffers"/> element index. </param>
    public VertexBufferAttribute (int slot) { }
}

// --- Generator Draw Call Rules ---
// 1. [BindIndex]   present              -> pass.DrawIndexed(indices.Length, [BindInstance] ?? 1, 0, 0, 0);
// 2. [BindStorage] only                 -> pass.Draw(vertices.Length, [BindInstance] ?? 1, 0, 0);
// 3. No geometry (Fullscreen/Compute)   -> pass.Draw(3, 1, 0, 0);

/// <summary>
/// Uses programmable vertex pulling via storage buffers instead of the fixed vertex input pipeline.<br/>
/// Vertices are fetched directly from an indexed buffer range, driven by the Draw() offset.
/// </summary>
/// <remarks>
/// See: <b><a href="https://www.w3.org/TR/WGSL/#var-and-value">WGSL: Variable and Value Declarations</a></b><br/>
/// <code>
///   // InBuffer&lt;&gt;      WGSL: var&lt;storage, read&gt;
///   C#    [BindStorage(0, 0)]    InBuffer&lt;VertexData&gt;  triangles,
///   WGSL:  @group(0) @binding(0) var&lt;storage, read&gt;    mesh_data:  VertexData;
///    
///   // InOutBuffer&lt;&gt;   WGSL: var&lt;storage, read_write&gt;
///   C#    [BindStorage(0, 0)]    InOutBuffer&lt;VertexData&gt;  triangles,
///   WGSL:  @group(0) @binding(0) var&lt;storage, read_write&gt; mesh_data:  VertexData;
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindStorageAttribute : Attribute
{
    public BindStorageAttribute (int group, int binding) { }
}

/// <summary> Annotates a shader method parameter passing a uniform. </summary>
/// <remarks>
/// See: <b><a href="https://www.w3.org/TR/WGSL/#var-and-value">WGSL: Variable and Value Declarations</a></b><br/>
/// A uniform can be any unmanaged C# type containing blittable data.<br/>
/// Supported primitives and math types include:<br/>
/// - <c>int</c> i32, <c>uint</c> u32, <c>float</c> f32<br/>
/// - <c>Vector2</c> vec2&lt;f32&gt;, <c>Vector3</c> vec3&lt;f32&gt;, <c>Vector4</c> vec4&lt;f32&gt;, <c>Matrix4x4</c> mat4x4&lt;f32&gt;<br/>
/// - Or any custom <c>struct</c> meeting WebGPU alignment rules (e.g., <c>Vector3</c> requires 16-byte alignment).<br/>
/// <br/>
/// <b>Restrictions:</b><br/>
/// <c>bool</c> is prohibited inside structs (use <c>uint</c>).<br/>
/// 64-bit types (<c>double</c>, <c>long</c>) and 8/16-bit types (<c>byte</c>, <c>short</c>) are not supported.<br/>
/// <code>
///   // struct MyUniforms      WGSL: var&lt;uniform&gt;
///   C#    [BindUniform(1, 0)]    InBuffer&lt;VertexData&gt;  myUniforms,
///   WGSL:  @group(1) @binding(0) var&lt;uniform&gt;          myUniforms: MyUniforms;
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindUniformAttribute : Attribute
{
    public BindUniformAttribute (int group, int binding) { }
}


[AttributeUsage(AttributeTargets.Parameter)]
public sealed class BindIndexAttribute : Attribute
{
    public BindIndexAttribute (int group, int binding) { }
}



