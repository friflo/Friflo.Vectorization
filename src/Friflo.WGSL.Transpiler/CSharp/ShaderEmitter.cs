using System.Text;

namespace Friflo.WGSL.Transpiler.CSharp;

public static class ShaderEmitter
{
    public static string EmitShader(bool staticMethod, CsMethod method, string hash)
    {
        var signature = new StringBuilder();
        foreach (var parameter in method.Parameters)
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
        
        var code =
$$"""
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
}