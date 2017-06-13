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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ChatServer
{
    sealed class UdpClientEntry
    {
        private UdpClient _client;
        private IPEndPoint _endpoint;

        public string UserName { get; set; }
        public string HostName { get; set; }
        public IPEndPoint ClientEndpoint { get; set; }

        public bool WritePacket(ProtocolPacket packet)
        {
            try
            {

                return true;
            }
            catch
            {
                return false;
            }
        }

        public UdpClientEntry(UdpClient client, string hostname, IPEndPoint endpoint)
        {
            _client = client;
            _endpoint = endpoint;
            UserName = String.Format("User_", hostname);
            HostName = hostname;
        }
    }

    struct AcceptState
    {
        public IClientEntry NewClient { get; private set; }
        public string RemoteEndpoint { get; private set; }
        public string LocalEndpoint { get; private set; }
        public INetworkListener Listener { get; private set; }
        public Exception Exception { get; private set; }

        public AcceptState(IClientEntry new_client, string remote_endpoint,
            string local_endpoint, INetworkListener listener, Exception exception)
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
        public IClientEntry Client { get; private set; }
        public ProtocolPacket Packet { get; private set; }
        public Exception Exception { get; private set; }

        public ReadPacketState(TcpClientEntry client, 
            ProtocolPacket packet, Exception exception)
        {
            Client = client;
            Packet = packet;
            Exception = exception;
        }
    }

    internal interface INetworkListener : IDisposable
    {
        Task<AcceptState> AcceptConnection();
    }

    interface IClientEntry : IDisposable
    {
        string UserName { get; set; }
        string HostName { get; set; }

        Task<ReadPacketState> ReadPacketAsync();
        bool WritePacket(ProtocolPacket packet);
        void SetXorKey(byte xorkey);
    }
}