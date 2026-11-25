// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
using SkiaSharp;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;


internal static unsafe class SkiaApiNative
{
    /// Use native method to avoid allocation required otherwise.
    /// <see cref="SKVertices.CreateCopy(SKVertexMode, SKPoint[], SKPoint[], SKColor[], ushort[])"/>
    internal static SKVertices CreateCopy(
        SKVertexMode    vmode,
        int             vertexCount,
        SKPoint[]       positions,
        SKPoint[]       texs,
        SKColor[]       colors,
        int             indexCount,
        ushort[]        indices)
    {
        fixed (SKPoint* positions1  = positions)
        fixed (SKPoint* texs1       = texs)
        fixed (SKColor* colors1     = colors)
        fixed(ushort*   indices1    = indices) {
            var handle = MakeCopy(vmode, vertexCount, positions1, texs1, (uint*)colors1, indexCount, indices1);
            return GetObject(handle);
        }
    }
    
    private delegate IntPtr SkVerticesMakeCopyDelegate(
        SKVertexMode vmode,
        int vertexCount,
        SKPoint* positions,
        SKPoint* texs,
        uint* colors,
        int indexCount,
        ushort* indices);

    private static readonly SkVerticesMakeCopyDelegate MakeCopy = CreateDelegate();

    private static SkVerticesMakeCopyDelegate CreateDelegate()
    {
        // Assembly of a known SkiaSharp type, e.g. SKVertices or SKCanvas
        Assembly skiaAssembly = typeof(SKVertices).Assembly;

        Type skiaApiType = skiaAssembly.GetType("SkiaSharp.SkiaApi", throwOnError: false)
                            ?? throw new MissingMemberException("SkiaSharp.SkiaApi not found.");

        MethodInfo method = skiaApiType.GetMethod(
                                 "sk_vertices_make_copy",
                                 BindingFlags.Static | BindingFlags.NonPublic)
                             ?? throw new MissingMethodException("sk_vertices_make_copy not found.");

        return method.CreateDelegate<SkVerticesMakeCopyDelegate>();
    }
    
    private static SKVertices GetObject(IntPtr handle) {
        return handle != IntPtr.Zero ? Create(handle, true) : null;
    }
    

    /// <see cref="SKVertices"/>  internal SKVertices(IntPtr x, bool owns)
    private static readonly Func<IntPtr, bool, SKVertices> Create = CreateFactory();

    private static Func<IntPtr, bool, SKVertices> CreateFactory()
    {
        Type type = typeof(SKVertices);
        ConstructorInfo ctor = type.GetConstructor(
                                   BindingFlags.Instance | BindingFlags.NonPublic,
                                   binder: null,
                                   types: new[] { typeof(IntPtr), typeof(bool) },
                                   modifiers: null)
                               ?? throw new MissingMemberException(nameof(SKVertices), ".ctor(IntPtr, bool)");

        ParameterExpression handleParam = Expression.Parameter(typeof(IntPtr), "x");
        ParameterExpression ownsParam = Expression.Parameter(typeof(bool), "owns");
        NewExpression newExpr = Expression.New(ctor, handleParam, ownsParam);

        return Expression.Lambda<Func<IntPtr, bool, SKVertices>>(newExpr, handleParam, ownsParam).Compile();
    }
}