// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Friflo.TmGui.Client;


internal static class PosixSignalUtils
{
    private static Action? _exitHandlers;
    

    internal static void RemoveExitHandler(Action exitHandler)
    {
        _exitHandlers -= exitHandler;
    }
    
    internal static void AddExitHandler(Action exitHandler)
    {
        _exitHandlers += exitHandler;
        
        PosixSignalRegistration.Create(PosixSignal.SIGINT, context => 
        {
            context.Cancel = true;
            _exitHandlers?.Invoke();
            Environment.Exit(0);
        });

        PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => {
            _exitHandlers?.Invoke();
        });
    }
}

