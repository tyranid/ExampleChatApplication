# SuperFunkyChatCore - (c) James Forshaw 2017
A simple example command line chat application written for .NET to learn network protocol analysis.
You should _NOT_ use this for any real world use, it's entirely designed to learn the basics of
network protocol analysis.

It should work on any platform with .NET Standard support 2.0, so .NET Core 2.0.0 on Windows, Linux and
macOS should be suitable as well as recompiling for .NET framework and Mono.

To use either compile with Visual Studio 2017 with .NET Core support or from the command line do the 
following:

```bash
cd ExampleChatApplication
dotnet restore
dotnet build ChatClient/ChatClient.csproj -f netcoreapp2.0 -c Release
dotnet build ChatServer/ChatServer.csproj -f netcoreapp2.0 -c Release
# Run server in one terminal on default port with TLS support
dotnet exec ChatServer/bin/Release/netcoreapp2.0/ChatServer.dll --c ChatServer/server.pfx
# Run client in another terminal
dotnet exec ChatClient/bin/Release/netcoreapp2.0/ChatClient.dll username 127.0.0.1/ChatClient
```

The build process will also generate .NET executables compatible with .NET frame 4.7.1 and Mono 5.

When running the command line client to send a message just type the message and hit enter. The 
protocol does support multi-line messages but the client doesn't. You can exit the application
cleanly using /quit. For other commands see /help.

NOTE: The **server.pfx** file is in the repository intentionally, it's just a simple self signed
certificate for testing and should have no security risk, as you're not going to use this application
for real.