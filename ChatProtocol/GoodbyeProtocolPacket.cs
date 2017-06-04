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

namespace ChatProtocol
{
    public class GoodbyeProtocolPacket : ProtocolPacket
    {
        public string Message { get; set; }

        private GoodbyeProtocolPacket() 
            : base(ProtocolCommandId.Goodbye)
        {
        }

        public override void GetData(IDataWriter writer)
        {
            writer.Write(Message);
        }

        public GoodbyeProtocolPacket(IDataReader reader) : this()
        {
            Message = reader.ReadString();
        }

        public GoodbyeProtocolPacket(string message) : this()
        {
            Message = message;
        }
    }
}
