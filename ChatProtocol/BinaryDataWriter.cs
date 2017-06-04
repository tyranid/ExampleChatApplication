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
    class BinaryDataWriter : IDataWriter
    {
        MemoryStream _stm;
        BinaryWriter _writer;

        public BinaryDataWriter()
        {
            _stm = new MemoryStream();
            _writer = new BinaryWriter(_stm, new UTF8Encoding(false));
        }

        public byte[] ToArray()
        {
            return _stm.ToArray();
        }

        public void Write(string str)
        {
            _writer.Write(str);
        }

        public void Write(int i)
        {
            _writer.WriteNetworkOrder(i);
        }

        public void Write(bool b)
        {
            _writer.Write(b);
        }

        public void Write(byte[] ba)
        {
            _writer.WriteLengthBytes(ba);
        }

        public void Write(byte b)
        {
            _writer.Write(b);
        }
    }
}
