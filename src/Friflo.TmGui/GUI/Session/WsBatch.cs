// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace

using System;

namespace Friflo.TmGui.Session;


internal class WsBatch : TmBatch, IClientAction
{
    internal WsBatch(TmGuiBackend backend, int maxVertices, LoopTimer timer) : base(backend, maxVertices, timer)
    {
    }

    protected internal override void InitBatch() { }
    
    internal void DrawCommandList()
    {
        EndBatch();
    }
    
    // --- IClientAction
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target)
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
            
            var command = new TmSendCommand(sb.ToString());
            session?.client.SendCommand(command);
            return true;
        }
        
        return false; // Invalid URI format
    }
    
    public void SetClearColor(Color32 light, Color32 dark)
    {
        var sb = StringBuilder();
        sb.Append("set-clear-color");
        sb.Append('|');
        sb.Append($"{light.R:x2}{light.G:x2}{light.B:x2}{light.A:x2}");
        sb.Append('|');
        sb.Append($"{dark.R:x2}{dark.G:x2}{dark.B:x2}{dark.A:x2}");
        
        var command = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(command);
    }    
    
    public void SetDisplayMode(TmDisplayMode mode) {
        var sb = StringBuilder();
        sb.Append("set-display-mode");
        sb.Append('|');
        sb.Append(mode.ToString());
        var command = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(command);
    }
    
    public void SetWindowTitle(ReadOnlySpan<char> title)
    {
        var sb = StringBuilder();
        sb.Append("set-title");
        sb.Append('|');
        sb.Append(title);
        var msg = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(msg);
    }
    
    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        var sb = StringBuilder();
        sb.Append("copy-to-clipboard");
        sb.Append('|');
        sb.Append(text);
        var msg = new TmSendCommand(sb.ToString());
        session?.client.SendCommand(msg);
    }
}