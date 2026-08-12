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

/// <summary>
/// Associates a WGSL shader file with the annotated method and specifies the entry points for the pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple =  true)]
public sealed class ShaderAttribute : Attribute
{
    /// <param name="wgslPath">Relative path - with prefix <c>~/</c> to the .wgsl source file.</param>
    /// <param name="vertex"  >Name of the <c>@vertex</c> shader entry point function.</param>
    /// <param name="fragment">Name of the <c>@fragment</c> shader entry point function.</param>
    /// <param name="compute" >Name of the <c>@compute</c> shader entry point function.</param>
    public ShaderAttribute([PathReference] string wgslPath, string vertex = null, string fragment = null, string compute = null) { }
}

#endregion



#region ---------- Draw method / parameter Attributes

/// <summary>
/// Marks the buffer that provides the vertex data for the draw command.<br/>
/// See: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">MDN: Draw()</a> for reference.
/// </summary>
/// <remarks>
/// Buffer types are annotated by either:<br/>
/// - <see cref="storageAttribute"/><br/>
/// - <see cref="uniformAttribute"/><br/>
/// - <see cref="VertexBufferAttribute"/><br/>
/// <br/>
/// Use <see cref="DrawInstanceAttribute"/> to set additional Draw() parameters.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawAttribute : Attribute;

/// <summary>
/// Optional - set <b>instanceCount</b> in <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw">Draw()</a>
/// by the <see cref="InBuffer{T}.Length"/> of the annotated <see cref="InBuffer{T}"/> parameter.
/// </summary>
/// <remarks>
/// - If <see cref="DrawInstanceAttribute"/> is missing <b>instanceCount</b> defaults to 1.<br/>
/// - If the shader method has a <see cref="DrawArgs"/> parameter, <see cref="DrawInstanceAttribute"/> will be used in any case.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DrawInstanceAttribute : Attribute;

#endregion



#region ---------- Compute method / parameter Attributes

[AttributeUsage(AttributeTargets.Method)]
public sealed class WorkgroupSizeAttribute : Attribute
{
    public WorkgroupSizeAttribute(int workgroupCountX, int workgroupCountY = 1, int workgroupCountZ = 1) { }
}

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DispatchAttribute : Attribute;

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

/// <summary>
/// Marks a <c>[Shader]</c> method parameter as the index buffer for a WebGPU draw call.
/// </summary>
/// <remarks>
/// This attribute binds the annotated <see cref="InBuffer{T}"/> parameter before drawing.<br/>
/// Modern GPU hardware restricts each render pass to a single index slot, which enforces the following architecture:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Single Index Buffer Constraint:</b> Only one <c>[IndexBuffer]</c> attribute is allowed per shader method. 
///       Declaring multiple will trigger a compile-time error.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Combining with <c>[Draw]</c>:</b> When paired with the <c>[Draw]</c> attribute, the source generator 
///       automatically invokes <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/drawIndexed"><c>drawIndexed()</c></a>
///       and derives the <c>indexCount</c> directly from this buffer's length.
///     </description>
///   </item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class IndexBufferAttribute : Attribute;


/// <summary>
/// Maps a WGSL resource (e.g. <c>storage, uniform, ...</c>) to a specific bind <c>@group()</c> and <c>@binding()</c> index.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MapAttribute : Attribute
{
    /// <param name="group">The bind <c>@group()</c> (0-3).</param>
    /// <param name="binding">The <c>@binding()</c> index within the group.</param>
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


[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class,  AllowMultiple = true)]
public sealed class SourceAttribute : Attribute
{
    public SourceAttribute([PathReference] string wgslPath, string name = null) { }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class NoEmitAttribute : Attribute;