# ExampleChatApplication
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
\# Run server in one terminal.
dotnet exec ChatServer/bin/Debug/netcoreapp1.1/ChatServer.dll
\# Run client in another terminal
dotnet exec ChatClient/bin/Debug/netcoreapp1.1/ChatClient.dll username 127.0.0.1/ChatClient

