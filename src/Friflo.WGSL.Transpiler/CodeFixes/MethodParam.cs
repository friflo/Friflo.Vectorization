// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

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
        public readonly BindGroup?  bindGroup;
        public readonly string      attribute;
        public readonly string      type;
        public readonly string      name;
        public readonly string      comment;

        public override string ToString() => name;

        internal MethodParam(WgslBinding binding, string attribute, string type)
        {
            bindGroup       = new BindGroup(binding.Group, binding.Binding);    
            this.attribute  = attribute;
            this.type       = type;
            name            = binding.Name;
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

        int maxAttrLength = 0;

        foreach (var param in parameters) {
            if (param.attribute.Length > maxAttrLength) maxAttrLength = param.attribute.Length;
        }
        int attrTargetWidth = maxAttrLength + 4; 

        for (int n = 0; n < parameters.Count; n++)
        {
            var parameter = parameters[n];
            sb.Append("\n        "); 

            // column 1: [Map(0, 1)]
            if (parameter.bindGroup != null) {
                var bg = parameter.bindGroup.Value;
                sb.Append($"[Map({bg.group},{bg.binding,2})] ");
            } else {
                sb.Append($"            ");
            }
            
            // column 2: WGSL-attribute (e.g. [texture_2d(ST.f32)])
            sb.Append(parameter.attribute);
            
            // dynamic padding
            sb.Append(' ', attrTargetWidth - parameter.attribute.Length);

            // column 3 & 4: C#-Type and Name
            sb.Append(parameter.type);
            sb.Append(' ');
            sb.Append(parameter.name);
            var last = n == parameters.Count - 1;
            sb.Append(last ? ")" : ",");
            if (parameter.comment != null) {
                sb.Append(parameter.comment);
            }
        }
    }
}