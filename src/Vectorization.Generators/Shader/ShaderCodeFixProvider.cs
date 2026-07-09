using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable CheckNamespace
namespace Friflo;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ShaderCodeFixProvider)), Shared]
public class ShaderCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["WGPU003"];

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
                $"Add parameters from wgsl for: {methodNode.Identifier.Text}()",
                c => InsertParametersAsync(context.Document, methodNode.ParameterList, diagnostic, c),
                equivalenceKey: "GenWgsl"),
            diagnostic);
    }

    private async Task<Document> InsertParametersAsync(
        Document            document,
        ParameterListSyntax oldParams,
        Diagnostic          diagnostic,
        CancellationToken   cancellationToken)
    {
        if (!diagnostic.Properties.TryGetValue("ShaderParams", out var paramString) || string.IsNullOrEmpty(paramString)) {
            return document;
        }
        var newParams = SyntaxFactory.ParseParameterList(paramString);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        return document.WithSyntaxRoot(root!.ReplaceNode(oldParams, newParams));
    }
}

