// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Tests.WGSL;

// ReSharper disable InconsistentNaming
namespace Shader;

public static class VerifyShaderUtils
{
    private class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public  override string         Path { get; } = path;
        public  readonly SourceText     Text = SourceText.From(content);

        public override SourceText GetText(CancellationToken cancellationToken = default) => Text;
    }
    
    public static ImmutableArray<AdditionalText> LoadAdditionalFilesRecursive(string srcFolder)
    {
        var files = TestWgslUtils.LoadAdditionalFilesRecursive(srcFolder);
        var builder = ImmutableArray.CreateBuilder<AdditionalText>();

        foreach (var file in files) {
            builder.Add(new InMemoryAdditionalText(file.NormalizedPath, file.Content));
        }
        return builder.ToImmutable();
    }
    
    public static Compilation CreateCompilation(string code)
    {
        // Setup (Helper method suggested for readability)
        var compilation = Tests.Generators.VerifyUtils.CreateCompilation(code);
        
        // Ignore Diagnostics
        var options = compilation.Options.WithSpecificDiagnosticOptions(
            new Dictionary<string, ReportDiagnostic> {
                { "WGPU003", ReportDiagnostic.Suppress },
                { "WGPU004", ReportDiagnostic.Suppress },
                { "WGPU007", ReportDiagnostic.Suppress }
            });
        return compilation.WithOptions(options);
    }
}