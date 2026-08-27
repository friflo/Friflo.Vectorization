// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public readonly ref partial struct ImDraw
{
    internal readonly   ImBatch     batch;  //  8 bytes

    public              ImFont      DefaultFont => batch.defaultFont;
    
    internal ImDraw(ImBatch batch)
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

        batch.Flush();
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
        batch.Flush();
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

        batch.Flush();
        batch.currentSamplerFilter = samplerFilter;
    }
    
    public void PopSamplerFilter()
    {
        var filter = batch.samplerFilterStack.Pop();
        ApplySampler(filter);
    }
    

    public void SetViewport(float width, float height)
    {
        batch.Flush();

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

        bat.Flush();

        bat.currentTransform    = transform;
        bat.projection          = bat.currentTransform * bat.defaultOrtho;
    }

    public void SetBlendState(BlendState blendState)
    {
        if (blendState == batch.currentBlendState) return;
        
        batch.Flush();
        batch.currentBlendState = blendState;
    }
    
    public ZIndexScope PushZIndex(int zIndex)
    {
        var bat = batch;
        bat.zIndexStack.Push(bat.currentZIndex);

        bat.Flush();
        bat.currentZIndex = zIndex;
        bat.sortZIndex    = true;
        return new ZIndexScope(this);
    }

    public void PopZIndex()
    {
        var bat = batch;
        if (bat.zIndexStack.Count == 0) return;

        int prevZIndex = bat.zIndexStack.Pop();

        bat.Flush();
        bat.currentZIndex = prevZIndex;
    }

    public Gui BeginGui() => new(this, batch);
#endregion
}

