// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Numerics;
using Friflo.WGPU;
using Shaders.Imdraw;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct Draw2D
{
    internal readonly   Batch2D     batch;  //  8 bytes

    public              Font        DefaultFont => batch.defaultFont;
    
    internal Draw2D(Batch2D batch)
    {
        this.batch  = batch;
    }


#region State / Pipeline
    public ScissorScope PushScissor(Vector2 position, Vector2 size)
    {
        var scissorStack = batch.scissorStack;
        var cur = scissorStack.Count > 0 ? scissorStack.Peek() : new RectVector2(Vector2.Zero, batch.viewport);

        var intersectPos    = Vector2.Max(cur.pos, position);
        var curMax          = cur.pos + cur.size;
        var newMax          = position + size;
        var intersectMax    = Vector2.Min(curMax, newMax);
        var intersectSize   = Vector2.Max(Vector2.Zero, intersectMax - intersectPos);

        var scissor = new RectVector2(intersectPos, intersectSize);
        scissorStack.Push(scissor);

        Flush();
        batch.currentScissor = scissor;
        return new ScissorScope(this);
    }

    public void PopScissor()
    {
        var scissorStack = batch.scissorStack;
        if (scissorStack.Count > 0) {
            scissorStack.Pop();
        }
        var scissor = scissorStack.Count > 0 ? scissorStack.Peek() : new RectVector2(Vector2.Zero, batch.viewport);
        Flush();
        batch.currentScissor = scissor;
    }
    
    public SamplerFilterScope PushSamplerFilter(SamplerFilter samplerFilter)
    {
        batch.samplerFilterStack.Push(batch.currentSamplerFilter);
        ApplySampler(samplerFilter);
        return new SamplerFilterScope(this);
    }
    
    private void ApplySampler(SamplerFilter samplerFilter)
    {
        if (batch.currentSamplerFilter == samplerFilter) return;

        Flush();
        batch.currentSamplerFilter = samplerFilter;
    }
    
    public void PopSamplerFilter()
    {
        var filter = batch.samplerFilterStack.Pop();
        ApplySampler(filter);
    }
    

    public void SetViewport(float width, float height)
    {
        Flush();

        var bat = batch;
        bat.viewport = new Vector2(width, height);
        
        // base projection for window size
        bat.defaultOrtho = Matrix4x4.CreateOrthographicOffCenter(0f, width, height, 0f, -1f, 1f);
        
        // combine with current camera transform
        bat.projection = bat.currentTransform * bat.defaultOrtho;
    }

    public TransformScope PushTransform(in Matrix4x4 transform)
    {
        var transformStack = batch.transformStack;
        var parent    = transformStack.Count > 0 ? transformStack.Peek() : Matrix4x4.Identity;
        var combined  = transform * parent;

        transformStack.Push(combined);
        ApplyTransform(combined);
        return new TransformScope(this);
    }

    public void PopTransform()
    {
        var transformStack = batch.transformStack;
        if (transformStack.Count > 0) {
            transformStack.Pop();
        }
        var targetTransform = transformStack.Count > 0 ? transformStack.Peek() : Matrix4x4.Identity;

        ApplyTransform(targetTransform);
    }

    private void ApplyTransform(in Matrix4x4 transform)
    {
        var bat = batch;
        if (bat.currentTransform == transform) return;

        Flush();

        bat.currentTransform    = transform;
        bat.projection          = bat.currentTransform * bat.defaultOrtho;
    }

    public void SetBlendState(BlendState blendState)
    {
        if (blendState == batch.currentBlendState) return;
        
        Flush();
        batch.currentBlendState = blendState;
    }
    
    public ZIndexScope PushZIndex(int zIndex)
    {
        var bat = batch;
        bat.zIndexStack.Push(bat.currentZIndex);

        Flush();
        bat.currentZIndex = zIndex;
        bat.sortZIndex    = true;
        return new ZIndexScope(this);
    }

    public void PopZIndex()
    {
        var bat = batch;
        if (bat.zIndexStack.Count == 0) return;

        int prevZIndex = bat.zIndexStack.Pop();

        Flush();
        bat.currentZIndex = prevZIndex;
    }

    public void Flush()
    {
        var bat = batch;
        int pendingVertices = bat.vertexCount - bat.vertexStart;
        if (pendingVertices <= 0) {
            return;
        }

        int pendingQuads = pendingVertices / 4;

        var vertexView  = new MemoryView(bat.vertexStart, pendingVertices);
        var indexView   = new MemoryView(0, pendingQuads * 6);
        bat.vertexStart = bat.vertexCount;

        // Batch2D.Draw(pass, config, bat.uniforms, texture, bat.currentSampler, vertexView, indexView);
        
        bat.drawCommands.Add(new DrawCommand(
            zIndex: bat.currentZIndex,
            sequence:        bat.currentSequence++, 
            texture:         bat.currentTexture,
            vertexView:      vertexView,
            indexView:       indexView,
            blendState:      bat.currentBlendState,
            projection:      bat.projection,
            samplerFilter:   bat.currentSamplerFilter,
            scissor:         bat.currentScissor
        ));
    }
    
    
    public void DrawCommandList(in RenderTarget target, in GpuRenderPassDescriptor descriptor)
    {
        Flush();
        if (batch.vertexCount == 0) {
            return;
        }
        descriptor.colorAttachments[0].view = target.View;
        using var pass = target.BeginRenderPass(descriptor);

        var bat = (WgpuBatch)batch;
        // Upload vertexBuffer with a single wgpu call
        bat.gpuVertexBuffer.Write(0, bat.vertexCount);

        var commands = bat.drawCommands;
        var segments = bat.commandSegments;
        segments.Clear();
        if (bat.sortZIndex) {
            SortCommands(commands, segments);
        } else {
            segments.Add(new CmdSegment { index = 0, length = commands.Count });
        }
        var scissor = new RectVector2(Vector2.Zero, bat.viewport);

        var vertexBuffer = ((ImWgpuBuffer<Vertex2D>)bat.gpuVertexBuffer).native;
        var indexBuffer  = ((ImWgpuBuffer<uint>)    bat.gpuIndexBuffer).native;

        
        foreach (var segment in segments)
        {
            for (int n = 0; n < segment.length; n++)
            {
                var cmd = commands[segment.index + n];
                if (!cmd.scissor.Equals(scissor)) {
                    scissor = cmd.scissor;
                    pass.SetScissorRect((int)scissor.pos.X, (int)scissor.pos.Y, (int)scissor.size.X, (int)scissor.size.Y);    
                }
                var texture     = new GpuTextureView(cmd.texture.handle, (GpuTexture)cmd.texture.obj!);
                var vertexView  = vertexBuffer.In(cmd.vertexView.offset, cmd.vertexView.length);
                var indexView   = indexBuffer. In(cmd.indexView.offset,  cmd.indexView.length);
                var sampler     = cmd.samplerFilter == SamplerFilter.Linear ? bat.samplerLinear : bat.samplerNearest;
                var uniforms    = new ImUniforms(cmd.projection);
                var config      = bat.renderConfigs[(int)cmd.blendState];
                WgpuBatch.Draw(pass, config, uniforms, texture, sampler, vertexView, indexView);
            }
        }
    }
    
    private static void SortCommands(List<DrawCommand> commands, List<CmdSegment> segments)
    {
        // commands.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
        
        // Run-Length optimization - of commented Sort() above
        var command_0   = commands[0];
        int zIndex      = command_0.zIndex;
        var segment     = new CmdSegment { zIndex = zIndex, sequence = command_0.sequence, index = 0, length = 1 };
        
        for (int n = 1; n < commands.Count; n++)
        {
            var cmd = commands[n];
            if (zIndex == cmd.zIndex) {
                segment.length++;
                continue;
            }
            segments.Add(segment);
            zIndex              = cmd.zIndex;
            segment.zIndex      = zIndex;
            segment.sequence    = cmd.sequence;
            segment.index       = n;
            segment.length      = 1;
        }
        segments.Add(segment);
        
        segments.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
    }
    
    public Gui BeginGui() => new(this, batch);
#endregion
}

