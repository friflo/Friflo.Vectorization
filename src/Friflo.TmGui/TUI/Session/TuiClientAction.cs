// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;
using Friflo.SmGui.Session;
using Friflo.SmGui.Utils;

namespace Friflo.SmGui.TUI.Session;


/// <see cref="ClientAction"/> is not implemented by <see cref="TuiSession"/> to avoid exposing its reference.
internal sealed class TuiClientAction : ClientAction
{
    private             Color32         backgroundLight = 0x888888ff;
    private             Color32         backgroundDark  = 0x404142ff;
    private readonly    SmClient        client;
    private readonly    StringBuilder   sb = new(512, 512);
    
    internal            Color32     Background => WindowSettingsUtils.IsDarkMode ? backgroundDark : backgroundLight;
    
    internal TuiClientAction(SmClient client) {
        this.client = client;
    }

    public override void SetClearColor(Color32 light, Color32 dark)
    {
        backgroundLight = light;
        backgroundDark  = dark;
    }
    
    public override void SetWindowTitle(ReadOnlySpan<char> title)
    {
        sb.Clear();
        sb.Append("\x1b]0;");
        sb.Append(title);
        sb.Append("\x07");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var msg = new SmSendMessage(bytes);
        client.Send(msg);
    }
}
