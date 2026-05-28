// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;

// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU.Runtime;

internal static class BufferUtils
{
    private static long IdCounter;
    
    internal static long NextId() => Interlocked.Increment(ref IdCounter);
    
    
    // --- Buffer<>, InBuffer<>
    internal static string BufferToString<T>(GpuBuffer<T> gpuBuffer, string spanType, int length) where T : unmanaged
    {
        var type     = typeof(T);
        var typeCode = Type.GetTypeCode(type);
        return BufferToString(gpuBuffer != null, typeCode, type.Name, gpuBuffer?.Label, spanType, length);
    }
    
    private static string BufferToString(bool isBuffer, TypeCode typeCode, string typeName, string label, string spanType, int length)
    {
        typeName = GetTypeName(typeCode, typeName);
        return isBuffer ? $"GpuBuffer<{typeName}> '{label}'  Length: {length}" : $"{spanType}<{typeName}>  Length: {length}";
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
        return $"{structName}<{typeName}> '{label}' [{offset}..{offset + length}]";
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


