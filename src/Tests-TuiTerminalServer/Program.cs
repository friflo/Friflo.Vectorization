using System.Net;
using System.Net.Sockets;
using Friflo.TmGui.TUI.Terminal;
using TerminalServer;


// connect terminal client
// plink(PuTTY/Windows)         echo --view logs --theme dark | plink -raw -t -P 9000 127.0.0.1

Console.WriteLine("TUI Terminal Server");

var appState = new AppState(); // shared application state among all clients each having its own IGuiView instance

var port = 9000;
var engine = new SingleThreadedShardEngine((ConnectInfo info) => new TestGuiView(appState));

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