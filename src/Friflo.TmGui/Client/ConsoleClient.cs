// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Client;

public class ConsoleClient : StreamClient
{
    public ConsoleClient() : base(Console.OpenStandardInput(), Console.OpenStandardOutput())
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
    }
}