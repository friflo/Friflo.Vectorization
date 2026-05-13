// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Friflo.Vectorization.Generators;

public readonly struct EmissionResult : IEquatable<EmissionResult>
{
    public  readonly string                 name;
    public  readonly string                 code;
    public  readonly List<DiagnosticData>   diagnostics;
    private readonly int                    cachedHash;

    public EmissionResult(string name, string code, List<DiagnosticData> diagnostics)
    {
        this.name = name;
        this.code = code;
        this.diagnostics = diagnostics;
        int hash = 17;
        hash = hash * 23 + (name?.GetHashCode() ?? 0);
        hash = hash * 23 + (code?.GetHashCode() ?? 0);
        cachedHash = hash;
    }

    // Direct call, no boxing
    public bool Equals(EmissionResult other)
    {
        // 1. Check cached hash (O(1))
        if (cachedHash != other.cachedHash) return false;
        
        // 2. Check name (Short string)
        if (name != other.name) return false;

        // 3. Last resort: Check code (O(N))
        return string.Equals(code, other.code);
    }

    // Required overrides (just in case)
    public override bool Equals(object obj) => obj is EmissionResult other && Equals(other);
    public override int GetHashCode() => cachedHash;
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
