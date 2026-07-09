// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators;



public readonly struct EmissionResult : IEquatable<EmissionResult>
{
    public  readonly string                 name;
    public  readonly string                 code;
    public  readonly List<DiagnosticData>   diagnostics;
    private readonly int                    cachedHash;
    
    // --- exception
    public readonly GeneratorError          error;
    
    public EmissionResult(GeneratorError error)
    {
        this.error  = error;
        name        = "";
        code        = "";
        diagnostics = [];
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
    
}



public readonly struct GeneratorError
{
    public  readonly string?                exceptionMessage;
    private readonly string?                exceptionStacktrace;
    private readonly Location?              methodLocation;
    
    public GeneratorError(string? message, string? stacktrace, Location? methodLocation)
    {
        exceptionMessage    = message;
        exceptionStacktrace = stacktrace;
        this.methodLocation = methodLocation;
    }
    
    public void ReportException (SourceProductionContext productionContext)
    {
        var customDescriptor = new DiagnosticDescriptor(
            id:             "ECSGEN008",
            title:          "Transpiler exception",
            messageFormat:  "{0}",
            category:       "Design",
            defaultSeverity: DiagnosticSeverity.Error,
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
            
            var cleanLine   = line.Trim();
            int inIndex     = cleanLine.IndexOf(" in ",   StringComparison.Ordinal);
            int lineIndex   = cleanLine.IndexOf(":line ", StringComparison.Ordinal);
            Location lineLocation;
            
            if (inIndex >= 0 && lineIndex > inIndex) {
                var filePath = cleanLine.Substring(inIndex + 4, lineIndex - (inIndex + 4));
                var lineNumStr = cleanLine.Substring(lineIndex + 6);
                
                if (int.TryParse(lineNumStr, out int lineNumber)) {
                    var position = new LinePosition(lineNumber - 1, 0); 
                    var lineSpan = new LinePositionSpan(position, position);
                    lineLocation = Location.Create(filePath, new TextSpan(0, 0), lineSpan);
                    int bracketIndex = cleanLine.IndexOf('(');
                    if (bracketIndex > 0) {
                        cleanLine = cleanLine.Substring(0, bracketIndex) + "()";
                    }
                } else {
                    lineLocation = methodLocation ?? Location.None;
                }
            } else {
                lineLocation = methodLocation ?? Location.None;
            }
            productionContext.ReportDiagnostic(Diagnostic.Create(traceDescriptor, lineLocation, cleanLine));
        }
    }
}

public readonly record struct DiagnosticData(
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
) {
    internal void ReportDiagnostic(SourceProductionContext productionContext)
    {
        var start       = new LinePosition(StartLine, StartColumn);
        var end         = new LinePosition(EndLine, EndColumn);
        var lineSpan    = new LinePositionSpan(start, end);
        var location    = Location.Create(FilePath, new TextSpan(StartOffset, Length), lineSpan);
        var diagnostic  = Diagnostic.Create(Descriptor, location, MessageArgs);
        productionContext.ReportDiagnostic(diagnostic);
    }   
};
