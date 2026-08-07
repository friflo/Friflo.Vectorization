// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;
using TypeLayout = System.Reflection.Metadata.TypeLayout;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable UseNullPropagation
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.Shader;


internal class AssemblyInfo
{
    internal readonly Dictionary<(string, string), TypeLayout>  typeLayouts = new();
}

internal class SemanticInfo
{
    internal readonly   Dictionary<ITypeSymbol, CsTypeInfo>         types           = new(SymbolEqualityComparer.Default);
    private  readonly   SemanticModel                               semanticModel;
    private  readonly   Dictionary<IAssemblySymbol, AssemblyInfo?>  assemblyInfos   = new(SymbolEqualityComparer.Default);
    
    internal SemanticInfo(SemanticModel semanticModel)
    {
        this.semanticModel = semanticModel;
    }
    
    internal CsTypeInfo GetTypeInfo(ITypeSymbol typeSymbol)
    {
        if (types.TryGetValue(typeSymbol, out var typeInfo)) {
            return typeInfo;
        }
        var assemblySymbol = typeSymbol.ContainingAssembly;
        if (!assemblyInfos.TryGetValue(assemblySymbol, out var assemblyInfo)) {
            assemblyInfo = CreateAssemblyInfo(assemblySymbol);
            assemblyInfos.Add(assemblySymbol, assemblyInfo);
        }
        if (assemblyInfo == null) {
            return default;
        }
        var ns = typeSymbol.ContainingNamespace.ToDisplayString();
        assemblyInfo.typeLayouts.TryGetValue((ns, typeSymbol.Name), out var layout);
        if (layout.IsDefault) {
            
        }
        return default;
    }
    
    private AssemblyInfo? CreateAssemblyInfo(IAssemblySymbol assemblySymbol)
    {
        var metadataRef = semanticModel.Compilation.GetMetadataReference(assemblySymbol) as PortableExecutableReference;
        if (metadataRef == null) {
            return null;
        }
        using var metadata = metadataRef.GetMetadata();
        if (metadata is not AssemblyMetadata assemblyMetadata) {
            return null;
        }
        var info = new AssemblyInfo();
        var modules = assemblyMetadata.GetModules();
        foreach (ModuleMetadata moduleMetadata in modules)
        {
            var reader = moduleMetadata.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var typeDef = reader.GetTypeDefinition(handle);
                var name    = reader.GetString(typeDef.Name);
                var ns      = reader.GetString(typeDef.Namespace);
                var layout  = typeDef.GetLayout();
                info.typeLayouts.TryAdd((ns, name), layout);
            }
        }
        return info;
    }
}