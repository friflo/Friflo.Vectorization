using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Friflo.Vectorization.Generators;

public class Diagnostics
{
    public required IMethodSymbol                   BlueprintMethod { get; init; }
    public readonly List<DiagnosticData>            list = new();

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
        list.Add(data);   
    }
}