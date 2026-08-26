// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.CompilerServices;
using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

public readonly struct ImTexture : IEquatable<ImTexture>
{
    public readonly object? obj;            // 8 byte
    public readonly nint    handle;         // 8 byte
    public readonly Vector2 whiteUv;        // 8 byte
    public readonly bool    hasWhitePixel;  // 1 byte

    public override string? ToString()      => obj != null ? obj.ToString() : $"handle: {handle}";

    public ImTexture(object obj, nint handle, Vector2 whiteUv)
    {
        this.obj            = obj;
        this.handle         = handle;
        this.whiteUv        = whiteUv;
        this.hasWhitePixel  = true;
    }
    
    public ImTexture(object obj, nint handle)
    {
        this.obj            = obj;
        this.handle         = handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ImTexture other) {
        return ReferenceEquals(obj, other.obj) && handle == other.handle;
    }

    public override bool Equals(object? obj) {
        return obj is ImTexture other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        int objHash = obj != null ? RuntimeHelpers.GetHashCode(obj) : 0;
        return HashCode.Combine(objHash, handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in ImTexture left, in ImTexture right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in ImTexture left, in ImTexture right) => !left.Equals(right);
}


/*
internal readonly struct ImTexture
{
    internal readonly   GpuTextureView  native;
    internal readonly   bool            hasWhitePixel;
    internal readonly   Vector2         whiteUv;
    
    internal            nint            Handle      => native.Handle;
    public              bool            IsDisposed  => native.IsDisposed;
    public   override   string          ToString()  => native.ToString();

    internal ImTexture(GpuTextureView native) {
        this.native     = native;
    }

    internal ImTexture(GpuTextureView native, Vector2 whiteUv) {
        this.native     = native;
        hasWhitePixel   = true;
        this.whiteUv    = whiteUv;
    }
    // Intentionally not using: public static implicit operator ImTextureView(GpuTextureView view) => new(view);
}
*/