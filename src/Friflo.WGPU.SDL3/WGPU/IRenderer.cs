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
public interface IRenderer
{
    public void             OnWindowChanged(int width, int height);
    public void             OnFrame        (in RenderTarget target);
    public void             OnShutdown();
    public SmGuiBackend?    GuiBackend      => null;
}
