// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.CompilerServices;
using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;

/// <summary>
/// A struct containing a reference or an opaque handle to a backend-specific texture.<br/>
/// In case of the WGPU backend, both are used to reuse a texture view once created.
/// </summary>
public readonly struct TmTexture : IEquatable<TmTexture>
{
    public readonly object? native;         // 8 byte
    public readonly nint    handle;         // 8 byte
    public readonly Vector2 whiteUv;        // 8 byte
    public readonly bool    hasWhitePixel;  // 1 byte

    public override string? ToString()      => native != null ? native.ToString() : $"handle: {handle}";

    public TmTexture(object native, nint handle, Vector2 whiteUv)
    {
        this.native     = native;
        this.handle     = handle;
        this.whiteUv    = whiteUv;
        hasWhitePixel   = true;
    }
    
    public TmTexture(in TmTexture texture, Vector2 whiteUv)
    {
        native          = texture.native;
        handle          = texture.handle;
        this.whiteUv    = whiteUv;
        hasWhitePixel   = true;
    }
    
    public TmTexture(object native, nint handle)
    {
        this.native     = native;
        this.handle     = handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TmTexture other) {
        return ReferenceEquals(native, other.native) && handle == other.handle;
    }

    public override bool Equals(object? obj) {
        return obj is TmTexture other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        int objHash = native != null ? RuntimeHelpers.GetHashCode(native) : 0;
        return HashCode.Combine(objHash, handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in TmTexture left, in TmTexture right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in TmTexture left, in TmTexture right) => !left.Equals(right);
}
