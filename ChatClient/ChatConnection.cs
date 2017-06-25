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

namespace ChatClient
{
    internal abstract class ChatConnection : IDisposable
    {
        public static int DEFAULT_CHAT_PORT = 12345;

        public abstract void Dispose();

        public abstract void WritePacket(ProtocolPacket packet);

        public abstract Task<ProtocolPacket> ReadPacket();

        public abstract Task<ProtocolPacket> ReadPacket(int timeout);

        protected abstract void SetXorKey(byte xorkey);

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
                throw new EndOfStreamException("Unknown packet response");
            }
            else
            {
                ReKeyProtocolPacket p = (ReKeyProtocolPacket)packet;
                SetXorKey(p.XorKey);
            }
        }

        public void SendMessage(string username, string message)
        {
            MessageProtocolPacket packet = new MessageProtocolPacket(username, message);

            WritePacket(packet);
        }
    }

    internal sealed class UdpChatConnection : ChatConnection
    {
        UdpClient _client;
        IPEndPoint _endpoint;

        public UdpChatConnection(DnsEndPoint endpoint)
        {
            IPAddress[] addrs = Dns.GetHostAddressesAsync(endpoint.Host).GetAwaiter().GetResult();
            foreach (IPAddress addr in addrs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    _endpoint = new IPEndPoint(addr, endpoint.Port);
                    break;
                }
            }

            if (_endpoint == null)
            {
                throw new ArgumentException("Can't lookup hostname");
            }

            _client = new UdpClient(AddressFamily.InterNetwork);
        }

        public override void Dispose()
        {
            _client?.Dispose();
        }

        public async override Task<ProtocolPacket> ReadPacket()
        {
            UdpReceiveResult result = await _client.ReceiveAsync();
            MemoryStream stm = new MemoryStream(result.Buffer);
            return BinaryNetworkTransport.ReadPacket(new BinaryReader(stm), result.Buffer.Length - 4);
        }

        public async override Task<ProtocolPacket> ReadPacket(int timeout)
        {
            int last_timeout = _client.Client.ReceiveTimeout;
            try
            {
                _client.Client.ReceiveTimeout = timeout;
                return await ReadPacket();
            }
            finally
            {
                _client.Client.ReceiveTimeout = last_timeout;
            }
        }

        public override void WritePacket(ProtocolPacket packet)
        {
            MemoryStream stm = new MemoryStream();
            BinaryNetworkTransport.WritePacket(packet, new BinaryWriter(stm), false);
            byte[] data = stm.ToArray();
            _client.SendAsync(data, data.Length, _endpoint).GetAwaiter().GetResult();
        }

        protected override void SetXorKey(byte xorkey)
        {
            // Do nothing.
        }
    }

    internal sealed class TcpChatConnection : ChatConnection
    {
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
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            Console.WriteLine("SSL Policy Errors: {0}", sslPolicyErrors);
            foreach (var status in chain.ChainStatus)
            {
                Console.WriteLine("Status: {0}", status.Status);
                Console.WriteLine("StatusInformation: {0}", status.StatusInformation);
            }

            return false;
        }

        private static bool ValidateRemoteConnectionBypass(
            Object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        )
        {
            return true;
        }

        private static string SslProtocolToString(SslProtocols protocol)
        {
            switch (protocol)
            {
                case SslProtocols.Tls:
                    return "TLS v1.0";
                case SslProtocols.Tls11:
                    return "TLS v1.1";
                case SslProtocols.Tls12:
                    return "TLS v1.2";
                default:
                    return protocol.ToString();
            }
        }

        private static string KeyExToString(ExchangeAlgorithmType keyex)
        {
            // For some reason ECDH isn't showing up
            if (keyex == (ExchangeAlgorithmType)44550)
            {
                return "ECDH";
            }
            else
            {
                return keyex.ToString();
            }
        }

        private async Task DoConnect(TcpClient client, string hostname, bool text, bool tls, bool verify_tls, bool buffered)
        {
            Stream stm;

            _client = client;
            _client.NoDelay = true;

            if (tls)
            {
                RemoteCertificateValidationCallback validation = verify_tls ?
                    new RemoteCertificateValidationCallback(ValidateRemoteConnection) :
                    new RemoteCertificateValidationCallback(ValidateRemoteConnectionBypass);
                SslStream sslStream = new SslStream(_client.GetStream(), false,
                        validation);
            
                int lastTimeout = sslStream.ReadTimeout;
                sslStream.ReadTimeout = 3000;                
                await sslStream.AuthenticateAsClientAsync(hostname);
                Console.WriteLine("TLS Protocol: {0}", SslProtocolToString(sslStream.SslProtocol));
                Console.WriteLine("TLS KeyEx   : {0}", KeyExToString(sslStream.KeyExchangeAlgorithm));
                Console.WriteLine("TLS Cipher:   {0}", sslStream.CipherAlgorithm);
                Console.WriteLine("TLS Hash:     {0}", sslStream.HashAlgorithm);
                Console.WriteLine("Cert Subject: {0}", sslStream.RemoteCertificate.Subject);
                Console.WriteLine("Cert Issuer : {0}", sslStream.RemoteCertificate.Issuer);

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

        public async Task Connect(string hostname, int port, bool text, bool tls, bool verify_tls, bool buffered)
        {
            await DoConnect(await Connect(hostname, tls ? port+1 : port), hostname, text, tls, verify_tls, buffered);
        }

        public async Task Connect(string hostname, int port, bool text, bool tls, bool verify_tls, bool buffered, string proxyaddr, int proxyport)
        {
            await DoConnect(await ConnectThroughSocks(hostname, tls ? port+1 : port, proxyaddr, proxyport), hostname, text, tls, verify_tls, buffered);
        }

        public override void WritePacket(ProtocolPacket packet)
        {
            _transport.WritePacket(packet);
        }

        public override async Task<ProtocolPacket> ReadPacket()
        {
            return await _transport.ReadPacketAsync();
        }

        public override async Task<ProtocolPacket> ReadPacket(int timeout)
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

        protected override void SetXorKey(byte xorkey)
        {
            if (xorkey != 0)
            {
                Console.WriteLine("ReKeying connection to key 0x{0:X02}", xorkey);
                _base_stream.XorKey = xorkey;
            }
        }

        #region IDisposable Members

        public override void Dispose()
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
