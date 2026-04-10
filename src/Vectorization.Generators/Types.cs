// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Friflo.Vectorization.Generators;

public struct EcsTypes
{
    public INamedTypeSymbol componentInterface;
    public INamedTypeSymbol entityStruct;
    public INamedTypeSymbol vectorizeAttribute;
    public INamedTypeSymbol omitHashAttribute;
    
    public bool IsEntityParameter(IParameterSymbol parameter) {
        return parameter.Name == "entity" && SymbolEqualityComparer.Default.Equals(parameter.Type, entityStruct);
    }

    public bool IsComponent(ITypeSymbol typeSymbol) {
        return typeSymbol.AllInterfaces.Contains(componentInterface);
    }
}

public enum GenerateTrigger
{
    QueryAttribute,
    VectorizedAttribute
}

public class Query
{
    public          IMethodSymbol                   methodSymbol;
    public          ImmutableArray<AttributeData>   attributes;
    public          ImmutableArray<IParameterSymbol>parameters;
    public          List<IParameterSymbol>          components;
    public          EcsTypes                        ecsTypes;
    public          SemanticModel                   semanticModel;
    // --- generated output
    public readonly List<DiagnosticData>            diagnostics = new();
    public          int                             vectorDimension;    // [3, 4]
    public          int                             laneCount;          // [3, 2]
    public          StringBuilder[]                 lanes;
    public          VectorType[]                    vectorTypes;
    public          string                          hash;
    public          bool                            vectorize;
    public          string                          avxMethod = "";
    public readonly Dictionary<string, Param>       paramTypes = new ();
    public readonly StringBuilder                   locals = new ();
    public readonly StringBuilder                   computeTemp = new ();
    public          int                             computeTempCount;
    public          int                             constLocalsCount;
    public          bool                            requireSoA;
    public          bool                            useSoA; // true: SoA   false: AoS

    public string AddConst() {
        return $"const{constLocalsCount++}";
    }
    public string AddTemp() {
        return $"temp{computeTempCount++}";
    }

    public void ReportDiagnosticSymbol(DiagnosticDescriptor descriptor, ISymbol? locationSymbol, params object?[]? messageArgs)
    {
        var location = locationSymbol?.Locations.FirstOrDefault();
        if (location == null) {
            location = methodSymbol.Locations.FirstOrDefault();
        }
        // Diagnostic diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
        // spc.ReportDiagnostic(diagnostic);
        AddDiagnostic(descriptor, location, messageArgs);
    }
    
    public void ReportDiagnosticSyntax(DiagnosticDescriptor descriptor, CSharpSyntaxNode syntaxNode, params object?[]? messageArgs)
    {
        var location = syntaxNode.GetLocation();
        // Diagnostic diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
        // spc.ReportDiagnostic(diagnostic);
        AddDiagnostic(descriptor, location, messageArgs);
    }
    
    private void AddDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
    {
        var lineSpan = location.GetLineSpan();
        var data = new DiagnosticData(
                Descriptor:     descriptor,
                FilePath:       lineSpan.Path,
                StartOffset:    location.SourceSpan.Start,
                Length:         location.SourceSpan.Length,
                StartLine:      lineSpan.StartLinePosition.Line,
                StartColumn:    lineSpan.StartLinePosition.Character,
                EndLine:        lineSpan.EndLinePosition.Line,
                EndColumn:      lineSpan.EndLinePosition.Character,
                MessageArgs:    messageArgs
            );
        diagnostics.Add(data);   
    }

    public void AddParam(string name, bool isComponent, bool isScalar, bool isParam, int dimension)
    {
        paramTypes.Add(name, new Param  { isComponent = isComponent, isScalar = isScalar, isParam = isParam, dimension = dimension });
    }
    
    public string GetVectorName(string name, int i)
    {
        if (!useSoA) {
            if (paramTypes.TryGetValue(name, out var paramSoa)) {
                if (paramSoa.isScalar) {
                    return $"{name}_scalar";
                }
            }
            return $"{name}_{i}";
        }
        if (paramTypes.TryGetValue(name, out var param)) {
            if (param.isComponent) {
                if (param.dimension > 1) {
                    return $"{name}_{i}";
                }
                if (vectorDimension == 2) {
                    return $"{name}_{i / (lanes.Length / 2)}";
                }
                return $"{name}_0";
            }
            if (param.dimension == 1 && param.isScalar && param.isParam) {
                return $"{name}_scalar";
            }
            if (param.dimension == 2 && param.isScalar && param.isParam) {
                return $"{name}_{i % 2}";
            }
            if (param.dimension == 1 && param.isScalar && !param.isParam) {
                return $"{name}_{i / (lanes.Length / 2)}";
            }
        }
        return $"{name}_{i}";
    }
}

public enum ParamType
{
    None,
    Scalar,
    Vector,
    Matrix4x4
}

public struct Param
{
    public bool isComponent;
    public bool isScalar;
    public bool isParam;
    public int  dimension;
}

public struct VectorType
{
    public IParameterSymbol parameter;
    public string           fullQualifiedName;
    public bool             isComponent;
    public bool             isScalar;
    public ITypeSymbol      valueType;
    public SpecialType      valueSpecialType;
    public ParamType        paramType;
    public int              dimension;

    public override string ToString() {
        return $"{parameter} : {valueType.Name} ({(paramType == ParamType.Vector ? "vector" : "scalar")})";
    }
}

public struct ConstValue
{
    public string       name;
    public string       value;
    public ParamType    paramType;
}

public record struct DiagnosticData(
    DiagnosticDescriptor    Descriptor,
    string                  FilePath,
    // Location?              Location, // has reference to SyntaxTree. Too heavy in memory use. 
    int                     StartOffset,
    int                     Length,
    int                     StartLine,      // Just an int
    int                     StartColumn,    // Just an int
    int                     EndLine,        // Just an int
    int                     EndColumn,      // Just an int
    object?[]?              MessageArgs
);

public readonly struct EmissionResult : IEquatable<EmissionResult>
{
    public  readonly string                 Name;
    public  readonly string                 Code;
    public  readonly List<DiagnosticData>   Diagnostics;
    private readonly int                    _cachedHash;

    public EmissionResult(string name, string code, List<DiagnosticData> diagnostics)
    {
        Name = name;
        Code = code;
        Diagnostics = diagnostics;
        int hash = 17;
        hash = hash * 23 + (name?.GetHashCode() ?? 0);
        hash = hash * 23 + (code?.GetHashCode() ?? 0);
        _cachedHash = hash;
    }

    // Direct call, no boxing
    public bool Equals(EmissionResult other)
    {
        // 1. Check cached hash (O(1))
        if (_cachedHash != other._cachedHash) return false;
        
        // 2. Check name (Short string)
        if (Name != other.Name) return false;

        // 3. Last resort: Check code (O(N))
        return string.Equals(Code, other.Code);
    }

    // Required overrides (just in case)
    public override bool Equals(object obj) => obj is EmissionResult other && Equals(other);
    public override int GetHashCode() => _cachedHash;
}