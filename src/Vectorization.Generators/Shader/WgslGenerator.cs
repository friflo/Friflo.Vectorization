// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo;


internal static class WgslGenerator
{
    internal static WgslFile CreateWgslFile(AdditionalText text, CancellationToken cancellationToken)
    {
        var content = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
        var path    = text.Path.Replace('\\', '/');
        WgslModule module;
        string? source = null;
        try {
            module = WgslParser.ParseWgsl(content, path);
            source = TypeEmitter.Emit(module);            
        }
        catch (Exception exception) {
            var type        = exception.GetType();
            var firstLine   = GetStackTraceCaller(exception);
            module          = new WgslModule();
            module.Errors.Add($"{type.Namespace}.{type.Name}: {exception.Message} {firstLine}");
        }
        return new WgslFile {
            NormalizedPath  = path,
            Hash            = ComputeFnv1A64(content),
            Content         = content,
            Module          = module,
            Source          = source
        };
    }
    
    private static string GetStackTraceCaller(Exception ex)
    {
        var trace = ex.StackTrace;
        if (string.IsNullOrEmpty(trace)) return string.Empty;

        int end = trace.IndexOf('\n');
        end = end != -1 ? end : trace.Length;

        int closeParen = trace.IndexOf(')', 0, end);
        
        int length = closeParen != -1 ? closeParen + 1 : end;
        
        return trace.Substring(0, length).Trim();
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
