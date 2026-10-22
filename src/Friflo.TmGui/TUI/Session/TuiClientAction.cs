// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Friflo.TmGui.Session;
using Friflo.TmGui.Utils;

namespace Friflo.TmGui.TUI.Session;


/// <see cref="IClientAction"/> is not implemented by <see cref="TuiSession"/> to avoid exposing its reference.
internal sealed class TuiClientAction : IClientAction
{
    private             Color32         backgroundLight = 0x888888ff;
    private             Color32         backgroundDark  = 0x404142ff;
    private readonly    TmClient        client;
    private readonly    StringBuilder   sb;
    
    internal            Color32     Background => WindowSettingsUtils.IsDarkMode ? backgroundDark : backgroundLight;
    
    internal TuiClientAction(TmClient client, StringBuilder sb) {
        this.client = client;
        this.sb     = sb;
    }

    // --- IClientAction
    public void SetClearColor(Color32 light, Color32 dark)
    {
        backgroundLight = light;
        backgroundDark  = dark;
    }
    
    public void SetWindowTitle(ReadOnlySpan<char> title)
    {
        sb.Clear();
        sb.Append("\x1b]0;");
        sb.Append(title);
        sb.Append("\x07");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var msg = new TmSendMessage(bytes);
        client.Send(msg);
    }
}
