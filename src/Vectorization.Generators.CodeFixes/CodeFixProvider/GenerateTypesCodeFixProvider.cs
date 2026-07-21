using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable CheckNamespace
namespace Friflo.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GenerateTypesCodeFixProvider)), Shared]
public class GenerateTypesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["WGPU007"];

    public override FixAllProvider? GetFixAllProvider() => null; // null -> fix only specific method - was: WellKnownFixAllProviders.BatchFixer; 

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var token = root.FindToken(diagnosticSpan.Start);
        var methodNode = token.Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        
        if (methodNode == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Generate types for: {methodNode.Identifier.Text}()",
                c => InsertTypesAsync(context.Document, methodNode, diagnostic, c),
                equivalenceKey: "GenC#lTypes"),
            diagnostic);
    }

    private static async Task<Document> InsertTypesAsync(
        Document document, MethodDeclarationSyntax method, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var wgslFiles = WgslUtils.CreateWgslFiles(diagnostic.Properties, out _);
        if (wgslFiles == null) {
            return document;
        }
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;
        
        var typeEmitter = new TypeEmitter();
        typeEmitter.EmitAllStructs(wgslFiles, "");

        return document;
    }
}

