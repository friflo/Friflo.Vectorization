// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

/// <summary> Specify a read-write buffer for a Kernel method as input/output parameter </summary>
/// <remarks>
/// Generic types that can be converted into an input/output parameter are:<br/>
/// <see cref="BufferView{T}"/>, <see cref="Span{T}"/> and <see cref="Memory{T}"/>.
/// </remarks>
public readonly ref struct Buffer<T> where T : unmanaged
{
    public  readonly    Span<T>         Span;
    public  readonly    GpuBuffer<T>    GpuBuffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;
    
    public  override    string          ToString() => BufferUtils.BufferToString(GpuBuffer, "Span");
    
    private Buffer(Span<T> span) {
        Span        = span;
        Length      = span.Length;
    }
    
    private Buffer(Memory<T> memory) {
        Span        = memory.Span;
        Length      = memory.Length;
    }
    
    private Buffer(BufferView<T> view) {
        GpuBuffer   = view.gpuBuffer;
        Span        = view.Span;
        Offset      = view.Offset;
        Length      = view.Length;
    }
    
    // --- CPU buffers
    public static implicit operator Buffer<T>(T[]           array)      => new(array);
    public static implicit operator Buffer<T>(Span<T>       span)       => new(span);
    public static implicit operator Buffer<T>(Memory<T>     memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator Buffer<T>(BufferView<T> view)       => new(view);
    
    // public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer);      intentionally not available
}

/// <summary> Specify a read-only buffer for a Kernel method as input parameter </summary>
/// <remarks>
/// Generic types that can be converted into an input parameter are:<br/>
/// <see cref="ReadOnlyView{T}"/>, <see cref="BufferView{T}"/>, <see cref="ReadOnlySpan{T}"/> and <see cref="ReadOnlyMemory{T}"/>.
/// </remarks>
public readonly ref struct InBuffer<T> where T : unmanaged
{
    public  readonly    ReadOnlySpan<T> Span;
    public  readonly    GpuBuffer<T>    GpuBuffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;

    public  override    string          ToString() => BufferUtils.BufferToString(GpuBuffer, "ReadOnlySpan");

    private InBuffer(ReadOnlySpan<T> span) {
        Span        = span;
        Length      = span.Length;
    }
    
    private InBuffer(ReadOnlyMemory<T> memory) {
        Span        = memory.Span;
        Length      = memory.Length;
    }
    
    private InBuffer(ReadOnlyView<T> view) {
        GpuBuffer   = view.gpuBuffer;
        Span        = view.Span;
        Offset      = view.Offset;
        Length      = view.Length;
    }
    
    private InBuffer(BufferView<T> view) {
        GpuBuffer   = view.gpuBuffer;
        Span        = view.Span;
        Offset      = view.Offset;
        Length      = view.Length;
    }
    
    
    // --- CPU buffers
    public static implicit operator InBuffer<T>(ReadOnlySpan<T>   span)       => new(span);
    public static implicit operator InBuffer<T>(ReadOnlyMemory<T> memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator InBuffer<T>(ReadOnlyView<T>   view)       => new(view);
    public static implicit operator InBuffer<T>(BufferView<T>     view)       => new(view); // read/write buffer also allowed
    
    // public static implicit operator InBuffer<T>      (GpuBuffer<T> gpuBuffer);   intentionally not available
    // public static implicit operator ReadOnlyBuffer<T>(T[] array));               intentionally not available
}

internal static class BufferUtils
{
    // --- Buffer<>, InBuffer<>
    internal static string BufferToString<T>(GpuBuffer<T> gpuBuffer, string spanType) where T : unmanaged
    {
        var type     = typeof(T);
        var typeCode = Type.GetTypeCode(type);
        return BufferToString(gpuBuffer != null, typeCode, type.Name, gpuBuffer?.Label, spanType);
    }
    
    private static string BufferToString(bool isBuffer, TypeCode typeCode, string typeName, string label, string spanType)
    {
        typeName = GetTypeName(typeCode, typeName);
        return isBuffer ? $"GpuBuffer<{typeName}> '{label}'" : $"{spanType}<{typeName}>";
    }
    
    // --- BufferView<>, ReadOnlyView<>
    internal static string ViewToString<T>(string structName, GpuBuffer<T> gpuBuffer, int offset, int length) where T : unmanaged
    {
        var type     = typeof(T);
        var typeCode = Type.GetTypeCode(type);
        return ViewToString(structName, typeCode, type.Name, gpuBuffer.Label, offset, length);
    }
    
    private static string ViewToString(string structName, TypeCode typeCode, string typeName, string label, int offset, int length)
    {
        typeName = GetTypeName(typeCode, typeName);
        return $"{structName}<{typeName}> '{label}' [{offset}, {offset + length}]";
    }
    
    private static string GetTypeName(TypeCode typeCode, string typeName)
    {
        return typeCode switch {
            TypeCode.Boolean    => "bool",
            TypeCode.Char       => "char",
            TypeCode.SByte      => "sbyte",
            TypeCode.Byte       => "byte",
            TypeCode.Int16      => "short",
            TypeCode.UInt16     => "ushort",
            TypeCode.Int32      => "int",
            TypeCode.UInt32     => "uint",
            TypeCode.Int64      => "long",
            TypeCode.UInt64     => "ulong",
            TypeCode.Single     => "float",
            TypeCode.Double     => "double",
            TypeCode.Decimal    => "decimal",
            _                   => typeName
        };
    }
}



