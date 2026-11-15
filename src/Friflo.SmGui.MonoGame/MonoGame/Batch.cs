// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.MonoGame;

public sealed class MonoGameBatch : SmBatch
{
    internal MonoGameBatch(MonoGameGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    {
        // renderConfigs   = CreateRenderConfigs(targetFormat);
        
        // samplerLinear   = backend.samplerLinear;
        // samplerNearest  = backend.samplerNearest;
    }

    protected override void InitBatch() { }

    /*
    // TextureFormat.BGRA8Unorm / RGBA8Unorm
    /// <summary> Create multiple configs. A single would be sufficient, if not supporting <see cref="GpuBlendState"/>. </summary>
    private static RenderConfig[] CreateRenderConfigs(TextureFormat targetFormat)
    {
        var configs = new RenderConfig[6];
        var desc = new GpuRenderPipelineDescriptor();
        desc.VertexState.buffers = [
            new GpuVertexBufferLayout {     // [VertexBuffer(0)]   (slot: 0)
                arrayStride = Unsafe.SizeOf<Vertex2D>(),
                attributes = [
                    new GpuVertexAttribute { shaderLocation = 0, offset =  0, format = VertexFormat.Float32x2 },    // Vertex2D.position 
                    new GpuVertexAttribute { shaderLocation = 1, offset =  8, format = VertexFormat.Float32x2 },    // Vertex2D.uv
                    new GpuVertexAttribute { shaderLocation = 2, offset = 16, format = VertexFormat.Unorm8x4  }     // Vertex2D.color
                ]
        }];
        desc.PrimitiveState = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.None
        };
        for (int index = 0; index < configs.Length; index++) {
            var blendIndex  = (BlendState)index;
            var blend       = GuiEffect.CreateBlendState(blendIndex);
            desc.FragmentState = new GpuFragmentState{ targets = [ new GpuColorTargetState { format = targetFormat, blend  = blend }]};
            configs[index] = desc.CreateConfig($"Batch2D: {blendIndex}"); // does not create any wgpu handle
        }
        return configs;
    } */
    
    private             BasicEffect     effect;
    
    private readonly    RasterizerState rasterizerState = new() {
        CullMode                = CullMode.None,
        DepthBias               = 0,
        FillMode                = FillMode.Solid,
        MultiSampleAntiAlias    = false,
        ScissorTestEnable       = true,
        SlopeScaleDepthBias     = 0
    };
    
    public void DrawCommandList(GraphicsDevice graphicsDevice)
    {
        EndBatch();
        
        var lastViewport    = graphicsDevice.Viewport;
        var lastScissorBox  = graphicsDevice.ScissorRectangle;
        
        graphicsDevice.BlendFactor          = Color.White;
        graphicsDevice.BlendState           = Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied;
        graphicsDevice.RasterizerState      = rasterizerState;
        graphicsDevice.DepthStencilState    = DepthStencilState.DepthRead;
        
        var scissor = new RectVector2(Vector2.Zero, viewport);

        var vertexBuffer    = ((MonoGameVertexBuffer)gpuVertexBuffer).vertexBuffer;
        var indices         = ((MonoGameIndexBuffer) gpuIndexBuffer).indexBuffer;

        graphicsDevice.SetVertexBuffer(vertexBuffer);
        graphicsDevice.Indices = indices;
        
        foreach (var cmd in DrawList)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                graphicsDevice.ScissorRectangle = new Rectangle((int)scissor.pos.X, (int)scissor.pos.Y, (int)scissor.size.X, (int)scissor.size.Y);    
            }
            var texture2D   = (Texture2D)cmd.texture.native!;
            var vertexView  = cmd.vertexView;
            var indexView   = cmd.indexView;
            // var sampler  = cmd.samplerFilter == SamplerFilter.Linear ? samplerLinear : samplerNearest;
            // var config   = renderConfigs[(int)cmd.blendState];
            
            effect                    ??= new BasicEffect(graphicsDevice);
            effect.World                = Matrix.Identity;
            effect.View                 = Matrix.Identity;
            effect.Projection           = cmd.projection;
            effect.TextureEnabled       = true;
            effect.Texture              = texture2D;
            effect.VertexColorEnabled   = true;
            
            /* 
            graphicsDevice.DrawIndexedPrimitives(
                primitiveType:  PrimitiveType.TriangleList,
                baseVertex:     vertexView.offset,
                minVertexIndex: 0,
                numVertices:    vertexView.length,
                startIndex:     (int)drawCmd.IdxOffset + idxOffset,
                primitiveCount: (int)drawCmd.ElemCount / 3); */
        }
        
        graphicsDevice.Viewport         = lastViewport;
        graphicsDevice.ScissorRectangle = lastScissorBox;
    }
}