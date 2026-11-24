// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.KNI;

public sealed class KniBatch : SmBatch
{
    internal KniBatch(KniGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    {
        // renderConfigs   = CreateRenderConfigs(targetFormat);
        
        // samplerLinear   = backend.samplerLinear;
        // samplerNearest  = backend.samplerNearest;
    }

    protected override void InitBatch() { }

    /* Omitted WGPU implementation. May not be required.   
    private static RenderConfig[] CreateRenderConfigs(TextureFormat targetFormat)
    { } */
    
    private BasicEffect basicEffect;
    
    private readonly RasterizerState rasterizerState = new() {
        CullMode                = CullMode.None,
        DepthBias               = 0,
        FillMode                = FillMode.Solid,
        MultiSampleAntiAlias    = false,
        ScissorTestEnable       = true,
        SlopeScaleDepthBias     = 0
    };
    
    public void DrawCommandList(GraphicsDevice graphicsDevice, int x, int y, int width, int height)
    {
        EndBatch();
        
        var lastViewport    = graphicsDevice.Viewport;
        var lastScissorBox  = graphicsDevice.ScissorRectangle;
        
        graphicsDevice.BlendFactor          = Color.White;
        graphicsDevice.BlendState           = Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied;
        graphicsDevice.RasterizerState      = rasterizerState;
        graphicsDevice.DepthStencilState    = DepthStencilState.DepthRead;
        
        graphicsDevice.Viewport             = new Viewport(x, y, width, height);
        
        var scissor = new RectVector2(Vector2.Zero, viewport);

        var vertexBuffer    = ((KniVertexBuffer)gpuVertexBuffer).vertexBuffer;
        var indices         = ((KniIndexBuffer) gpuIndexBuffer).indexBuffer;

        graphicsDevice.SetVertexBuffer(vertexBuffer);
        graphicsDevice.Indices = indices;
        
        gpuVertexBuffer.Write(0, Vertices.Length);
        // gpuIndexBuffer. Write(0, indices.IndexCount);
        
        foreach (var cmd in DrawList)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                graphicsDevice.ScissorRectangle = new Rectangle((int)scissor.pos.X, (int)scissor.pos.Y, (int)scissor.size.X, (int)scissor.size.Y);
            }
            var texture2D   = (Texture2D)cmd.texture.native!;
            var vertexView  = cmd.vertexView;
            var indexView   = cmd.indexView;
            // var sampler  = cmd.samplerFilter == SamplerFilter.Linear ? samplerLinear : samplerNearest;   // todo  implement cmd.samplerFilter
            // var config   = renderConfigs[(int)cmd.blendState];                                           // todo  implement cmd.blendState
            
            var projection = cmd.projection;
            ref var kniProjection = ref Unsafe.As<Matrix4x4, Matrix>(ref projection);
            
            var effect = basicEffect  ??= new BasicEffect(graphicsDevice);
            effect.World                = Matrix.Identity;
            effect.View                 = Matrix.Identity;
            effect.Projection           = kniProjection;
            effect.TextureEnabled       = true;
            effect.Texture              = texture2D;
            effect.VertexColorEnabled   = true;
            
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, vertexView.offset, indexView.offset, indexView.length / 3);
                
                /* [Obsolete("Use DrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int startIndex, int primitiveCount) instead. In future versions this method can be removed.")]
                graphicsDevice.DrawIndexedPrimitives( 
                    primitiveType:  PrimitiveType.TriangleList,
                    baseVertex:     vertexView.offset,
                    minVertexIndex: 0,
                    numVertices:    vertexView.length,
                    startIndex:     indexView.offset,
                    primitiveCount: indexView.length / 3); */
            }
        }
        
        graphicsDevice.Viewport         = lastViewport;
        graphicsDevice.ScissorRectangle = lastScissorBox;
    }
}