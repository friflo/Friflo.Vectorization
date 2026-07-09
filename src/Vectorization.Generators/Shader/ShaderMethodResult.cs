// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Vectorization.Generators;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo;

public class ShaderMethodResult : IEquatable<ShaderMethodResult>
{
    public  readonly    string?                 fileName; 
    public  readonly    CsMethod?               method;
    private readonly    int                     hashCode;
    public  readonly    List<DiagnosticData>    diagnostics;
    public  readonly    GeneratorError          error;
    
    private readonly    string?                 filePath;
    private readonly    int                     startPosition;
    private readonly    int                     length;
    
    public ShaderMethodResult(string fileName, CsMethod method, Location? location, List<DiagnosticData> diagnostics) {
        this.fileName       = fileName;
        this.method         = method;
        hashCode            = method.GetHashCode();
        this.diagnostics    = diagnostics;

        if (location != null && location.IsInSource) {
            filePath        = location.SourceTree?.FilePath;
            startPosition   = location.SourceSpan.Start;
            length          = location.SourceSpan.Length;
        }
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
    
    /// <summary>
    /// Resolves a fresh, compilation-safe <see cref="Location"/> from the serialized coordinates.
    /// </summary>
    /// <remarks>
    /// <b>Note:</b> Essential for Incremental Generators to prevent "SyntaxTree is not part of the compilation"  exceptions.
    /// It discards the obsolete syntax tree reference from previous compilation cycles and 
    /// maps the raw text span onto the current, active syntax tree matching the file path.<br/>
    /// <b>Important:</b> Required for <see cref="MissingParametersCodeFixProvider"/> 
    /// </remarks>
    public Location GetFreshLocation(Compilation compilation)
    {
        if (string.IsNullOrEmpty(this.filePath)) 
            return Location.None;

        var freshTree = compilation.SyntaxTrees.FirstOrDefault(t => 
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (freshTree != null) {
            var span = new TextSpan(startPosition, length);
            return Location.Create(freshTree, span);
        }
        return Location.None;
    }
}
