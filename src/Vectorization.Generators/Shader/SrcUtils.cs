// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class SrcUtils
{
    public static SrcLoc GetSrcLoc(this ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        return location.GetSrcLoc();
    }
    
    public static SrcLoc GetSrcLoc(this AttributeData attributeData)
    {
        var syntaxRef   = attributeData.ApplicationSyntaxReference;
        var location    = syntaxRef?.GetSyntax().GetLocation();
        return location.GetSrcLoc();            
    }
        
    private static SrcLoc GetSrcLoc(this Location? location)
    {
        if (location != null && location.IsInSource) {
            return new SrcLoc {
                path    = location.SourceTree?.FilePath,
                start   = location.SourceSpan.Start,
                length  = location.SourceSpan.Length
            };
        }
        return default;
    }
    
    /// <summary>
    /// Resolves a fresh, compilation-safe <see cref="Location"/> from the serialized coordinates.
    /// </summary>
    /// <remarks>
    /// <b>Note:</b> Essential for Incremental Generators to prevent "SyntaxTree is not part of the compilation"  exceptions.
    /// It discards the obsolete syntax tree reference from previous compilation cycles and 
    /// maps the raw text span onto the current, active syntax tree matching the file path.<br/>
    /// <b>Important:</b> Required for <c>AddParamsCodeFixProvider</c> 
    /// </remarks>
    public static Location GetFreshLocation(this SrcLoc srcLoc, Compilation compilation)
    {
        if (string.IsNullOrEmpty(srcLoc.path))
            return Location.None;

        var freshTree = compilation.SyntaxTrees.FirstOrDefault(t => 
            string.Equals(t.FilePath, srcLoc.path, StringComparison.OrdinalIgnoreCase));

        if (freshTree != null) {
            var span = new TextSpan(srcLoc.start, srcLoc.length);
            return Location.Create(freshTree, span);
        }
        return Location.None;
    }
}