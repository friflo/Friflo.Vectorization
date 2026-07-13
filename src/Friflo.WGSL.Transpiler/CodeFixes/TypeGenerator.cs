// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public static class TypeGenerator
{
    public static string GenerateCSharpTypes(string wgsl)
    {
        return "\n    struct MyStruct { int value; }";
    }
    
}