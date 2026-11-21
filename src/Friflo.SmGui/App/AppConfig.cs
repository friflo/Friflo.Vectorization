// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.SmGui.Session;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;


public interface IAppConfig
{
    public  string      WebAppUrl       { get; }
    public  string?     AppId           { get; }
    public  string      WebRtcAppUrl    { get; }
}

/// <summary>
/// <see cref="AppConfig"/> provides the configuration for browser clients using an HTTP server or a signaling client.
/// </summary>
/// <remarks>
/// <example>E.g. at startup in <c>Program.cs</c>
/// <code>
///     var config = new AppConfig();
///     
///     // HTTP server.  nuget: Friflo.SmGui.Http
///     var webApp = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
///     webApp.UseWebSocketGuiSessions(config, new Uri("https://localhost:8080"));
///     await webApp.StartAsync();
///     
///     // Signaling client.  nuget: Friflo.SmGui.WebRTC
///     SmSignalingClient.Connect(config, new Uri("https://localhost:8081/?app=test-app"), AppIdType.Auto);
/// </code>
/// </example>
/// </remarks>
public class AppConfig : IAppConfig
{
    public  string      WebAppUrl       { get; private set; }
    public  string?     AppId           { get; private set; }
    public  string      WebRtcAppUrl    { get; private set; }

#region private fields
    private readonly    List<ISessionHandler>   list = [];
    public  readonly    SmSessionLoop?          sessionLoop;
#endregion

    public  override    string                  ToString() => $"count: {list.Count}";
    
    internal static readonly InvalidOperationException SessionLoopException =
        new($"No Config assigned. Assign a Config with: {nameof(AppConfig)}() or {nameof(AppConfig)}.{nameof(AddSessionLoop)}().");

    public AppConfig(SmSessionLoop? sessionLoop = null) {
        this.sessionLoop = sessionLoop;
        sessionLoop?.config = this;
        WebAppUrl       = "error: HTTP server not configured.\nCheck example at: https://github.com/friflo/Friflo.Vectorization/blob/main/src/MonoGame/Program.cs";
        WebRtcAppUrl    = "error: Signaling client not configured.\nCheck example at: https://github.com/friflo/Friflo.Vectorization/blob/main/src/MonoGame/Program.cs";
    }
    
    public void SetWebAppUrl(Uri uri) {
        WebAppUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }
    
    public void SetWebRtcUrl(Uri uri, string? appId) {
        AppId           = appId;
        if (appId == null) {
            WebRtcAppUrl = uri.ToString();
        } else {
            WebRtcAppUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/?app={appId}";    
        }
    }
    
    public void Add(ISessionHandler sessionHandler)
    {
        if (sessionLoop != null) {
            return;
        }
        list.Add(sessionHandler);
    }
    
    public CreateGuiApp AddSessionLoop(CreateGuiApp createGuiApp)
    {
        return CreateSessionApp;

        SmGuiApp CreateSessionApp(SmGuiBackend backend)
        {
            var app = createGuiApp(backend);
            var loop = app.SessionLoop;
            if (loop != null) {
                loop.config = this;
                foreach (var handler in list) {
                    handler.AddSessionLoop(loop); 
                }
            }
            return app;
        }
    }
    
    public void RemoveSessionLoop()
    {
        foreach (var handler in list) {
            handler.RemoveSessionLoop(); 
        }
    }
}

public interface ISessionHandler
{
    /// <summary>
    /// Adds the <see cref="SmGuiApp.SessionLoop"/> to the session handler after the <see cref="SmGuiApp"/> is created.<br/>
    /// To remove a <see cref="SmSessionLoop"/> call <see cref="RemoveSessionLoop"/>
    /// </summary>
    public void AddSessionLoop(SmSessionLoop loop);
    
    /// <summary>
    /// Removes the <see cref="SmSessionLoop"/> added with <see cref="AddSessionLoop"/>
    /// </summary>
    public void RemoveSessionLoop();
}
