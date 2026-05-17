// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;

public readonly struct EmissionResult : IEquatable<EmissionResult>
{
    public  readonly string                 name;
    public  readonly string                 code;
    public  readonly List<DiagnosticData>   diagnostics;
    private readonly int                    cachedHash;
    // --- exception
    public  readonly string?                exceptionMessage;
    private readonly string?                exceptionStacktrace;
    private readonly Location?              methodLocation;
    
    public EmissionResult(string? message, string? stacktrace, Location? methodLocation)
    {
        exceptionMessage    = message;
        exceptionStacktrace = stacktrace;
        this.methodLocation = methodLocation;
    }

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
    
    public void ReportException (SourceProductionContext productionContext)
    {
        var customDescriptor = new DiagnosticDescriptor(
            id:             "ECSGEN008",
            title:          "Transpiler exception",
            messageFormat:  "Transpiler exception - {0}",
            category:       "Design",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );
        productionContext.ReportDiagnostic(Diagnostic.Create(customDescriptor, methodLocation, exceptionMessage));
        
        var traceLines = exceptionStacktrace?.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        
        int frameIndex = 1;
        foreach (var line in traceLines) {
            var traceDescriptor = new DiagnosticDescriptor(
                id:             $"ECSGEN008_{frameIndex++:D2}",
                title:          "Transpiler Stacktrace",
                messageFormat:  "{0}",
                category:       "Design",
                defaultSeverity: DiagnosticSeverity.Warning,
                isEnabledByDefault: true
            );
            productionContext.ReportDiagnostic(Diagnostic.Create(traceDescriptor, methodLocation, line.Trim()));
        }
    }
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
