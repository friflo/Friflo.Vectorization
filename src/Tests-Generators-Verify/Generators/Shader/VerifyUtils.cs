// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
// ReSharper disable InconsistentNaming

public static class VerifyShaderUtils
{
    private class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public  override string         Path { get; } = path;
        public  readonly SourceText     Text = SourceText.From(content);

        public override SourceText GetText(CancellationToken cancellationToken = default) => Text;
    }
    
    public static ImmutableArray<AdditionalText> LoadAdditionalFilesRecursive(string srcFolder, string baseFolder)
    {
        if (Environment.CurrentDirectory.EndsWith("/linux-x64")) {
            srcFolder = "../" + srcFolder; // use a specific bin folder on GitHub.  See: https://github.com/friflo/Friflo.Vectorization/blob/main/.github/workflows/generators-ci.yml#L55
        }
        var searchPath  = Path.GetFullPath(srcFolder);
        if (!Directory.Exists(searchPath)) {
            throw new InvalidOperationException($"folder not found: searchPath: {searchPath}  CurrentDirectory: {Environment.CurrentDirectory}");
        } 
        var fullBaseDir = searchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var builder     = ImmutableArray.CreateBuilder<AdditionalText>();

        // iterate recursive all *.wgsl files
        foreach (var fullFilePath in Directory.EnumerateFiles(fullBaseDir, "*.wgsl", SearchOption.AllDirectories))
        {
            var relativePath = baseFolder + Path.GetRelativePath(fullBaseDir, fullFilePath);
            var content = File.ReadAllText(fullFilePath);
            builder.Add(new InMemoryAdditionalText(relativePath, content));
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
                { "WGPU004", ReportDiagnostic.Suppress }
            });
        return compilation.WithOptions(options);
    }
}