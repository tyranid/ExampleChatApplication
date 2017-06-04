# ExampleChatApplication
(c) James Forshaw 2017
A simple example command line chat application written for .NET Core to learn network protocol analysis.
You should _NOT_ use this for any real world use, it's entirely designed to learn the basics of
network protocol analysis.

It should work on any platform with .NET Standard support 1.6, so .NET Core 1.0.4 on Windows, Linux and
macOS should be suitable as well as recompiling for .NET framework and Mono.

To use either compile with Visual Studio 2017 with .NET Core support or from the command line do the 
following:

cd ExampleChatApplication
dotnet restore
dotnet build
\# Run server in one terminal on default port with TLS support
dotnet exec ChatServer/bin/Debug/netcoreapp1.1/ChatServer.dll --c ChatServer/server.pfx
\# Run client in another terminal
dotnet exec ChatClient/bin/Debug/netcoreapp1.1/ChatClient.dll username 127.0.0.1/ChatClient

When running the command line client to send a message just type the message and hit enter. The 
protocol does support multi-line messages but the client doesn't. You can exit the application
cleanly using /quit. For other commands see /help.

NOTE: The server.pfx file is in the repository intentionally, it's just a simple self signed
certificate for testing and should have no security risk, as you're not going to use this application
for real.