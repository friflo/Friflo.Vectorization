// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.SmGui;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU;

/// <summary> Defines the contract for an event-driven or frame-callback based renderer. </summary>
/// <remarks>
/// Avoids internal blocking loops (e.g. <c>while(running)</c>) inside the renderer,
/// handing control of the execution flow back to the host environment.<br/>
/// This frame-callback design ensures seamless compatibility across Desktop, Mobile, and WebAssembly (WASM), 
/// where blocking the main thread would freeze the application or terminate the browser context.
/// </remarks>
public interface IWgpuRenderer
{
    // --- WGPU lifecycle
    public void             OnWindowChanged(int width, int height);
    public void             OnFrame        (in RenderTarget target);
    public void             OnShutdown();
    
    // --- SmGui setup
    public SmGuiApp?        CreateGuiApp   (CreateGuiApp createGui) => null;
    public SmGuiBackend?    GuiBackend      => null;
}


public delegate IWgpuRenderer CreateRenderer(WgpuHost wgpuHost);
