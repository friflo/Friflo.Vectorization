// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


internal interface IExitHandler
{
    void RestoreTerminal();
}

/// <summary>
/// Handle CTRL + C in terminal
/// </summary>
internal class PosixSignalHandler
{
    private IExitHandler?               exitHandler;
    private PosixSignalRegistration?    sigInt;
    private PosixSignalRegistration?    sigTerm;
    
    internal void RemoveExitHandler()
    {
        exitHandler = null;
        sigInt?.Dispose();
        sigInt = null;
        sigTerm?.Dispose();
        sigTerm = null;
    }
    
    internal void AddExitHandler(IExitHandler handler)
    {
        exitHandler = handler;
        
        sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context => 
        {
            // called in terminal on:  CTRL + C
            context.Cancel = true;
            exitHandler?.RestoreTerminal();
            Environment.Exit(0);
        });

        sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => {
            exitHandler?.RestoreTerminal();
        });
    }
}

