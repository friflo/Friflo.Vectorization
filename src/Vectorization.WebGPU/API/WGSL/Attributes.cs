// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;



#region ---------- Shader method Attribute

[AttributeUsage(AttributeTargets.Method, AllowMultiple =  true)]
public sealed class ShaderAttribute : Attribute
{
    public ShaderAttribute([PathReference] string wgsl, string vertex = null, string fragment = null) { }
}

#endregion



#region ---------- Draw method / parameter Attributes

[AttributeUsage(AttributeTargets.Method)]
public sealed class DrawVertexIndexAttribute : Attribute
{
    public DrawVertexIndexAttribute(
        uint vertexCount    = 3, 
        uint instanceCount  = 1, 
        uint firstVertex    = 0, 
        uint firstInstance  = 0) { }
}

/// <summary><a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">Draw()</a>
/// the annotated storage, uniform or vertex buffer. </summary>
/// <remarks>
/// Buffer types are annotated by either:<br/>
/// - <see cref="storageAttribute"/><br/>
/// - <see cref="uniformAttribute"/><br/>
/// - <see cref="VertexBufferAttribute"/><br/>
/// <br/>
/// Use <see cref="DrawInstanceAttribute"/>, <see cref="DrawFirstVertexAttribute"/>
/// or <see cref="DrawFirstInstanceAttribute"/> to set additional Draw() parameters.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawAttribute : Attribute;

/// <summary>
/// Optional - set <b>instanceCount</b> in <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">Draw()</a>
/// by the <c>Length</c> of the annotated <c>InBuffer&lt;&gt;</c> parameter.<br/>
/// If missing <b>instanceCount</b> defaults to 1.
/// </summary>
/// <remarks> Requires another parameter is annotated with <see cref="DrawAttribute"/>. </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawInstanceAttribute : Attribute;

/// <summary>
/// Optional - set <b>firstVertex</b> in <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">Draw()</a>
/// with an <c>int</c> parameter.<br/>
/// If missing <b>firstVertex</b> defaults to 0.
/// </summary>
/// <remarks> Requires another parameter is annotated with <see cref="DrawAttribute"/>. </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawFirstVertexAttribute : Attribute;

/// <summary>
/// Optional - set <b>firstInstance</b> in <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">Draw()</a>
/// with an <c>int</c> parameter.<br/>
/// If missing <b>firstInstance</b> defaults to 0.
/// </summary>
/// <remarks> Requires another parameter is annotated with <see cref="DrawAttribute"/>. </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawFirstInstanceAttribute : Attribute;

#endregion



#region ---------- GpuBuffer<> parameter Attributes

/// <summary> Specifies that the parameter binds a vertex buffer to the designated GPU input slot. </summary>
/// <remarks>
/// <para>
/// Vertex buffers are created with buffer type <see cref="BufferType.Vertex"/> using <c>GpuDevice.CreateBuffer()</c>.
/// </para>
/// <para> A vertex buffer requires a <see cref="GpuVertexBufferLayout"/>. </para>
/// Example using a vertex buffer parameter:<br/>
/// <c>[VertexBuffer(0)] InBuffer&lt;float&gt; vertexBuffer, // slot = 0</c>
/// <code>
/// desc.VertexState.buffers = [
///     new GpuVertexBufferLayout {  // buffers[0]  ->  slot = 0
///         arrayStride = Cube.cubeVertexSize,
///         attributes = [
///             new GpuVertexAttribute {
///                 shaderLocation = 0,     // WGSL: @location(0) position : vec4f (Im Shader)
///                 offset = Cube.cubePositionOffset,
///                 format = VertexFormat.Float32x4
///             },
///             new GpuVertexAttribute {
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
    /// <param name="slot">Maps to the <see cref="GpuVertexState.buffers"/> element index. </param>
    public VertexBufferAttribute (int slot) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class IndexBufferAttribute : Attribute
{ }


[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MapAttribute : Attribute
{
    public MapAttribute (int group, int binding) { }
}

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
public sealed class storageAttribute : Attribute;

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
public sealed class uniformAttribute : Attribute;

#endregion



[AttributeUsage(AttributeTargets.Method)]
public sealed class NoEmitAttribute : Attribute;