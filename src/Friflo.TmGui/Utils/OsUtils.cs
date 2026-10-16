// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Friflo.TmGui.Utils;

internal static class OsUtils
{
    internal static bool OpenUrl(ReadOnlySpan<char> url) {
        var urlStr = url.ToString();

        // Validate that it is a valid absolute URI and uses safe protocols (http or https)
        if (Uri.TryCreate(urlStr, UriKind.Absolute, out Uri? uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            try {
                Process.Start(new ProcessStartInfo { FileName = uriResult.AbsoluteUri, UseShellExecute = true });
                return true;
            }
            catch (Exception e) {
                Console.WriteLine($"OpenUrl failed: {e.Message}");
            }

        }
        return false;
        // Handle invalid or dangerous URL (log, throw exception, or ignore)
        // throw new ArgumentException("Invalid or unsafe URL provided.", nameof(url));
    }
}