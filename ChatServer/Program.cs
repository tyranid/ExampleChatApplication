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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ChatServer
{
    public class ServerProgram
    {
        // Return false if the connection should be closed.
        static bool HandlePacket(IClientEntry client, IEnumerable<IClientEntry> other_clients, ProtocolPacket packet)
        {
            bool result = true;
            ProtocolPacket write_packet = null;

            switch (packet.CommandId)
            {
                case ProtocolCommandId.Hello:
                    {
                        HelloProtocolPacket hello = (HelloProtocolPacket)packet;
                        Console.WriteLine("Hello Packet for User: {0} HostName: {1}", hello.UserName, hello.HostName);
                        client.UserName = hello.UserName;
                        client.HostName = hello.HostName;
                        ReKeyProtocolPacket rekey = new ReKeyProtocolPacket();
                        if (hello.SupportsSecurityUpgrade)
                        {
                            Random r = new Random();
                            rekey.XorKey = (byte)r.Next(256);
                        }
                        result = client.WritePacket(rekey);
                        client.SetXorKey(rekey.XorKey);

                        write_packet = new MessageProtocolPacket(hello.UserName, 
                            String.Format("I've just joined from {0}", hello.HostName));
                    }
                    break;
                case ProtocolCommandId.Message:
                    write_packet = packet;
                    break;
                case ProtocolCommandId.GetUserList:
                    result = client.WritePacket(new UserListProtocolPacket(other_clients.
                        Where(c => c.UserName != null && c.HostName != null).Select(c => new UserListEntry(c.UserName, c.HostName))));
                    break;
                case ProtocolCommandId.Target:
                    {
                        TargetProtocolPacket target = (TargetProtocolPacket)packet;
                        IClientEntry target_client = other_clients.Where(c => c.UserName.Equals(target.UserName)).FirstOrDefault();
                        if (target_client != null)
                        {
                            result = target_client.WritePacket(target.Packet);
                        }
                    }
                    break;
                case ProtocolCommandId.Goodbye:
                    client.WritePacket(new GoodbyeProtocolPacket("Don't let the door hit you on the way out!"));
                    if (!String.IsNullOrEmpty(client.UserName))
                    {
                        GoodbyeProtocolPacket goodbye = (GoodbyeProtocolPacket)packet;
                        write_packet = new MessageProtocolPacket("Server", String.Format("'{0}' has quit, they said '{1}'", client.UserName, goodbye.Message));
                    }
                    result = false;
                    break;
            }

            if (write_packet != null)
            {
                foreach (TcpClientEntry entry in other_clients)
                {
                    entry.WritePacket(write_packet);
                }
            }

            return result;
        }

        static void RunServer(int port, bool global, bool buffered, X509Certificate2 server_cert)
        {
            List<INetworkListener> listeners = new List<INetworkListener>();
            try
            {
                Console.WriteLine("Running server on port {0} Global Bind {1}", port, global);
                listeners.Add(new TcpNetworkListener(port, global, buffered, null));
                if (server_cert != null)
                {
                    Console.WriteLine("Running TLS server on port {0} Global Bind {1}", port + 1, global);
                    listeners.Add(new TcpNetworkListener(port + 1, global, buffered, server_cert));
                }
                listeners.Add(new UdpNetworkListener(port, global));
                List<Task> tasks = new List<Task>(listeners.Select(l => l.AcceptConnection()));
                List<IClientEntry> clients = new List<IClientEntry>();
                while (true)
                {
                    int task = Task.WaitAny(tasks.ToArray());
                    Task completed_task = tasks[task];
                    tasks.RemoveAt(task);

                    if (completed_task.IsFaulted)
                    {
                        // The state machine shouldn't fault.
                        throw completed_task.Exception;
                    }

                    var accept_task = completed_task as Task<AcceptState>;
                    var client_task = completed_task as Task<ReadPacketState>;

                    if (accept_task != null)
                    {
                        var accept = accept_task.Result;
                        Console.WriteLine("Connection from {0} to {1}", accept.RemoteEndpoint, accept.LocalEndpoint);
                        tasks.Add(accept.Listener.AcceptConnection());
                        IClientEntry client = accept.NewClient;
                        clients.Add(client);
                        tasks.Add(client.ReadPacketAsync());
                    }

                    if (client_task != null)
                    {
                        var result = client_task.Result;
                        bool keep_open = true;
                        if (result.Exception != null)
                        {
                            Console.WriteLine("Error from client '{0}'", result.Exception.Message);
                            keep_open = false;
                        }
                        else
                        {
                            Console.WriteLine("Received packet {0}", result.Packet);
                            keep_open = HandlePacket(result.Client, clients.Where(c => c != result.Client), result.Packet);
                        }

                        if (keep_open)
                        {
                            // Re-add task to read the next packet.
                            tasks.Add(result.Client.ReadPacketAsync());
                        }
                        else
                        {
                            Console.WriteLine("Closing Client");
                            result.Client.Dispose();
                            clients.Remove(result.Client);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Closing Server");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                foreach (var l in listeners)
                {
                    l.Dispose();
                }
            }
        }

        static async Task<bool> WriteUdpPacket(UdpClient client, IPEndPoint endpoint, ProtocolPacket packet)
        {
            try
            {
                MemoryStream stm = new MemoryStream();
                BinaryNetworkTransport.WritePacket(packet, new BinaryWriter(stm), false);
                byte[] data = stm.ToArray();

                int ret = await client.SendAsync(data, data.Length, endpoint);
                return ret == data.Length;
            }
            catch
            {
                return false;
            }
        }

        //static async Task RunUdpServer(int port, bool global)
        //{
        //    try
        //    {
        //        using (UdpClient client = new UdpClient(new IPEndPoint(global ? IPAddress.Any : IPAddress.Loopback, port)))
        //        {
        //            Console.WriteLine("Running UDP server on port {0} Global Bind {1}", port, global);
                    

        //            while (true)
        //            {
        //                UdpReceiveResult result = await client.ReceiveAsync();



        //                if (accept_task != null)
        //                {
        //                    var accept = accept_task.Result;
        //                    Console.WriteLine("Connection from {0} to {1}", accept.RemoteEndpoint, accept.LocalEndpoint);
        //                    tasks.Add(accept.Listener.AcceptConnection());
        //                    ClientEntry client = new ClientEntry(accept.NewClient, accept.RemoteEndpoint, buffered);
        //                    clients.Add(client);
        //                    tasks.Add(client.ReadPacketAsync());
        //                }

        //                if (client_task != null)
        //                {
        //                    var result = client_task.Result;
        //                    bool keep_open = true;
        //                    if (result.Exception != null)
        //                    {
        //                        Console.WriteLine("Error from client '{0}'", result.Exception.Message);
        //                        keep_open = false;
        //                    }
        //                    else
        //                    {
        //                        Console.WriteLine("Received packet {0}", result.Packet);
        //                        keep_open = HandlePacket(result.Client, clients.Where(c => c != result.Client), result.Packet);
        //                    }

        //                    if (keep_open)
        //                    {
        //                        // Re-add task to read the next packet.
        //                        tasks.Add(result.Client.ReadPacketAsync());
        //                    }
        //                    else
        //                    {
        //                        Console.WriteLine("Closing Client");
        //                        result.Client.Dispose();
        //                        clients.Remove(result.Client);
        //                    }
        //                }
        //            }
        //        }
        //    catch (OperationCanceledException)
        //    {
        //        Console.WriteLine("Closing Server");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Error: {0}", ex);
        //    }
        //}

        static X509Certificate2 LoadCert(CommandOption cert_file, CommandOption cert_password)
        {
            X509Certificate2 cert = null;
            if (cert_file.HasValue())
            {
                cert = new X509Certificate2(cert_file.Value(), cert_password.Value());
                if (!cert.HasPrivateKey)
                {
                    throw new ArgumentException("Server certificate must contiain a private key.");
                }
                Console.WriteLine("Loaded certificate, Subject={0}", cert.Subject);
            }
            return cert;
        }

        static int ParsePort(CommandOption port)
        {
            if (port.HasValue())
            {
                return NetworkUtils.ParseInt(port.Value());
            }
            return NetworkUtils.DEFAULT_PORT;
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("ChatServer (c) 2017 James Forshaw");
            Console.WriteLine("WARNING: Don't use this for a real chat system!!!");

            CommandLineApplication app = new CommandLineApplication(false);
            CommandOption server_cert = app.Option(
              "-c | --server_cert <server.pfx>",
              "Specify the server certificate for TLS support.",
              CommandOptionType.SingleValue);
            CommandOption cert_password = app.Option(
              "--password <cert_password>",
              "Specify a password to unlock server certificate.",
              CommandOptionType.SingleValue);
            CommandOption port = app.Option(
                "-p | --port <port>",
                "Specify the base TCP port for connection.",
                CommandOptionType.SingleValue);
            CommandOption global = app.Option(
              "-g | --global", "Bind to all listening addresses.",
              CommandOptionType.NoValue);
            CommandOption buffered = app.Option(
                "-b", "Buffer writes.",
                CommandOptionType.NoValue);

            app.ShowInHelpText = true;
            app.HelpOption("-? | -h | --help");
            app.OnExecute(() =>
            {
                try
                {
                    RunServer(ParsePort(port),
                        global.HasValue(), buffered.HasValue(),
                        LoadCert(server_cert, cert_password));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                }

                return 0;
            });
            app.Execute(args);
        }
    }
}
