// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class SrcLocUtils
{
    public static SrcLoc GetSymbolLoc(this ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault();
        return location.GetSrcLoc();
    }
    
    public static (SrcLoc attrLoc, SrcLoc pathLoc, SrcLoc vertLoc, SrcLoc fragLoc, SrcLoc computeLoc)
        GetShaderSrcLocs(this AttributeData attributeData)
    {
        if (attributeData.ApplicationSyntaxReference == null) {
            return default;
        }
        var attributeSyntax = (AttributeSyntax)attributeData.ApplicationSyntaxReference.GetSyntax();
        var args = attributeSyntax.ArgumentList!.Arguments;
        
        SrcLoc vertLoc      = default;
        SrcLoc fragLoc      = default;
        SrcLoc computeLoc   = default;
        
        for(int n = 1; n < args.Count; n++)
        {
            var arg = args[n];
            if (arg.NameColon == null) continue;
            if (arg.NameColon.Name.Identifier.Text == "vertex") {
                vertLoc = arg.Expression.GetLocation().GetSrcLoc();
            }
            if (arg.NameColon.Name.Identifier.Text == "fragment") {
                fragLoc = arg.Expression.GetLocation().GetSrcLoc();
            }
            if (arg.NameColon.Name.Identifier.Text == "compute") {
                computeLoc = arg.Expression.GetLocation().GetSrcLoc();
            }
        }
        return (attributeSyntax.GetLocation().GetSrcLoc(),
                args[0].Expression.GetLocation().GetSrcLoc(),
                vertLoc,
                fragLoc,
                computeLoc);
    }
    
    public static (SrcLoc attrLoc, SrcLoc arg0Loc, SrcLoc arg1Loc)
        GetParamSrcLocs(this AttributeData? attributeData)
    {
        if (attributeData?.ApplicationSyntaxReference == null) {
            return default;
        }
        var attributeSyntax = (AttributeSyntax)attributeData.ApplicationSyntaxReference.GetSyntax();

        var attrLoc = attributeSyntax.GetLocation().GetSrcLoc();
        if (attributeSyntax.ArgumentList == null) {
            return (attrLoc, default, default);
        }
        var args = attributeSyntax.ArgumentList.Arguments;
        
        var arg0Loc = args.Count < 1 ? default : args[0].Expression.GetLocation().GetSrcLoc();
        var arg1Loc = args.Count < 2 ? default : args[1].Expression.GetLocation().GetSrcLoc();
        return (attrLoc, arg0Loc, arg1Loc);
    }
    
    public static SrcLoc GetAttributeLoc(this AttributeData attributeData)
    {
        var syntaxRef   = attributeData.ApplicationSyntaxReference;
        var location    = syntaxRef?.GetSyntax().GetLocation();
        return location.GetSrcLoc();            
    }
    
    public static (SrcLoc nameLoc, SrcLoc typeLoc, SrcLoc genericArgLoc) GetParameterLocs(this IParameterSymbol parameterSymbol)
    {
        var syntaxRef = parameterSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null) {
            return default;
        }
        var parameterSyntax = (ParameterSyntax)syntaxRef.GetSyntax();

        var parameterNameLocation = parameterSymbol.Locations.FirstOrDefault() 
            ?? parameterSyntax.Identifier.GetLocation();

        var parameterTypeLocation = Location.None;
        SrcLoc genericArgLoc = default; 
        if (parameterSyntax.Type != null)
        {
            parameterTypeLocation = parameterSyntax.Type.GetLocation();
            if (parameterSyntax.Type is GenericNameSyntax genericName) {
                var args = genericName.TypeArgumentList.Arguments;
                if (args.Count >= 1) {
                    genericArgLoc = args[0].GetLocation().GetSrcLoc();
                }
            }
        }
        return (parameterNameLocation.GetSrcLoc(), parameterTypeLocation.GetSrcLoc(), genericArgLoc);
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