using System.Collections.Immutable;
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

// [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddTypesCodeFixProvider)), Shared]
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
        Document document, MethodDeclarationSyntax method, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var wgslFiles = WgslUtils.CreateWgslFiles(diagnostic.Properties, out _);
        if (wgslFiles == null) {
            return document;
        }
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;
        
        var module = CodeFixer.ParseWgslFiles(wgslFiles);
        var result = TypeGenerator.GenerateCSharpTypes(module, null); // pass null. Whole class is obsolete
        if (result.Types == "") {
            // add only comment
            var updatedMethod = method
                .WithSemicolonToken(method.SemicolonToken.WithTrailingTrivia(
                    SyntaxFactory.LineFeed,
                    SyntaxFactory.Comment(result.Comments), 
                    SyntaxFactory.CarriageReturnLineFeed));

            return document.WithSyntaxRoot(root.ReplaceNode(method, updatedMethod));
        }
        // add comment + types
        var text        = "    \n" + result.Comments + result.Types;
        var newTypes    = SyntaxFactory.ParseCompilationUnit(text).Members;
        var newRoot     = root.InsertNodesAfter(method, newTypes);
        return document.WithSyntaxRoot(newRoot);
    }
}

