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
    public sealed class MessageProtocolPacket : ProtocolPacket
    {
        public string UserName { get; set; }
        public string Message { get; set; }

        private MessageProtocolPacket()
            : base(ProtocolCommandId.Message)
        {
        }

        public MessageProtocolPacket(string username, string message)
            : this()
        {
            UserName = username;
            Message = message; ;
        }

        internal MessageProtocolPacket(IDataReader reader)
            : this()
        {
            UserName = reader.ReadString();
            Message = reader.ReadString();
        }

        public override void GetData(IDataWriter writer)
        {
            writer.Write(UserName);
            writer.Write(Message);
        }
    }
}