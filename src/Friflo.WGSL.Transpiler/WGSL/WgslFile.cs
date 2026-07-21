// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Friflo.WGSL.Transpiler.WGSL;

public readonly struct WgslFile : IEquatable<WgslFile>
{
    public required     string          NormalizedPath  { get; init; }
    public required     ulong           Hash            { get; init; }
    public required     string          Content         { get; init; }
    public required     WgslModule      Module          { get; init; }


    public override     int             GetHashCode() => (int)Hash;

    public override     bool            Equals(object other) => other is WgslFile that && Equals(that);

    public bool Equals(WgslFile other) {
        return Hash == other.Hash;
    }

    public override     string      ToString()      => NormalizedPath;
}
