using System.Net;
using System.Net.Sockets;
using Friflo.SmGui.Session;

// ReSharper disable CheckNamespace
internal static class TerminalExtensions
{
    internal static void UseTerminalTuiSessions(this SmSessionLoop loop) {
        _ = Task.Run(() => RunTcpServerAsync(loop));
    }
    
    private static async Task RunTcpServerAsync(SmSessionLoop loop)
    {
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, 9000));
        serverSocket.Listen();

        while (true) {
            var clientSocket = await serverSocket.AcceptAsync();
            var client = new SocketClient(clientSocket);
            _ = client.HandleTerminalIOAsync(loop);
        }
    }
}