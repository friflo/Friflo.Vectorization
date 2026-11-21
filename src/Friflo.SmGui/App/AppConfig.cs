// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Friflo.SmGui.Session;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;


public interface IAppConfig
{
    public  string      WebAppUrl       { get; }
    public  string?     AppId           { get; }
    public  string      WebRtcAppUrl    { get; }
}


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
        WebAppUrl       = "HTTP server not configured";
        WebRtcAppUrl    = "Signaling not configured";
    }
    
    public void SetWebAppUrl(Uri uri) {
        WebAppUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }
    
    public void SetWebRtcUrl(Uri uri, string? appId) {
        AppId           = appId;
        WebRtcAppUrl    = $"{uri}?app={appId}";
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
