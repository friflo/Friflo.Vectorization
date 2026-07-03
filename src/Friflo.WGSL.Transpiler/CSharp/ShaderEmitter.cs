using System;
using System.Linq;
using System.Text;

namespace Friflo.WGSL.Transpiler.CSharp;

public static class ShaderEmitter
{
    public static string EmitShader(bool staticMethod, CsMethod method, string hash)
    {
        // filter / sort parameters use to create bind group layouts & bind groups
        var layouts = method.Parameters.Where(p => p.HasBindGroup).ToArray();
        Array.Sort(layouts,  (x, y) => {
            int result = x.GroupIndex.CompareTo(y.GroupIndex);
            if (result == 0) {
                result = x.BindingIndex.CompareTo(y.BindingIndex);
            }
            return result;
        });
        
        var signature = GetSignature(method.Parameters);
        
        var code =
$$"""
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace {{method.DeclaringType.Identifier.Namespace}};

public partial class {{method.DeclaringType.Identifier.Name}}
{
    public {{(staticMethod ? "static " : "")}}partial void {{method.Name}}(
{{signature}})
    {
        // hello shader
    }
}
""";
        return code;
    }
    
    private static StringBuilder GetSignature(CsParameter[] parameters)
    {
        var signature = new StringBuilder();
        foreach (var parameter in parameters)
        {
            signature.Append("        ");        
            signature.Append(parameter.Type.Identifier.Name);
            var generics = parameter.Type.Generics;
            if (generics.Count > 0) {
                signature.Append("<");
                foreach (var generic in generics) {
                    signature.Append(generic.Name);
                    signature.Append(", ");
                }
                signature.Length -= 2;
                signature.Append(">");
            }
            signature.Append(" ");
            signature.Append(parameter.Name);
            signature.Append(",\n");
        }
        signature.Length -= 2;
        return signature;
    }
}
