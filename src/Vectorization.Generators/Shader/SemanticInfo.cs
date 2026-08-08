// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using Microsoft.CodeAnalysis;

// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
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
    
    internal TypeLayout GetTypeLayout(ITypeSymbol typeSymbol)
    {
        var assemblySymbol = typeSymbol.ContainingAssembly;
        if (!assemblyInfos.TryGetValue(assemblySymbol, out var assemblyInfo)) {
            assemblyInfo = CreateAssemblyInfo(assemblySymbol);
            assemblyInfos.Add(assemblySymbol, assemblyInfo);
        }
        if (assemblyInfo != null) {
            var ns = typeSymbol.ContainingNamespace.ToDisplayString();
            assemblyInfo.typeLayouts.TryGetValue((ns, typeSymbol.Name), out var layout);
            return new TypeLayout(layout.Size, layout.PackingSize);
        }
        var attributes   = typeSymbol.GetAttributes();
        var structLayout = attributes.FirstOrDefault(data => data.AttributeClass?.Name == "StructLayoutAttribute");
        if (structLayout == null) {
            return default;
        }
        var namedArguments = structLayout.NamedArguments;
        var size = namedArguments.FirstOrDefault(arg => arg.Key == "Size");
        var pack = namedArguments.FirstOrDefault(arg => arg.Key == "Pack");
        return new TypeLayout(
            size.Value.IsNull ? 0 : (int)size.Value.Value!,
            pack.Value.IsNull ? 0 : (int)pack.Value.Value!);
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
                if (!IsStruct(reader, typeDef)) {
                    continue;
                }
                var name    = reader.GetString(typeDef.Name);
                var ns      = reader.GetString(typeDef.Namespace);
                var layout  = typeDef.GetLayout();
                info.typeLayouts.TryAdd((ns, name), layout);
            }
        }
        return info;
    }
    
    private static bool IsStruct(MetadataReader reader, TypeDefinition typeDef)
    {
        var attrs = typeDef.Attributes;
        if ((attrs & TypeAttributes.Sealed) == 0 || 
            (attrs & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
        {
            return false;
        }

        var baseHandle = typeDef.BaseType;
        if (baseHandle.IsNil) return false;
        
        var comparer = reader.StringComparer;
        switch (baseHandle.Kind)
        {
            case HandleKind.TypeReference: {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)baseHandle);
                return comparer.Equals(typeRef.Name,            "ValueType") && 
                       comparer.Equals(typeRef.Namespace,       "System");
            }
            case HandleKind.TypeDefinition: {
                var baseTypeDef = reader.GetTypeDefinition((TypeDefinitionHandle)baseHandle);
                return comparer.Equals(baseTypeDef.Name,        "ValueType") && 
                       comparer.Equals(baseTypeDef.Namespace,   "System");
            }
        }
        return false;
    }
}