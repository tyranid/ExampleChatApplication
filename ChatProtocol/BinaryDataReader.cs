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
using System.Text;

namespace ChatProtocol
{
    class BinaryDataReader : IDataReader
    {
        BinaryReader _reader;

        public BinaryDataReader(byte[] data)
        {
            _reader = new BinaryReader(new MemoryStream(data), new UTF8Encoding(false));
        }

        public bool ReadBoolean()
        {
            return _reader.ReadBoolean();
        }

        public byte ReadByte()
        {
            return _reader.ReadByte();
        }

        public byte[] ReadBytes()
        {
            int length = _reader.ReadInt32NetworkOrder();
            return _reader.ReadAllBytes(length);
        }

        public int ReadInt32()
        {
            return _reader.ReadInt32NetworkOrder();
        }

        public string ReadString()
        {
            return _reader.ReadString();
        }
    }
}
