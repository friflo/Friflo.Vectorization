// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


/// <see cref="ClientAction"/> is not implemented by <see cref="GuiSession"/> to avoid exposing its reference. 
internal sealed class GuiClientAction : ClientAction
{
    private readonly    StringBuilder   stringBuilder = new(512, 512);
    private readonly    SmClient        client;
        
    internal GuiClientAction(SmClient client)
    {
        this.client = client;
    }
    
    private StringBuilder StringBuilder()
    {
        stringBuilder.Clear();
        return stringBuilder;
    }
    
    public override bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default)
    {
        var urlStr = url.ToString();

        // Allow both relative and absolute URIs
        if (Uri.TryCreate(urlStr, UriKind.RelativeOrAbsolute, out Uri? uriResult))
        {
            // If it's an absolute URI, strictly enforce http/https
            if (uriResult.IsAbsoluteUri) {
                if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps) {
                    return false; // Block unsafe absolute schemes (e.g., file:, ftp:, javascript:)
                }
            } else  {
                // If it's a relative URI, block potential javascript injection attempts
                if (urlStr.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                    urlStr.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Safe to build and send the command
            var sb = StringBuilder();
            sb.Append("open-url");
            sb.Append('|');
            sb.Append(urlStr); // Pass the original relative or absolute string to the client
            
            if (target.Length > 0) {
                sb.Append('|');
                sb.Append(target);
            }
            
            var command = new SmSendCommand(sb.ToString());
            client.SendCommand(command);
            return true;
        }
        
        return false; // Invalid URI format
    }
    
    public override void SetClearColor(Color32 light, Color32 dark)
    {
        var sb = StringBuilder();
        sb.Append("set-clear-color");
        sb.Append('|');
        sb.Append($"{light.R:x2}{light.G:x2}{light.B:x2}{light.A:x2}");
        sb.Append('|');
        sb.Append($"{dark.R:x2}{dark.G:x2}{dark.B:x2}{dark.A:x2}");
        
        var command = new SmSendCommand(sb.ToString());
        client.SendCommand(command);
    }    
    
    public override void SetDisplayMode(SmDisplayMode mode) {
        var sb = StringBuilder();
        sb.Append("set-display-mode");
        sb.Append('|');
        sb.Append(mode.ToString());
        var command = new SmSendCommand(sb.ToString());
        client.SendCommand(command);
    }
    
    public override  void SetWindowTitle(ReadOnlySpan<char> title)
    {
        var sb = StringBuilder();
        sb.Append("set-title");
        sb.Append('|');
        sb.Append(title);
        var msg = new SmSendCommand(sb.ToString());
        client.SendCommand(msg);
    }
    
    public override void CopyToClipboard(ReadOnlySpan<char> text)
    {
        var sb = StringBuilder();
        sb.Append("copy-to-clipboard");
        sb.Append('|');
        sb.Append(text);
        var msg = new SmSendCommand(sb.ToString());
        client.SendCommand(msg);
    }
}