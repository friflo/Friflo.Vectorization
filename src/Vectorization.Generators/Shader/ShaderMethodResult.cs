// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
// ReSharper disable ConvertToPrimaryConstructor

// ReSharper disable once CheckNamespace
namespace Friflo;

public class ShaderMethodResult : IEquatable<ShaderMethodResult>
{
    public  readonly    string?                 fileName; 
    public  readonly    CsMethod?               method;
    private readonly    int                     hashCode;
    private readonly    List<DiagnosticData>    diagnostics;
    private readonly    GeneratorError          error;
    
    public ShaderMethodResult(string fileName, CsMethod method, List<DiagnosticData> diagnostics) {
        this.fileName       = fileName;
        this.method         = method;
        hashCode            = method.GetHashCode();
        this.diagnostics    = diagnostics;
    }
    
    public ShaderMethodResult(List<DiagnosticData> diagnostics) {
        this.diagnostics    = diagnostics;
    }
    
    public ShaderMethodResult(GeneratorError error)
    {
        diagnostics = [];
        this.error  = error;
    }

    public override int GetHashCode() => hashCode;
    
    public bool Equals(ShaderMethodResult? other)
    {
        if (other is null)                  return false;
        if (ReferenceEquals(this, other))   return true;
        
        if (hashCode != other.hashCode)     return false;
        
        if (method != null) {
            // deep tree equals check
            return method.Equals(other.method);
        }
        return other.method != null;
    }

    public override bool Equals(object? obj) {
        return obj is ShaderMethodResult other && Equals(other);
    }
}
