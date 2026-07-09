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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingParametersCodeFixProvider)), Shared]
public class MissingParametersCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["WGPU003"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // 💡 ÄNDERUNG: Nutzt 'FindToken' statt 'FindNode' und wandert dann hoch zur Methode.
        // Das findet die Methode verlässlich, selbst wenn das Diagnostic nur auf dem Namen liegt!
        var token = root.FindToken(diagnosticSpan.Start);
        var methodNode = token.Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        
        if (methodNode == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Generate parameters from wgsl",
                c => InsertParametersAsync(context.Document, methodNode.ParameterList, c),
                "GenWgsl"),
            diagnostic);
    }

    private async Task<Document> InsertParametersAsync(Document document, ParameterListSyntax oldParams, CancellationToken cancellationToken)
    {
        // 1. HIER würdest du normalerweise deine WGSL-Datei parsen.
        // Für diesen simplen Beispiel-Code tun wir so, als hätten wir zwei Parameter gefunden:
        var newParamsList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
            {
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("buffer"))
                    .WithType(SyntaxFactory.ParseTypeName("GpuBuffer<float>")),
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("texture"))
                    .WithType(SyntaxFactory.ParseTypeName("GpuTextureView"))
            }));

        // 2. Ersetze die alte leere Parameterliste mit den neuen Parametern
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var newRoot = root?.ReplaceNode(oldParams, newParamsList);

        // 3. Gib das modifizierte Dokument zurück. Die IDE updatet den Quellcode sofort!
        return document.WithSyntaxRoot(newRoot!);
    }
}

