// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public readonly struct GpuHandle
{
    public  long    Active  { get; }
    public  long    Diff    { get; }

    public override string ToString() => $"{Active}  {Diff,5:+0;-0;0}";
    
    public GpuHandle(long active) {
        Active  = active;
    }
    
    public GpuHandle(in GpuHandle start, in GpuHandle cur) {
        Active  = start.Active;
        Diff    = cur.Active - start.Active;
    }
}


public readonly struct GpuHandleDiff
{
    public  GpuHandle   Adapters            { get; init; }
    public  GpuHandle   Devices             { get; init; }
    public  GpuHandle   Queues              { get; init; }
    public  GpuHandle   PipelineLayouts     { get; init; }
    public  GpuHandle   ShaderModules       { get; init; }
    public  GpuHandle   BindGroupLayouts    { get; init; }
    public  GpuHandle   BindGroups          { get; init; }
    public  GpuHandle   CommandBuffers      { get; init; }
    public  GpuHandle   RenderBundles       { get; init; }
    public  GpuHandle   RenderPipelines     { get; init; }
    public  GpuHandle   ComputePipelines    { get; init; }
    public  GpuHandle   PipelineCaches      { get; init; }
    public  GpuHandle   QuerySets           { get; init; }
    public  GpuHandle   Buffers             { get; init; }
    public  GpuHandle   Textures            { get; init; }
    public  GpuHandle   TextureViews        { get; init; }
    public  GpuHandle   Samplers            { get; init; }

    
    
    public GpuHandleDiff GetHandleDiff(in GpuHandleDiff cur)
    {
        return new GpuHandleDiff {
            Adapters            = new GpuHandle(Adapters,           cur.Adapters),
            Devices             = new GpuHandle(Devices,            cur.Devices),
            Queues              = new GpuHandle(Queues,             cur.Queues),
            PipelineLayouts     = new GpuHandle(PipelineLayouts,    cur.PipelineLayouts),
            ShaderModules       = new GpuHandle(ShaderModules,      cur.ShaderModules),
            BindGroupLayouts    = new GpuHandle(BindGroupLayouts,   cur.BindGroupLayouts),
            BindGroups          = new GpuHandle(BindGroups,         cur.BindGroups),
            CommandBuffers      = new GpuHandle(CommandBuffers,     cur.CommandBuffers),
            RenderBundles       = new GpuHandle(RenderBundles,      cur.RenderBundles),
            RenderPipelines     = new GpuHandle(RenderPipelines,    cur.RenderPipelines),
            ComputePipelines    = new GpuHandle(ComputePipelines,   cur.ComputePipelines),
            PipelineCaches      = new GpuHandle(PipelineCaches,     cur.PipelineCaches),
            QuerySets           = new GpuHandle(QuerySets,          cur.QuerySets),
            Buffers             = new GpuHandle(Buffers,            cur.Buffers),
            Textures            = new GpuHandle(Textures,           cur.Textures),
            TextureViews        = new GpuHandle(TextureViews,       cur.TextureViews),
            Samplers            = new GpuHandle(Samplers,           cur.Samplers),
        };
    }
    
    public bool IsDiffZero(int expectedCommandBuffers)
    {
        return (
        //      Adapters.           Diff                          == 0 &&       // TODO
                Devices.            Diff                          == 0 &&
        //      Queues.             Diff                          == 0 &&       // TODO
                PipelineLayouts.    Diff                          == 0 &&
                ShaderModules.      Diff                          == 0 &&
                BindGroupLayouts.   Diff                          == 0 &&
                BindGroups.         Diff                          == 0 &&
                CommandBuffers.     Diff - expectedCommandBuffers == 0 &&
                RenderBundles.      Diff                          == 0 &&
                RenderPipelines.    Diff                          == 0 &&
                ComputePipelines.   Diff                          == 0 &&
                PipelineCaches.     Diff                          == 0 &&
                QuerySets.          Diff                          == 0 &&
                Buffers.            Diff                          == 0 &&
                Textures.           Diff                          == 0 &&
                TextureViews.       Diff                          == 0 &&
                Samplers.           Diff                          == 0);
    }

    public override string ToString() {
        return $"Devices: {Devices.Active} {Devices.Diff,1:+0;-0;0}  Buffers: {Buffers.Active} {Buffers.Diff,1:+0;-0;0}";
    }

    public string GetState(string title = "")
    {
        return $@"{title}
ResourceType    Start Delta
--------------- ----- -----
Adapters         {Adapters          .Active,4} {Adapters          .Diff,5:+0;-0;0}
Devices          {Devices           .Active,4} {Devices           .Diff,5:+0;-0;0}
Queues           {Queues            .Active,4} {Queues            .Diff,5:+0;-0;0}
ComputePipelines {ComputePipelines  .Active,4} {ComputePipelines  .Diff,5:+0;-0;0}
ShaderModules    {ShaderModules     .Active,4} {ShaderModules     .Diff,5:+0;-0;0}
BindGroupLayouts {BindGroupLayouts  .Active,4} {BindGroupLayouts  .Diff,5:+0;-0;0}
BindGroups       {BindGroups        .Active,4} {BindGroups        .Diff,5:+0;-0;0}
CommandBuffers   {CommandBuffers    .Active,4} {CommandBuffers    .Diff,5:+0;-0;0}
RenderBundles    {RenderBundles     .Active,4} {RenderBundles     .Diff,5:+0;-0;0}
RenderPipelines  {RenderPipelines   .Active,4} {RenderPipelines   .Diff,5:+0;-0;0}
PipelineLayouts  {PipelineLayouts   .Active,4} {PipelineLayouts   .Diff,5:+0;-0;0}
PipelineCaches   {PipelineCaches    .Active,4} {PipelineCaches    .Diff,5:+0;-0;0}
QuerySets        {QuerySets         .Active,4} {QuerySets         .Diff,5:+0;-0;0}
Buffers          {Buffers           .Active,4} {Buffers           .Diff,5:+0;-0;0}
Textures         {Textures          .Active,4} {Textures          .Diff,5:+0;-0;0}
TextureViews     {TextureViews      .Active,4} {TextureViews      .Diff,5:+0;-0;0}
Samplers         {Samplers          .Active,4} {Samplers          .Diff,5:+0;-0;0}
";
    }
}
