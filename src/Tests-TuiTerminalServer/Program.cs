

using Friflo.ImGui2D.Terminal;

Console.WriteLine("TUI Terminal Server");

var port = 9000;
var server = new TuiTerminalServer(port); 

// bash >    stty raw -echo; winpty ncat localhost 9000; stty sane


// Run the server listening loop on the ThreadPool in the background
Task serverTask = Task.Run(async () => await server.StartAsync());

Console.WriteLine($"TUI Terminal Server is running on port {port}.");
Console.WriteLine($"Connect using:   ncat -C -v localhost {port}");
Console.WriteLine("Press ENTER to stop the server...");

Console.ReadLine();

// Graceful shutdown
server.Stop();

// Wait for the server listening task to wrap up cleanly
await serverTask;
Console.WriteLine("Server stopped.");