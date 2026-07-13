using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Friflo.WGSL.Transpiler.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable CheckNamespace
namespace Friflo.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTypesCodeFixProvider)), Shared]
public class AddTypesCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["WGPU004"];

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
                $"Add types from wgsl for: {methodNode.Identifier.Text}()",
                c => InsertTypesAsync(context.Document, methodNode, diagnostic, c),
                equivalenceKey: "GenWgslTypes"),
            diagnostic);
    }

    private static async Task<Document> InsertTypesAsync(
        Document document, MethodDeclarationSyntax methodNode, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (!diagnostic.Properties.TryGetValue("WGSL", out var wgsl) || wgsl == null || wgsl == "") {
            return document;
        }
        var types = TypeGenerator.GenerateCSharpTypes(wgsl);
        types =  "    \n" + types;

        var newTypes = SyntaxFactory.ParseCompilationUnit(types).Members;
        
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        
        // add new types in syntax tree directly after the method
        var newRoot = root!.InsertNodesAfter(methodNode, newTypes);
        return document.WithSyntaxRoot(newRoot);
    }
}

