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
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ChatApplicationLibrary
{
    public class ServerProgram
    {
        struct AcceptState
        {
            public Stream NewClient { get; private set; }
            public string RemoteEndpoint { get; private set; }
            public string LocalEndpoint { get; private set; }
            public NetworkListener Listener { get; private set; }
            public Exception Exception { get; private set; }

            public AcceptState(Stream new_client, string remote_endpoint, 
                string local_endpoint, NetworkListener listener, Exception exception)
            {
                NewClient = new_client;
                RemoteEndpoint = remote_endpoint;
                LocalEndpoint = local_endpoint;
                Listener = listener;
                Exception = exception;
            }
        }

        struct ReadPacketState
        {
            public ClientEntry Client { get; private set; }
            public ProtocolPacket Packet { get; private set; }
            public Exception Exception { get; private set; }

            public ReadPacketState(ClientEntry client, ProtocolPacket packet, Exception exception)
            {
                Client = client;
                Packet = packet;
                Exception = exception;
            }
        }

        sealed class NetworkListener : IDisposable
        {
            private TcpListener _listener;
            private X509Certificate2 _cert;
            private string _local_endpoint;

            public NetworkListener(int port, bool global, X509Certificate2 cert)
            {
                _listener = new TcpListener(global ? IPAddress.Any : IPAddress.Loopback, port);
                _listener.Start();          
                _cert = cert;
                if (cert != null)
                {
                    _local_endpoint = FormatEndpoint(_listener.Server.LocalEndPoint);
                }
            }

            public void Dispose()
            {
                try
                {
                    _listener?.Stop();
                }
                catch
                {
                }
                _cert?.Dispose();
            }

            private string FormatEndpoint(EndPoint ep)
            {
                return String.Format("{0}{1}", _cert != null ? "ssl:" : "", ep);
            }

            public async Task<AcceptState> AcceptConnection()
            {
                string remote_endpoint = FormatEndpoint(new IPEndPoint(IPAddress.None, 0));
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    remote_endpoint = FormatEndpoint(client.Client.RemoteEndPoint);
                    Stream stream = client.GetStream();
                    if (_cert != null)
                    {
                        SslStream ssl_stream = new SslStream(stream);
                        await ssl_stream.AuthenticateAsServerAsync(_cert, false, 
                            SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
                        stream = ssl_stream;
                    }
                    return new AcceptState(stream, remote_endpoint, _local_endpoint, this, null);
                }
                catch (Exception ex)
                {
                    return new AcceptState(null, remote_endpoint, _local_endpoint, this, ex);
                }
            }
        }


        sealed class ClientEntry : IDisposable
        {
            private XorStream _xor_stream;
            private INetworkTransport _transport;
            private bool _buffered;

            public string UserName { get; set; }
            public string HostName { get; set; }

            public void SetXorKey(byte key)
            {
                _xor_stream.XorKey = key;
            }

            public async Task<ReadPacketState> ReadPacketAsync()
            {
                try
                {
                    if (_transport == null)
                    {
                        int magic = await NetworkUtils.ReadNetworkOrderInt32Async(_xor_stream);
                        switch (magic)
                        {
                            case NetworkUtils.BINARY_MAGIC:
                                _transport = new BinaryNetworkTransport(_xor_stream, _buffered);
                                break;
                            case NetworkUtils.TEXT_MAGIC:
                                _transport = new TextNetworkTransport(_xor_stream);
                                break;
                            default:
                                throw new ArgumentException(String.Format("Invalid magic received {0:X}", magic));
                        }
                    }

                    return new ReadPacketState(this, await _transport.ReadPacketAsync(), null);
                }
                catch (Exception ex)
                {
                    return new ReadPacketState(this, null, ex);
                }
            }

            public bool WritePacket(ProtocolPacket packet)
            {
                try
                {
                    _transport.WritePacket(packet);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public void Dispose()
            {
                _transport?.Dispose();
            }

            public ClientEntry(Stream stm, string endpoint, bool buffered)
            {
                _xor_stream = new XorStream(stm);
                _buffered = buffered;
                UserName = String.Format("User_", endpoint);
                HostName = endpoint;
            }
        }
        
        // Return false if the connection should be closed.
        static bool HandlePacket(ClientEntry client, IEnumerable<ClientEntry> other_clients, ProtocolPacket packet)
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
                        result = client.WritePacket(hello);
                        write_packet = packet;
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
                        ClientEntry target_client = other_clients.Where(c => c.UserName.Equals(target.UserName)).FirstOrDefault();
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
                foreach (ClientEntry entry in other_clients)
                {
                    entry.WritePacket(write_packet);
                }
            }

            return result;
        }

        static void RunServer(int port, bool global, bool buffered, X509Certificate2 server_cert)
        {
            List<NetworkListener> listeners = new List<NetworkListener>();
            try
            {
                Console.WriteLine("Running server on port {0} Global Bind {1}", port, global);
                listeners.Add(new NetworkListener(port, global, null));
                if (server_cert != null)
                {
                    Console.WriteLine("Running TLS server on port {0} Global Bind {1}", port + 1, global);
                    listeners.Add(new NetworkListener(port + 1, global, server_cert));
                }
                List<Task> tasks = new List<Task>(listeners.Select(l => l.AcceptConnection()));
                List<ClientEntry> clients = new List<ClientEntry>();
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
                        ClientEntry client = new ClientEntry(accept.NewClient, accept.RemoteEndpoint, buffered);
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
