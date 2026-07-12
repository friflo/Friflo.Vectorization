// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;


namespace Friflo.WGSL.Transpiler.CodeFixes;

public static partial class CodeFixer
{
    private readonly struct BindGroup
    {
        public readonly int     group;
        public readonly int     binding;
        public readonly string  attribute;
        public readonly string  type;
        public readonly string  parameter;

        public override string ToString() => parameter;

        internal BindGroup(WgslBinding binding, string attribute, string type)
        {
            group           = binding.Group;
            this.binding    = binding.Binding;
            this.attribute  = attribute;
            this.type       = type;
            parameter       = binding.Name;
        }
    }
    
    private static void AppendParameters(StringBuilder sb, List<BindGroup> bindGroups)
    {
        foreach (var bindGroup in bindGroups) {
            sb.Append($"        [Map({bindGroup.group}, {bindGroup.binding})] {bindGroup.attribute}         {bindGroup.type} {bindGroup.parameter},\n");
        }
    }
}