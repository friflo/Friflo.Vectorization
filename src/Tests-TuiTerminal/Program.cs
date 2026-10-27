using System.Net;
using System.Net.Sockets;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using Friflo.SmGui.WebRTC;
using TuiTerminal;


//    Console           - local, single user (console)    via: ConsoleClient
//    ----------------------------------------------------------------------
// Windows / macOS / Linux          dotnet ./Tests-TuiTerminal.dll


//    Browser           - remote, multi user (url)        via: KestrelHttpServer
//    --------------------------------------------------------------------------
// Chrome, Firefox, Edge, Safari    https://localhost:8080/


//    Terminal Emulator - remote, multi user (host:port)  via: SocketClient
//    ---------------------------------------------------------------------
//
// Windows plink(PuTTY)     plink -raw -t -P 9000 127.0.0.1       with args: echo --view logs --theme dark | plink -raw -t -P 9000 127.0.0.1
// Windows WSL              stty raw -echo; nc $(ip route show default | awk '{print $3}') 9000; stty sane
// macOS / Linux            stty raw -echo; nc localhost 9000; stty sane

// PuTTY - Session                  Host Name: localhost    Port: 9000    Connection type: Other - Telnet
//       - Terminal                 Local echo: Force off   Local line editing: Force off
//                  > Keyboard      The Function keys and keypad:           VT100+
//                  > Features      Disable application cursor keys mode:   Enabled
//       - Window   > Appearance    Font:                                   Cascadia Code, Regular, 12 px
//                  > Translation   Remote character set:                   UTF-8
//
// Or use an SSH Proxy to redirect SSH client connections to the SmGui TCP server:
// Client (All OS):         ssh tmgui@127.0.0.1
// Server Windows:
//   - Setup:   Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0; Start-Service sshd
//   - Config:  Match User tmgui -> ForceCommand powershell -Command "$s=New-Object System.Net.Sockets.TcpClient('127.0.0.1',9000);$st=$s.GetStream();$i=[Console]::OpenStandardInput();$o=[Console]::OpenStandardOutput();Start-ThreadJob{$i.CopyTo($st)};$st.CopyTo($o)"
// Server Linux / macOS:
//   - Setup:   sudo apt install openssh-server (Linux) / brew install openssh (macOS)
//   - Config:  Match User tmgui -> ForceCommand nc 127.0.0.1 9000


Console.WriteLine("App");

var cpuBackend  = new CpuBackend("App", new DefaultGuiAssets());
var appState    = new AppState(cpuBackend);


var loop = new SmSessionLoop(cpuBackend, client => new TestGuiView(appState, client));

var builder = HttpUtils.CreateHostBuilder(new IPEndPoint(IPAddress.IPv6Any, 8080), loop);
_ = HttpUtils.Start(builder.Build());

var signalListener =  new SmSignalingListener(loop);
signalListener.ConnectAsync(new Uri("wss://localhost:8081/signaling"), "test-app", "auto").Wait(); // stay on main thread
_ = signalListener.RunAsync();


// Run TCP accept loop in background and lock Main-Thread for engine execution
_ = Task.Run(() => RunTcpServerAsync(loop, port: 9000));

loop.Run(); // Blocks Main-Thread directly


static async Task RunTcpServerAsync(SmSessionLoop loop, int port)
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