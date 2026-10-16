// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Friflo.TmGui.Utils;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public enum TmDisplayMode
{
    Windowed,
    Fullscreen,
}

public readonly ref struct TmAction : IHostAction
{
    private readonly    TmBatch batch;  //  8 bytes 
    
    internal TmAction(TmBatch batch)
    {
        this.batch = batch;
    }
    
    // --- IHostAction
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        return batch.HostAction.OpenUrl(url, target);
    }

    public void SetDisplayMode(TmDisplayMode mode) {
        batch.HostAction.SetDisplayMode(mode);
    }

    public void SetWindowTitle(ReadOnlySpan<char> title) {
        batch.HostAction.SetWindowTitle(title);
    }

    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        batch.HostAction.CopyToClipboard(text);
    }
}

public interface IHostAction
{
    public bool OpenUrl(ReadOnlySpan<char> url, ReadOnlySpan<char> target = default) {
        var urlStr = url.ToString();

        // Validate that it is a valid absolute URI and uses safe protocols (http or https)
        if (Uri.TryCreate(urlStr, UriKind.Absolute, out Uri? uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            Process.Start(new ProcessStartInfo { FileName = uriResult.AbsoluteUri, UseShellExecute = true });
            return true;
        }
        return false;
        // Handle invalid or dangerous URL (log, throw exception, or ignore)
        // throw new ArgumentException("Invalid or unsafe URL provided.", nameof(url));
    }
    
    public void SetDisplayMode(TmDisplayMode mode) { }
    
    public void SetWindowTitle(ReadOnlySpan<char> title) { }
    
    public void CopyToClipboard(ReadOnlySpan<char> text)
    {
        ClipboardUtils.CopyToClipboard(text);
    }
}

