//    ExampleChatApplication - Example Binary Network Application
//    Copyright (C) 2017 James Forshaw
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.

using ChatProtocol;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ChatClient
{
    class Program
    {
        static bool _color_enable;

        static void SetForegroundColor(ConsoleColor color)
        {
            if (_color_enable)
            {
                Console.ForegroundColor = color;
            }
        }

        static void WriteLineWithColor(string text1, ConsoleColor color1, 
            string text2, ConsoleColor color2)
        {
            ConsoleColor old_foreground = Console.ForegroundColor;
            try
            {
                SetForegroundColor(color1);
                Console.Write(text1);
                SetForegroundColor(color2);
                Console.WriteLine(": {0}", text2);
            }
            finally
            {
                SetForegroundColor(old_foreground);
            }
        }

        static void WriteLineWithColor(string text, ConsoleColor color)
        {
            ConsoleColor old_foreground = Console.ForegroundColor;
            try
            {
                SetForegroundColor(color);
                Console.WriteLine(text);
            }
            finally
            {
                SetForegroundColor(old_foreground);
            }
        }

        static void WriteLineWithColor(string text1, ConsoleColor color1,
                string text2)
        {
            WriteLineWithColor(text1, color1, text2, Console.ForegroundColor);
        }

        static void WriteError(string format, params object[] args)
        {
            WriteLineWithColor(string.Format(format, args), ConsoleColor.Red);
        }

        static void AddMessage(string username, string message)
        {
            WriteLineWithColor(username, ConsoleColor.Green, message);
        }

        static void SayGoodbye(string message)
        {
            AddMessage("Server", message);
        }

        static void ShowUserList(IEnumerable<UserListEntry> users)
        {
            Console.WriteLine("User List");
            foreach (var entry in users)
            {
                Console.WriteLine("{0} - {1}", entry.UserName, entry.HostName);
            }
        }

        static bool HandlePacket(ProtocolPacket packet)
        {
            switch (packet.CommandId)
            {
                case ProtocolCommandId.Message:
                    {
                        MessageProtocolPacket p = (MessageProtocolPacket)packet;
                        AddMessage(p.UserName, p.Message);
                    }
                    break;
                case ProtocolCommandId.Goodbye:
                    {
                        GoodbyeProtocolPacket goodbye = (GoodbyeProtocolPacket)packet;
                        SayGoodbye(goodbye.Message);
                        return true;
                    }
                case ProtocolCommandId.Hello:
                    {
                        HelloProtocolPacket p = (HelloProtocolPacket)packet;
                        AddMessage(p.UserName, String.Format("Hey I just joined from {0}!!11!", p.HostName));
                    }
                    break;
                case ProtocolCommandId.UserList:
                    {
                        UserListProtocolPacket p = (UserListProtocolPacket)packet;
                        ShowUserList(p.UserList);
                    }
                    break;
                default:
                    Console.WriteLine("Unsupported packet type, {0}",
                        packet.CommandId);
                    break;
            }
            return false;
        }

        static void PrintConsoleHelp()
        {
            WriteLineWithColor("Command Help", ConsoleColor.White, "");
            WriteLineWithColor("/quit [message]   ", ConsoleColor.White, "Quit client with optional message");
            WriteLineWithColor("/msg user message ", ConsoleColor.White, "Send a message to a specific user");
            WriteLineWithColor("/list             ", ConsoleColor.White, "List other users on the system");
            WriteLineWithColor("/help             ", ConsoleColor.White, "This help");
        }

        static string[] SplitString(string line)
        {
            return line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        }

        static private bool ProcessCommand(ChatConnection conn, string username, string line)
        {
            if (line.StartsWith("/"))
            {
                string[] cmdargs = SplitString(line);

                if (cmdargs.Length > 0)
                {
                    switch (cmdargs[0].ToLower())
                    {
                        case "/quit":
                            conn.WritePacket(new GoodbyeProtocolPacket(cmdargs.Length > 1 ? cmdargs[1] : "I'm going away now!"));
                            break;
                        case "/list":
                            conn.WritePacket(new GetUserListProtocolPacket());
                            break;
                        case "/msg":
                            {
                                bool invalid_msg = true;
                                if (cmdargs.Length > 1)
                                {
                                    string[] user_and_msg = SplitString(cmdargs[1]);
                                    if (user_and_msg.Length == 2)
                                    {
                                        conn.WritePacket(new TargetProtocolPacket(user_and_msg[0], new MessageProtocolPacket(username, user_and_msg[1])));
                                        invalid_msg = false;
                                    }
                                }
                                if (invalid_msg)
                                {
                                    WriteError("Invalid msg command");
                                }
                            }
                            break;       
                        case "/help":
                            PrintConsoleHelp();
                            break;
                        default:
                            WriteError("Unknown command {0}", cmdargs[0]);
                            break;
                    }
                }
            }
            else
            {
                conn.SendMessage(username, line);
            }

            return false;
        }

        static async Task<ChatConnection> Connect(DnsEndPoint server, DnsEndPoint socks_proxy, bool text, bool tls, bool buffered)
        {
            ChatConnection conn = new ChatConnection();
            if (socks_proxy == null)
            {
                await conn.Connect(server.Host, server.Port, text, tls, buffered);
            }
            else
            {
                await conn.Connect(server.Host, server.Port, text, tls, buffered,
                    socks_proxy.Host, socks_proxy.Port);
            }
            return conn;
        }

        static async Task RunClient(ChatConnection conn, string username, bool supports_upgrade)
        {
            ProtocolPacket packet = await conn.HandleHello(username, supports_upgrade);
            try
            {
                bool exit = false;
                exit = HandlePacket(packet);
                while (!exit)
                {
                    packet = await conn.ReadPacket();
                    exit = HandlePacket(packet);
                }
            }
            catch
            {
                HandlePacket(new GoodbyeProtocolPacket("Connection Closed :("));
            }
        }

        static async Task MainLoop(DnsEndPoint server, DnsEndPoint socks_proxy, bool text, bool tls, bool buffered, string username, bool supports_upgrade)
        {
            using (ChatConnection conn = await Connect(server, socks_proxy, text, tls, buffered))
            {
                Task client_task = RunClient(conn, username, supports_upgrade);
                Task<string> line_task = Task.Run(Console.In.ReadLineAsync);
                bool done = false;
                while (!done)
                {
                    int task = Task.WaitAny(client_task, line_task);
                    if (task == 0)
                    {
                        done = true;
                    }
                    else
                    {
                        if (line_task.IsFaulted)
                        {
                            throw line_task.Exception;
                        }

                        done = ProcessCommand(conn, username, line_task.Result.TrimEnd());
                        line_task = Task.Run(Console.In.ReadLineAsync);
                    }
                }
            }
        }

        static int ParsePort(CommandOption port)
        {
            if (port.HasValue())
            {
                return NetworkUtils.ParseInt(port.Value());
            }
            return NetworkUtils.DEFAULT_PORT;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("ChatClient (c) 2017 James Forshaw");
            Console.WriteLine("WARNING: Don't use this for a real chat system!!!");

            CommandLineApplication app = new CommandLineApplication(false);
            CommandArgument username = app.Argument("username", "Specify the username to use for the connection.");
            CommandArgument server = app.Argument("server", "Specify server hostname.");
            CommandOption socks = app.Option(
              "-s | --socks <host:port>",
              "Specify a SOCKS v4 server to connect to.",
              CommandOptionType.SingleValue);
            CommandOption xor = app.Option(
              "-x | --xor", "Enable simple XOR \"encryption\"",
              CommandOptionType.NoValue);
            CommandOption tls = app.Option(
              "-l | --tls", "Enable TLS",
              CommandOptionType.NoValue);
            CommandOption buffered = app.Option(
              "-b", "Buffer writes.",
              CommandOptionType.NoValue);
            CommandOption color_enable = app.Option(
                "--color", "Enable console color output.",
                CommandOptionType.NoValue);
            CommandOption text = app.Option(
              "-t | --text", "Use text based protocol instead of binary",
              CommandOptionType.NoValue);
            CommandOption port = app.Option(
                "-p | --port <port>",
                "Specify the base TCP port for connection.",
                CommandOptionType.SingleValue);

            app.ShowInHelpText = true;
            app.HelpOption("-? | -h | --help");
            app.OnExecute(() =>
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(server.Value) || String.IsNullOrWhiteSpace(username.Value))
                    {
                        app.ShowHelp();
                        return 1;
                    }

                    _color_enable = color_enable.HasValue();
                    int server_port = ParsePort(port);

                    Console.WriteLine("Connecting to {0}:{1}", server.Value, tls.HasValue() ? server_port + 1 : server_port);
                    DnsEndPoint server_ep = new DnsEndPoint(server.Value, server_port);
                    DnsEndPoint socks_ep = null;
                    if (socks.HasValue())
                    {
                        socks_ep = NetworkUtils.ParseEndpoint(socks.Value());
                    }
                    MainLoop(server_ep, socks_ep, text.HasValue(), tls.HasValue(), buffered.HasValue(), username.Value, xor.HasValue()).Wait();
                }
                catch(Exception ex)
                {
                    WriteError("Error: {0}", ex.Message);
                }

                return 0;
            });
            app.Execute(args);
        }
    }
}
