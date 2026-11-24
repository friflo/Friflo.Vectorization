// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using OpenTK.Graphics.OpenGL4;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.OpenTK;


internal static class OpenTkShader
{
    internal static int CreateShaderProgram()
    {
        const string vertexSource = 
            """
            #version 330 core
            layout (location = 0) in vec2 aPos;
            layout (location = 1) in vec2 aUv;
            layout (location = 2) in vec4 aColor;

            uniform mat4 uProjection;

            out vec2 vUv;
            out vec4 vColor;

            void main()
            {
                vUv = aUv;
                vColor = aColor;
                gl_Position = uProjection * vec4(aPos, 0.0, 1.0);
            }
            """;

        const string fragmentSource =
            """
            #version 330 core
            in vec2 vUv;
            in vec4 vColor;
  
            uniform sampler2D uTexture;
  
            out vec4 FragColor;
  
            void main()
            {
                FragColor = texture(uTexture, vUv) * vColor;
            }
            """;

        int vs = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vs, vertexSource);
        GL.CompileShader(vs);

        int fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, fragmentSource);
        GL.CompileShader(fs);

        int program = GL.CreateProgram();
        GL.AttachShader(program, vs);
        GL.AttachShader(program, fs);
        GL.LinkProgram(program);

        GL.DetachShader(program, vs);
        GL.DetachShader(program, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        return program;
    }
}