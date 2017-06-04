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

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ChatProtocol
{
    public class TextNetworkTransport : INetworkTransport
    {
        StreamReader _reader;
        StreamWriter _writer;

        public TextNetworkTransport(Stream stm)
        {
            _reader = new StreamReader(stm, new UTF8Encoding(false));
            _writer = new StreamWriter(stm, new UTF8Encoding(false));
            _writer.AutoFlush = true;
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
        }

        private ProtocolPacket StringToPacket(string line)
        {
            TextDataReader reader = new TextDataReader(line.Trim());
            ProtocolCommandId cmd = (ProtocolCommandId)Enum.Parse(typeof(ProtocolCommandId), reader.ReadString());
            return ProtocolPacket.FromData(cmd, reader);
        }

        public async Task<ProtocolPacket> ReadPacketAsync()
        {
            string line = await _reader.ReadLineAsync();
            return StringToPacket(line);
        }

        static string PacketToString(ProtocolPacket packet)
        {
            TextDataWriter writer = new TextDataWriter();
            writer.Write(packet.CommandId.ToString());
            packet.GetData(writer);
            return writer.ToString();
        }

        public void WritePacket(ProtocolPacket packet)
        {
            _writer.WriteLine(PacketToString(packet));
        }
    }
}
