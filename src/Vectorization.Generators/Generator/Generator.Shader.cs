// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Friflo.Vectorization.Generators;
using Microsoft.CodeAnalysis;


// ReSharper disable once CheckNamespace
namespace Friflo;

public sealed partial class Gen
{
    private static bool GenerateShaderMethod(
        SemanticModel                   semanticModel,
        ImmutableArray<AttributeData>   methodAttributes,
        IMethodSymbol                   methodSymbol,
        GenerateTrigger                 trigger,
        out EmissionResult              emissionResult)
    {
        var shader          = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.ShaderAttribute");
        var vertexShader    = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.VertexShaderAttribute");
        var fragmentShader  = GeneratorUtils.GetAttributeData(methodAttributes, "Friflo.Vectorization.WebGPU.FragmentShaderAttribute");
        
        if (shader == null && vertexShader == null && fragmentShader == null) {
            emissionResult = default;
            return false;
        }
        var diagnostics = new Diagnostics { BlueprintMethod = methodSymbol };
        if (shader != null) {
            if (vertexShader != null || fragmentShader != null) {
                diagnostics.ReportDiagnosticSymbol(Errors.ShaderError, shader.AttributeClass, "[Shader] cannot be combined with [VertexShader] or [FragmentShader]");
                emissionResult = new EmissionResult("", "", diagnostics.List);
                return false;
            }
        }
        emissionResult = new EmissionResult("", "", diagnostics.List);
        return true;
    }
}
