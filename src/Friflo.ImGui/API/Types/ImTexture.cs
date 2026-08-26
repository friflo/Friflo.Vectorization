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
    public readonly object? native;         // 8 byte
    public readonly nint    handle;         // 8 byte
    public readonly Vector2 whiteUv;        // 8 byte
    public readonly bool    hasWhitePixel;  // 1 byte

    public override string? ToString()      => native != null ? native.ToString() : $"handle: {handle}";

    public ImTexture(object native, nint handle, Vector2 whiteUv)
    {
        this.native     = native;
        this.handle     = handle;
        this.whiteUv    = whiteUv;
        hasWhitePixel   = true;
    }
    
    public ImTexture(ImTexture texture, Vector2 whiteUv)
    {
        native          = texture.native;
        handle          = texture.handle;
        this.whiteUv    = whiteUv;
        hasWhitePixel   = true;
    }
    
    public ImTexture(object native, nint handle)
    {
        this.native     = native;
        this.handle     = handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ImTexture other) {
        return ReferenceEquals(native, other.native) && handle == other.handle;
    }

    public override bool Equals(object? obj) {
        return obj is ImTexture other && Equals(other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        int objHash = native != null ? RuntimeHelpers.GetHashCode(native) : 0;
        return HashCode.Combine(objHash, handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in ImTexture left, in ImTexture right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in ImTexture left, in ImTexture right) => !left.Equals(right);
}
