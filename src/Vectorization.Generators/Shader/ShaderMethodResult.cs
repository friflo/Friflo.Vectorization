// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.WGSL.Transpiler.CSharp;
// ReSharper disable ConvertToPrimaryConstructor

// ReSharper disable once CheckNamespace
namespace Friflo;

public class ShaderMethodResult : IEquatable<ShaderMethodResult>
{
    public  readonly    CsMethod    method;
    private readonly    int         hashCode;
    
    public ShaderMethodResult(CsMethod method) {
        this.method = method;
        hashCode    = method.GetHashCode();
    }

    public override int GetHashCode() => hashCode;
    
    public bool Equals(ShaderMethodResult? other)
    {
        if (other is null)                  return false;
        if (ReferenceEquals(this, other))   return true;
        
        if (hashCode != other.hashCode)     return false;
        
        // deep tree equals check
        return method.Equals(other.method);
    }

    public override bool Equals(object? obj) {
        return obj is ShaderMethodResult other && Equals(other);
    }
}
