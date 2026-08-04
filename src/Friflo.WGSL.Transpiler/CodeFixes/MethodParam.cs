// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using Friflo.WGSL.Transpiler.WGSL;

// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public static partial class CodeFixer
{
    internal readonly struct BindGroup(int group, int binding)
    {
        public readonly int group   = group;
        public readonly int binding = binding;
    }
    
    private readonly struct MethodParam
    {
        public readonly BindGroup?  bindGroup; // always 11 char + space or 12 spaces if null
        public readonly string      attribute;
        public readonly string      type;
        public readonly string      name;
        public readonly string?     comment;

        public override string ToString() => name;

        internal MethodParam(WgslBinding binding, string attribute, string type, string? comment = null)
        {
            bindGroup       = new BindGroup(binding.Group, binding.Binding);    
            this.attribute  = attribute;
            this.type       = type;
            name            = binding.Name;
            this.comment    = comment;
        }
        
        internal MethodParam(string attribute, string type, string name, string comment)
        {
            this.attribute  = attribute;
            this.type       = type;
            this.name       = name;
            this.comment    = comment;
        }
    }
    
    private static void AppendParameters(StringBuilder sb, List<MethodParam> parameters)
    {
        if (parameters.Count == 0) return;

        const int attrStartColumn = 20;

        int maxAttrLength = 0;
        int maxTypeLength = 0;

        foreach (var param in parameters) {
            if (param.attribute.Length > maxAttrLength) maxAttrLength = param.attribute.Length;
            if (param.type.Length      > maxTypeLength) maxTypeLength = param.type.Length;
        }

        int absoluteTypeStart   = attrStartColumn + maxAttrLength + 1;
        int typeGlobalTabStop   = ((absoluteTypeStart + 4) / 4) * 4;
        int attrTargetWidth     = typeGlobalTabStop - attrStartColumn;
        int typeTargetWidth     = ((maxTypeLength     + 4) / 4) * 4; 

        for (int n = 0; n < parameters.Count; n++)
        {
            var parameter = parameters[n];
            sb.Append("\n        ");

            if (parameter.bindGroup != null) {
                var bg = parameter.bindGroup.Value;
                sb.Append($"[Map({bg.group},{bg.binding,2})] ");
            } else {
                sb.Append("            "); 
            }
            
            // column 2: WGSL-attribute
            sb.Append(parameter.attribute);
            sb.Append(' ', attrTargetWidth - parameter.attribute.Length);

            // column 3: C#-Type
            sb.Append(parameter.type);
            sb.Append(' ', typeTargetWidth - parameter.type.Length);

            // column 4: parameter name
            sb.Append(parameter.name);
            
            var last = n == parameters.Count - 1;
            sb.Append(last ? ")" : ",");
        }
    }
}