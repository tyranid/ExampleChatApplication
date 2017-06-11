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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ChatClient
{
    internal sealed class ChatConnection : IDisposable
    {
        public static int DEFAULT_CHAT_PORT = 12345;

        private INetworkTransport _transport;
        private TcpClient _client;
        private XorStream _base_stream;

        private static bool ValidateRemoteConnection(
            Object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        )
        {
            // We always succeed
            return true;
        }

        private async Task DoConnect(TcpClient client, string hostname, bool text, bool tls, bool buffered)
        {
            Stream stm;

            _client = client;
            _client.NoDelay = true;

            if (tls)
            {
                SslStream sslStream = new SslStream(_client.GetStream(), false, ValidateRemoteConnection);

                int lastTimeout = sslStream.ReadTimeout;
                sslStream.ReadTimeout = 3000;                
                await sslStream.AuthenticateAsClientAsync(hostname);

                sslStream.ReadTimeout = lastTimeout;

                stm = sslStream;
            }
            else
            {
                stm = _client.GetStream();
            }

            _base_stream = new XorStream(stm);
            if (text)
            {
                NetworkUtils.WriteNetworkOrderInt32(_base_stream, NetworkUtils.TEXT_MAGIC);
                _transport = new TextNetworkTransport(_base_stream);
            }
            else
            {
                NetworkUtils.WriteNetworkOrderInt32(_base_stream, NetworkUtils.BINARY_MAGIC);
                _transport = new BinaryNetworkTransport(_base_stream, buffered);
            }
        }

        public async Task HandleHello(string username, bool supports_upgrade)
        {            
            WritePacket(new HelloProtocolPacket(username, Environment.MachineName, supports_upgrade));

            ProtocolPacket packet = await ReadPacket(3000);
            if (packet.CommandId == ProtocolCommandId.Goodbye)
            {
                throw new EndOfStreamException(((GoodbyeProtocolPacket)packet).Message);
            }
            else if (packet.CommandId != ProtocolCommandId.ReKey)
            {
                throw new EndOfStreamException("Unknow packet response");
            }
            else
            {
                ReKeyProtocolPacket p = (ReKeyProtocolPacket)packet;
                _base_stream.XorKey = p.XorKey;
            }
        }
        
        private async static Task<IPAddress> GetHostIP(string hostname)
        {
            IPAddress hostaddr;

            if (IPAddress.TryParse(hostname, out hostaddr))
            {
                return hostaddr;
            }

            IPHostEntry ent = await Dns.GetHostEntryAsync(hostname);

            foreach (IPAddress addr in ent.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    return addr;
                }
            }

            throw new ArgumentException("Cannot get a IPv4 address for host");
        }

        private async Task<TcpClient> Connect(string hostname, int port)
        {
            IPAddress hostaddr = await GetHostIP(hostname);
            TcpClient client = new TcpClient();
            await client.ConnectAsync(hostaddr.ToString(), port);
            return client;
        }

        private static async Task<byte[]> CreateSocksRequest(string hostname, int port)
        {
            IPAddress hostaddr = await GetHostIP(hostname);

            MemoryStream stm = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stm);
            
            writer.Write((byte)4);
            writer.Write((byte)1);
            writer.Write(IPAddress.HostToNetworkOrder((short)port));
            writer.Write(hostaddr.GetAddressBytes());
            writer.Write((byte)0);            
            
            return stm.ToArray();
        }

        private async Task<TcpClient> ConnectThroughSocks(string hostname, int port, string proxyaddr, int proxyport)
        {
            bool connected = false;

            TcpClient client = await Connect(proxyaddr, proxyport);

            try
            {
                byte[] req = await CreateSocksRequest(hostname, port);
                Stream stm = client.GetStream();
                stm.Write(req, 0, req.Length);
                byte[] resp = await NetworkUtils.ReadAllBytesAsync(stm, 8);

                if (resp[1] == 0x5A)
                {
                    connected = true;
                }
                else
                {
                    throw new EndOfStreamException("Failed to connect through SOCKS proxy");
                }
            }
            finally
            {
                if (!connected && (client != null))
                {
                    client.Dispose();
                }
            }

            return client;
        }

        public async Task Connect(string hostname, int port, bool text, bool tls, bool buffered)
        {
            await DoConnect(await Connect(hostname, tls ? port+1 : port), hostname, text, tls, buffered);
        }

        public async Task Connect(string hostname, int port, bool text, bool tls, bool buffered, string proxyaddr, int proxyport)
        {
            await DoConnect(await ConnectThroughSocks(hostname, tls ? port+1 : port, proxyaddr, proxyport), hostname, text, tls, buffered);
        }

        public void WritePacket(ProtocolPacket packet)
        {
            _transport.WritePacket(packet);
        }

        public async Task<ProtocolPacket> ReadPacket()
        {
            return await _transport.ReadPacketAsync();
        }

        public async Task<ProtocolPacket> ReadPacket(int timeout)
        {
            int lastTimeout = _base_stream.ReadTimeout;
            try
            {
                _base_stream.ReadTimeout = timeout;
                return await ReadPacket();
            }
            finally
            {
                _base_stream.ReadTimeout = lastTimeout;
            }
        }

        public void SendMessage(string username, string message)
        {
            MessageProtocolPacket packet = new MessageProtocolPacket(username, message);

            WritePacket(packet);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        #endregion
    }
}
