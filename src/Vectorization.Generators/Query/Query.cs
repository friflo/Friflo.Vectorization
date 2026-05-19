// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

// ReSharper disable MergeIntoPattern
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

// --- 3+1 Strategy architecture ---
// The initial Strategy depends on VectorLayout (SoA, AoS) of the vector parameters:
//      Only SoA  -> NativeSoA
//      Only Aos  -> VerticalAoS
//      Otherwise -> MixedAdapter
// Strategy
//  NativeSoA:      Result from Traversal is final. No second pass. "Golden Path"
//  VerticalAoS:    Escalates to Horizontal always on: Dot, Cross | Distance, Length, LengthSquared | Normalize | Slerp, Reflect
//   				Transform get special treatment (no escalation)
//                  (outdated: Escalates to Horizontal whenever an operation’s required input shape deviates from the passed parameter's data shape)
//  MixedAdapter:   Second MixedAdapter pass required to apply Deinterleave() / Interleave()
//  Horizontal:     Escalated from VerticalAoS. Its result is final.
public enum Strategy
{
    NativeSoA    = 0, // [Layout: [SoA] All]     - lane-native speed
    VerticalAoS  = 1, // [Layout: AoS-Vertical]  - lane-native speed
    MixedAdapter = 2, // [Layout: AoS-SoA-Mixed] - lane-native speed + Deinterleave penalty
    Horizontal   = 3  // [Layout: Horizontal]    - lane-native speed + Deinterleave penalty
}

public sealed class Query
{
    // --- immutable input fields - created from blueprint method signature 
    public required IMethodSymbol                       BlueprintMethod { get; init; }
    public required string?                             CustomMethod    { get; init; }
    public required VectorMode                          VectorMode      { get; init; }
    public required ImmutableArray<AttributeData>       Attributes      { get; init; }
    public required ImmutableArray<BlueprintParameter>  Parameters      { get; init; }
    public required ImmutableArray<VectorType>          VectorTypes     { get; init; }
    public required ImmutableArray<BlueprintParameter>  Spans           { get; init; }
    public required SemanticModel                       SemanticModel   { get; init; }
    public required string                              Hash            { get; init; }
    
    // --- mutable output
    public required Diagnostics                 Diagnostics { get; init; }
    public          Strategy                    strategy;
    public          int                         vectorDimension;        // [1, 2, 3, 4]
    public          int                         laneCount;              // [4, 4, 3, 4]
    public          int                         scalarLaneCount;        // [4, 2, 1, 1]
    public          StringBuilder[]             lanes;
    public          bool                        vectorized;
    public          string                      avxMethod   = "";
    public          string                      wgslBody    = "";
    public readonly HashSet<string>             readVectors     = [];   // vectors that are used on the Right-Hand Side (RHS) of an expression
    public readonly List<string>                dirtyVectors    = [];   // contains vectors that are stored. Meaning they are "dirty"
    public readonly HashSet<string>             dirtyVectorsSet = [];   // same as dirtyVectors
    
    public readonly Dictionary<string, Param>   paramTypes      = new ();
    public readonly StringBuilder               locals          = new ();
    public readonly StringBuilder               computeTemp     = new ();
    private         int                         computeTempCount;
    private         int                         constLocalsCount;
    public          bool                        requireDeinterleave;
    public          bool                        useDeinterleave;        // true => add Deinterleave() / Interleave()
    public          bool                        isWgslLane;
    public readonly HashSet<string>             wgslHelperMethods = new(); 

    
    public void AddDirty(string vectorName)
    {
        if (!dirtyVectorsSet.Contains(vectorName)) {
            dirtyVectors.Add(vectorName); // DIRTY
            dirtyVectorsSet.Add(vectorName);
        }
    }
    
    public string AddConst() {
        return $"const{constLocalsCount++}";
    }
    public string AddTemp() {
        return $"temp{computeTempCount++}";
    }



    public void AddParam(string name, bool isComponent, bool isScalar, bool isParam, int dimension)
    {
        paramTypes.Add(name, new Param  { isComponent = isComponent, isScalar = isScalar, isParam = isParam, dimension = dimension });
    }
    
    public string GetVectorName(string name, int i)
    {
        if (isWgslLane) {
            // case: WGSL
            return $"_{name}";
        }
        // case: AVX
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
    
    public void ResetQueryState()
    {
        lanes = null;
        paramTypes.Clear();
        locals.Clear();
        computeTemp.Clear();
        computeTempCount = 0;
        constLocalsCount = 0;
        // TODO clear   readVectors, dirtyVectors & dirtyVectorsSet
    }
}

public enum GenerateTrigger
{
    QueryAttribute,
    VectorizeAttribute,
    KernelAttribute,
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
