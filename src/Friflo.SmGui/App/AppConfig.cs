// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Friflo.SmGui.Session;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui;


public class AppConfig
{
    private readonly    List<ISessionHandler>   list = [];
    public  readonly    SmSessionLoop?          sessionLoop;
    public              string?                 AppId { get; private set; }

    public  override    string                  ToString() => $"count: {list.Count}";

    public AppConfig(SmSessionLoop? sessionLoop = null) {
        this.sessionLoop = sessionLoop;
    }
    
    public void SetAppId(string? appId) {
        AppId = appId;
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
                loop.appId = AppId;
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
