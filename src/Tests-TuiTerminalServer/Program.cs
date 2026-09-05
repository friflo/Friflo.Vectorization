using System.Net;
using System.Net.Sockets;
using Friflo.TmGui.TUI.Terminal;
using TerminalServer;


Console.WriteLine("TUI Terminal Server");

var port = 9000;
var engine = new SingleThreadedShardEngine((ConnectInfo info) => {
    return new TestRenderer();
});

// 2. IMPORTANT: Start the dedicated single-threaded event loop!
engine.Start();

// 3. Start TCP listener loop
using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
serverSocket.Listen();

Console.WriteLine("[+] Server & ShardEngine running on port {port}...");

while (true)
{
    Socket clientSocket = await serverSocket.AcceptAsync();
    
    // Pass engine reference to every client I/O session
    _ = SingleThreadedShardEngine.HandleClientSessionAsync(clientSocket, engine, CancellationToken.None);
}