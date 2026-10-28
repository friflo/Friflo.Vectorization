using System.Net;
using Friflo.SmGui;
using Friflo.SmGui.Headless;
using Friflo.SmGui.Http;
using Friflo.SmGui.Session;
using Friflo.SmGui.WebRTC;
using TuiTerminal;


//    Console           - local, single user (console)    via: Console
//    ----------------------------------------------------------------------
// Windows / macOS / Linux          dotnet ./Tests-TuiTerminal.dll


//    Browser           - remote, multi user (url)        via: HTTP & WebRTC
//    ----------------------------------------------------------------------
// Chrome, Firefox, Edge, Safari    https://localhost:8080/


//    Terminal Emulator - remote, multi user (host:port)  via: TCP
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

// Create one session loop per App
var loop = new SmSessionLoop(cpuBackend, session => new TestGuiView(appState, session));

// Connect to Signaling Server for WebRTC Browser sessions.  nuget: Friflo.SmGui.WebRTC
loop.UseWebRtcGuiSessions(new Uri("wss://localhost:8081/signaling"), "test-app", "auto");

// HTTP server for Browser sessions.  nuget: Friflo.SmGui.Http
var app = HttpUtils.CreateWebApplication(new IPEndPoint(IPAddress.IPv6Any, 8080));
app.UseWebSocketGuiSessions(loop);
_ = app.StartAsync();

// Acquire stdin/stdout for Console session
loop.UseConsoleTuiSession();

// TCP server for Terminal Emulator sessions.  source: TerminalUtils.cs
loop.UseTerminalTuiSessions(new IPEndPoint(IPAddress.IPv6Any, 9000));

loop.Run(); // Blocks Main-Thread directly
