// See https://aka.ms/new-console-template for more information

using Friflo.ImGui2D.Terminal;

Console.WriteLine("TUI Terminal Server");


var server = new TuiTerminalServer(9000); 


// Run the server listening loop on the ThreadPool in the background
Task serverTask = Task.Run(async () => await server.StartAsync());

Console.WriteLine("TUI Terminal Server is running on port 9000.");
Console.WriteLine("Connect using: ncat localhost 9000");
Console.WriteLine("Press ENTER to stop the server...");

Console.ReadLine();

// Graceful shutdown
server.Stop();

// Wait for the server listening task to wrap up cleanly
await serverTask;
Console.WriteLine("Server stopped.");