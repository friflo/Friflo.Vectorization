// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

namespace Friflo.WGSL.Transpiler.WGSL;

public readonly struct WgslFile : IEquatable<WgslFile>
{
    public required     string          NormalizedPath  { get; init; }
    public required     ulong           Hash            { get; init; }
    public required     string          Content         { get; init; }
    public required     WgslModule?     Module          { get; init; }


    public override     int             GetHashCode() => (int)Hash;

    public override     bool            Equals(object? other) => other is WgslFile that && Equals(that);

    public bool Equals(WgslFile other) {
        return Hash == other.Hash;
    }
    
    public static void Sort(WgslFile[] files) => FileEntry.Sort(files);

    public override     string      ToString()      => NormalizedPath;
}


internal struct FileEntry : IComparable<FileEntry>
{
    private     string[]    path;       // priority 1 small Length,   priority 3  element Alphabetical
    private     bool        isCommon;   // priority 2 (true)
    private     WgslFile    file;

    public override string  ToString() => file.NormalizedPath;

    public int CompareTo(FileEntry other)
    {
        int cmp = path.Length.CompareTo(other.path.Length);
        if (cmp != 0) return cmp;

        cmp = other.isCommon.CompareTo(isCommon);
        if (cmp != 0) return cmp;

        for (int i = 0; i < path.Length; i++) {
            cmp = string.Compare(path[i], other.path[i], StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
    
    internal static void Sort(WgslFile[] wgslFiles)
    {
        var entries = new FileEntry[wgslFiles.Length];
        for (int n = 0; n < wgslFiles.Length; n++) {
            var file = wgslFiles[n];
            var path = file.NormalizedPath.Split('/');
            entries[n] = new FileEntry {
                path        = path,
                isCommon    = HasSharedFolder(path),
                file        = file 
            };
        }
        Array.Sort(entries);
        
        for (int n = 0; n < entries.Length; n++) {
            wgslFiles[n] = entries[n].file;
        }
    }
    
    private static bool HasSharedFolder(string[] path)
    {
        for (int n = 0; n < path.Length - 1; n++) {
            var folder = path[n];
            if (folder.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                folder.Equals("shared", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
