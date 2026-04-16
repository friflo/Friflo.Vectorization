// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Friflo.Vectorization.Generators;


public class Query
{
    public required IMethodSymbol                   BlueprintMethod    { get; init; }
    public required VectorMode                      VectorMode      { get; init; }
    public required ImmutableArray<AttributeData>   Attributes      { get; init; }
    public required ImmutableArray<IParameterSymbol>Parameters      { get; init; }
    public required List<IParameterSymbol>          Spans           { get; init; }
    public required NamedTypes                      NamedTypes      { get; init; }
    public required SemanticModel                   SemanticModel   { get; init; }
    public required string                          Hash            { get; init; }
    // --- generated output
    public readonly List<DiagnosticData>            diagnostics = new();
    public          int                             vectorDimension;    // [3, 4]
    public          int                             laneCount;          // [3, 2]
    public          StringBuilder[]                 lanes;
    public          VectorType[]                    vectorTypes;
    public          bool                            vectorized;
    public          string                          avxMethod = "";
    public readonly Dictionary<string, Param>       paramTypes = new ();
    public readonly StringBuilder                   locals = new ();
    public readonly StringBuilder                   computeTemp = new ();
    public          int                             computeTempCount;
    public          int                             constLocalsCount;
    public          bool                            requireDeinterleave;
    public          bool                            useDeinterleave; // true: SoA   false: AoS

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
            location = BlueprintMethod.Locations.FirstOrDefault();
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
        if (!useDeinterleave) {
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

public enum ExecutionStrategy
{
    NativeSoA    = 0, // Operation: Any                                     [Layout: [SoA] All]     - lane-native speed
    VerticalAoS  = 1, // Operation: Add, Sub, Mul, (Dot, Length, Distance)  [Layout: AoS-Vertical]  - lane-native speed
    MixedAdapter = 2, // Operation: Any                                     [Layout: AoS-SoA-Mixed] - lane-native speed + Deinterleave penalty
    Horizontal   = 3  // Operation: Transform, Cross, Normalize             [Layout: Horizontal]    - lane-native speed + Deinterleave penalty
}

public enum GenerateTrigger
{
    QueryAttribute,
    VectorizeAttribute
}

public struct Param
{
    public bool isComponent;
    public bool isScalar;
    public bool isParam;
    public int  dimension;
}

public enum VectorMode {
    None,
    Vector,
    Query
}

public struct NamedTypes
{
    public INamedTypeSymbol componentInterface;
    public INamedTypeSymbol entityStruct;
    public INamedTypeSymbol omitHashAttribute;
    
    public bool IsEntityParameter(IParameterSymbol parameter) {
        return parameter.Name == "entity" && SymbolEqualityComparer.Default.Equals(parameter.Type, entityStruct);
    }

    public bool IsComponent(ITypeSymbol typeSymbol) {
        return typeSymbol.AllInterfaces.Contains(componentInterface);
    }
}