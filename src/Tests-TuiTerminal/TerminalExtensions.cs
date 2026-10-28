using System.Net;
using System.Net.Sockets;
using Friflo.SmGui.Session;

// ReSharper disable CheckNamespace
internal static class TerminalExtensions
{
    internal static SmSessionLoop UseTerminalTuiSessions(this SmSessionLoop loop, IPEndPoint endPoint)
    {
        _ = Task.Run(() => RunTcpServerAsync(loop, endPoint));
        
        return loop;
    }
    
    private static async Task RunTcpServerAsync(SmSessionLoop loop, IPEndPoint endPoint)
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(endPoint);
        serverSocket.Listen();

        while (true) {
            var clientSocket = await serverSocket.AcceptAsync();
            var client = new SocketClient(clientSocket);
            _ = client.HandleTerminalIOAsync(loop);
        }
    }
}