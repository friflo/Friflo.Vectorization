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

// Important: CodeFixProvider requires its own project
// If integrated in Generator it results in compiler warning: 
//   RS1038: This compiler extension should not be implemented in an assembly containing a reference to Microsoft.CodeAnalysis.Workspaces.
//     The Microsoft.CodeAnalysis.Workspaces assembly is not provided during command line compilation scenarios,
//     so references to it could cause the compiler extension to behave unpredictably.
// Note: A CodeFixProvider only runs in IDEs / language servers
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddParamsCodeFixProvider)), Shared]
public class AddParamsCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("WGPU003");

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
                $"Add parameters to: {methodNode.Identifier.Text}()",
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
        var wgslFiles = WgslUtils.CreateWgslFiles(diagnostic.Properties);
        if (wgslFiles == null) {
            return document;
        }
        if (!diagnostic.Properties.TryGetValue("proj_dir", out var projDir)) {
            return document;
        }
        var module      = CodeFixer.ParseWgslFiles(wgslFiles);
        var mappings    = TypeMappings.LoadTypeMappings($"{projDir}/{TypeMappings.MappingPath}", out _);
        var paramsResult= CodeFixer.CreateShaderParams(module, mappings);
        var newParams   = SyntaxFactory.ParseParameterList(paramsResult.Parameters);
        
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var method = oldParams.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null || root == null) return document;

        var updatedMethod = method
            .WithParameterList(newParams)
            .WithSemicolonToken(method.SemicolonToken.WithTrailingTrivia(
                SyntaxFactory.LineFeed,
                SyntaxFactory.Comment(paramsResult.Comments), 
                SyntaxFactory.CarriageReturnLineFeed));

        return document.WithSyntaxRoot(root.ReplaceNode(method, updatedMethod));
    }
}

