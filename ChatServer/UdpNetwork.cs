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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ChatServer
{
    sealed class UdpNetworkListener : INetworkListener
    {
        UdpClient _client;
        Dictionary<IPEndPoint, UdpClientEntry> _clients;
        IPEndPoint _bind_endpoint;

        public UdpNetworkListener(int port, bool global)
        {
            _bind_endpoint = new IPEndPoint(global ? IPAddress.Any : IPAddress.Loopback, port);
            _client = new UdpClient(_bind_endpoint);
            _client.DontFragment = true;
            _clients = new Dictionary<IPEndPoint, UdpClientEntry>();
        }

        public async Task<AcceptState> AcceptConnection()
        {
            while (true)
            {
                try
                {
                    UdpReceiveResult result = await _client.ReceiveAsync();
                    lock (_clients)
                    {
                        if (_clients.ContainsKey(result.RemoteEndPoint))
                        {
                            _clients[result.RemoteEndPoint].Enqueue(result.Buffer);
                        }
                        else
                        {
                            UdpClientEntry client = new UdpClientEntry(this, result.RemoteEndPoint);
                            client.Enqueue(result.Buffer);
                            _clients.Add(result.RemoteEndPoint, client);
                            return new AcceptState(client, result.RemoteEndPoint.ToString(), "UDP", this, null);
                        }
                    }
                }
                catch(SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        Console.Error.WriteLine("One or more clients is dead, trying to ping them");

                        lock (_clients)
                        {
                            foreach (var pair in _clients.ToArray())
                            {
                                var client = pair.Value;
                                DateTime current_time = DateTime.UtcNow;

                                if (!client.LastPingTime.HasValue)
                                {
                                    client.WritePacket(new PingProtocolPacket());
                                    client.LastPingTime = current_time;
                                }
                                else if (current_time > client.LastPingTime.Value.AddMinutes(2))
                                {
                                    Console.WriteLine("Client {0} exceeded ping response time",
                                        client.ClientEndpoint);
                                    pair.Value.Cancel();
                                    _clients.Remove(pair.Key);
                                }
                                else if (current_time > client.LastPingTime.Value.AddMinutes(1))
                                {
                                    // Send again but don't update ping time.
                                    client.WritePacket(new PingProtocolPacket());
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("UDP socket had error {0}", ex.Message);
                    }
                }
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            lock (_clients)
            {
                foreach (var client in _clients)
                {
                    client.Value.Cancel();
                }
            }
        }

        sealed class UdpClientEntry : IClientEntry
        {
            private UdpNetworkListener _listener;
            private IPEndPoint _endpoint;
            private Queue<byte[]> _queue;
            private SemaphoreSlim _semaphore;
            private CancellationTokenSource _cancel_source;

            public string UserName { get; set; }
            public string HostName { get; set; }
            public IPEndPoint ClientEndpoint { get; set; }
            internal DateTime? LastPingTime { get; set; }

            internal void Enqueue(byte[] data)
            {
                lock (_queue)
                {
                    _queue.Enqueue(data);
                    _semaphore.Release();
                }
            }

            public bool WritePacket(ProtocolPacket packet)
            {
                try
                {
                    MemoryStream stm = new MemoryStream();
                    BinaryNetworkTransport.WritePacket(packet, new BinaryWriter(stm), false);
                    byte[] data = stm.ToArray();
                    return _listener._client.SendAsync(data, data.Length, _endpoint).GetAwaiter().GetResult() == data.Length;
                }
                catch(Exception)
                {
                    Cancel();
                    return false;
                }
            }

            public void Cancel()
            {
                _cancel_source.Cancel();
            }

            public async Task<ReadPacketState> ReadPacketAsync()
            {
                try
                {
                    await _semaphore.WaitAsync(_cancel_source.Token);
                    byte[] data;
                    lock (_queue)
                    {
                        data = _queue.Dequeue();
                        LastPingTime = null;
                    }
                    
                    MemoryStream stm = new MemoryStream(data);
                    return new ReadPacketState(this, 
                        BinaryNetworkTransport.ReadPacket(new BinaryReader(stm), data.Length - 4), null);
                }
                catch (Exception ex)
                {
                    return new ReadPacketState(this, null, ex);
                }
            }

            public void SetXorKey(byte xorkey)
            {
                // Do nothing.
            }

            public void Dispose()
            {
                lock (_listener._clients)
                {
                    _listener._clients.Remove(_endpoint);
                }
                _cancel_source?.Dispose();
            }

            public UdpClientEntry(UdpNetworkListener listener, IPEndPoint endpoint)
            {
                _listener = listener;
                _endpoint = endpoint;
                _semaphore = new SemaphoreSlim(0);
                _queue = new Queue<byte[]>();
                _cancel_source = new CancellationTokenSource();
                UserName = String.Format("User_{0}", endpoint);
                HostName = endpoint.ToString();
            }
        }
    }
}