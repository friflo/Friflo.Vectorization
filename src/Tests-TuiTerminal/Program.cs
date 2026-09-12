using System.Net;
using System.Net.Sockets;
using Friflo.TmGui.Client;
using TuiTerminal;


// connect terminal client
// Windows plink(PuTTY)     plink -raw -t -P 9000 127.0.0.1       with args: echo --view logs --theme dark | plink -raw -t -P 9000 127.0.0.1
// Windows WSL              stty raw -echo; nc $(ip route show default | awk '{print $3}') 9000; stty sane
// macOS / Linux            stty raw -echo; nc localhost 9000; stty sane

// Or use an SSH Proxy to redirect SSH client connections to the TmGui TCP server:
// Client (All OS):         ssh tmgui@127.0.0.1
// Server Windows:
//   - Setup:   Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0; Start-Service sshd
//   - Config:  Match User tmgui -> ForceCommand powershell -Command "$s=New-Object System.Net.Sockets.TcpClient('127.0.0.1',9000);$st=$s.GetStream();$i=[Console]::OpenStandardInput();$o=[Console]::OpenStandardOutput();Start-ThreadJob{$i.CopyTo($st)};$st.CopyTo($o)"
// Server Linux / macOS:
//   - Setup:   sudo apt install openssh-server (Linux) / brew install openssh (macOS)
//   - Config:  Match User tmgui -> ForceCommand nc 127.0.0.1 9000


Console.WriteLine("TUI Terminal Server");

var appState = new AppState(); // shared application state among all clients each having its own IGuiView instance

TerminalUtils.EnableRawModeAndVT100();


await TcpServer();

async ValueTask TcpServer()
{
    var port = 9000;
    var engine = new SingleThreadedShardEngine((ConnectInfo info) => new TestGuiView(appState));

    // 2. IMPORTANT: Start the dedicated single-threaded event loop!
    engine.Start();
    
    var localClient = new ConsoleClient();
    _ = ConsoleClient.HandleClientSessionAsync(localClient, engine, CancellationToken.None);
    
    // await Task.Delay(-1);

    

    // 3. Start TCP listener loop
    using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
    serverSocket.Listen();

    Console.WriteLine("[+] Server & ShardEngine running on port {port}...");

    while (true)
    {
        Socket clientSocket = await serverSocket.AcceptAsync();
        
        // Pass engine reference to every client I/O session
        var client = new SocketClient(clientSocket);
        _ = SocketClient.HandleClientSessionAsync(client, engine, CancellationToken.None);
    }
}