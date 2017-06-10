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

using System.IO;
using System.Threading.Tasks;

namespace ChatProtocol
{
    public sealed class BinaryNetworkTransport : INetworkTransport
    {
        private BinaryReader _reader;
        private BinaryWriter _writer;

        public BinaryNetworkTransport(Stream stream, bool buffered)
        {
            if (buffered)
            {
                stream = new BufferedStream(stream);
            }

            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        private static int CalculateChecksum(byte command_byte, byte[] data)
        {
            int ret = command_byte;

            foreach (byte b in data)
            {
                ret = ret + b;
            }

            return ret;
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
        }

        private ProtocolPacket ReadPacket()
        {
            int total_len = _reader.ReadInt32NetworkOrder();
            if (total_len < 1)
            {
                throw new InvalidDataException("Invalid length field, must be at least 1 for command.");
            }
            int chksum = _reader.ReadInt32NetworkOrder();
            byte cmd = _reader.ReadByte();
            byte[] data = _reader.ReadAllBytes(total_len - 1);
            if (CalculateChecksum(cmd, data) != chksum)
            {
                throw new InvalidDataException("Checksum does not match");
            }

            return ProtocolPacket.FromData((ProtocolCommandId)cmd, new BinaryDataReader(data));
        }

        public Task<ProtocolPacket> ReadPacketAsync()
        {
            //Task.Factory.StartNew(ReadPacket, TaskCreationOptions
            return Task.Run(() => ReadPacket());
        }

        private byte[] PacketToBytes(ProtocolPacket packet)
        {
            BinaryDataWriter writer = new BinaryDataWriter();
            packet.GetData(writer);
            return writer.ToArray();
        }

        public void WritePacket(ProtocolPacket packet)
        {
            byte[] data = PacketToBytes(packet);
            int total_len = 1 + data.Length;
            byte cmd = (byte)(int)packet.CommandId;
            int chksum = CalculateChecksum(cmd, data);
            _writer.WriteNetworkOrder(total_len);
            _writer.WriteNetworkOrder(chksum);
            _writer.Write(cmd);
            _writer.Write(data);
            _writer.Flush();
        }
    }
}
