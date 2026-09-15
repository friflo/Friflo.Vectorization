using System.Net;
using System.Net.Sockets;
using Friflo.TmGui.Session;
using TuiTerminal;


// connect terminal client
// Windows plink(PuTTY)     plink -raw -t -P 9000 127.0.0.1       with args: echo --view logs --theme dark | plink -raw -t -P 9000 127.0.0.1
// Windows WSL              stty raw -echo; nc $(ip route show default | awk '{print $3}') 9000; stty sane
// macOS / Linux            stty raw -echo; nc localhost 9000; stty sane

// PuTTY - Session                  Host Name: localhost    Port: 9000    Connection type: Other - Telnet
//       - Terminal                 Local echo: Force off   Local line editing: Force off  
//                  > Keyboard      The Function keys and keypad:           VT100+
//                  > Features      Disable application cursor keys mode:   Enabled
//       - Window   > Appearance    Font:                                   Cascadia Code, Regular, 12 px
//                  > Translation   Remote character set:                   UTF-8


// Or use an SSH Proxy to redirect SSH client connections to the TmGui TCP server:
// Client (All OS):         ssh tmgui@127.0.0.1
// Server Windows:
//   - Setup:   Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0; Start-Service sshd
//   - Config:  Match User tmgui -> ForceCommand powershell -Command "$s=New-Object System.Net.Sockets.TcpClient('127.0.0.1',9000);$st=$s.GetStream();$i=[Console]::OpenStandardInput();$o=[Console]::OpenStandardOutput();Start-ThreadJob{$i.CopyTo($st)};$st.CopyTo($o)"
// Server Linux / macOS:
//   - Setup:   sudo apt install openssh-server (Linux) / brew install openssh (macOS)
//   - Config:  Match User tmgui -> ForceCommand nc 127.0.0.1 9000


Console.WriteLine("TUI Terminal Server");

var appState = new AppState();
var loop     = new TmSessionLoop(_ => new TestGuiView(appState));


// Flag toggles execution mode:
// true  => UI Loop runs in dedicated background Thread, Main-Thread runs TCP server.
// false => TCP server runs on ThreadPool, Main-Thread is blocked by UI Loop.
bool runAsync = false;

if (runAsync) {
    loop.StartAsync(); // Spawns dedicated "ShardLoopThread"
    await RunTcpServerAsync(loop, port: 9000);
}
else {
    // Run TCP accept loop in background and lock Main-Thread for engine execution
    _ = Task.Run(() => RunTcpServerAsync(loop, port: 9000));
    
    loop.StartSync(); // Blocks Main-Thread directly
}


static async Task RunTcpServerAsync(TmSessionLoop loop, int port)
{
    // Start local console I/O session
    var localClient = new ConsoleClient();
    _ = ConsoleClient.HandleClientSessionAsync(localClient, loop, CancellationToken.None);

    using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
    serverSocket.Listen();

    Console.WriteLine($"[+] TCP Listener active on port {port}...");

    while (true)
    {
        Socket clientSocket = await serverSocket.AcceptAsync();
        
        var client = new SocketClient(clientSocket);
        _ = SocketClient.HandleClientSessionAsync(client, loop, CancellationToken.None);
    }
}