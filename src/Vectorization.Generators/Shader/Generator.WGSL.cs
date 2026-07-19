// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo;


public sealed partial class ShaderGen
{
    private static WgslFile CreateWgslFile(AdditionalText text, CancellationToken cancellationToken)
    {
        var content = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
        var path    = text.Path.Replace('\\', '/');
        WgslModule module;
        try {
            module = WgslParser.ParseWgsl(content, path);
        } catch (Exception exception) {
            var type        = exception.GetType();
            var firstLine   = GetFirstStackTraceLine(exception);
            module          = new WgslModule();
            module.Errors.Add($"{type.Namespace}.{type.Name}: {exception.Message} {firstLine}");
        }
        return new WgslFile {
            NormalizedPath  = path,
            Hash            = ComputeFnv1A64(content),
            Content         = content,
            Module          = module
        };
    }
    
    private static string GetFirstStackTraceLine(Exception ex)
    {
        string trace = ex.StackTrace;
        if (string.IsNullOrEmpty(trace)) return string.Empty;

        int end = trace.IndexOf('\n');
        var line = (end != -1 ? trace.Substring(0, end) : trace).Trim();
        return line.Replace('\\', '/');
    }
    
    // High-performance, allocation-free FNV-1a 64-bit string hashing
    private static ulong ComputeFnv1A64(string text)
    {
        ulong hash = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        foreach (char c in text)
        {
            hash ^= (byte)c;        hash *= prime;
            hash ^= (byte)(c >> 8); hash *= prime;
        }
        return hash;
    }
}
