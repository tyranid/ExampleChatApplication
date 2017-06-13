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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ChatServer
{
    sealed class TcpNetworkListener : INetworkListener
    {
        private TcpListener _listener;
        private X509Certificate2 _cert;
        private string _local_endpoint;
        private bool _buffered;

        public TcpNetworkListener(int port, bool global, bool buffered, X509Certificate2 cert)
        {
            _buffered = buffered;
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
            return String.Format("{0}{1}", _cert != null ? "tls:" : "", ep);
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
                return new AcceptState(new TcpClientEntry(stream, remote_endpoint, _buffered), 
                    remote_endpoint, _local_endpoint, this, null);
            }
            catch (Exception ex)
            {
                return new AcceptState(null, remote_endpoint, _local_endpoint, this, ex);
            }
        }
    }

    internal sealed class TcpClientEntry : IClientEntry
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

        public TcpClientEntry(Stream stm, string endpoint, bool buffered)
        {
            _xor_stream = new XorStream(stm);
            _buffered = buffered;
            UserName = String.Format("User_", endpoint);
            HostName = endpoint;
        }
    }
}