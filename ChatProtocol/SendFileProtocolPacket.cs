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
    public class SendFileProtocolPacket : ProtocolPacket
    {
        public string UserName { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }

        public override void GetData(IDataWriter writer)
        {
            writer.Write(UserName);
            writer.Write(Name);
            writer.Write(Data);
        }

        private SendFileProtocolPacket() 
            : base(ProtocolCommandId.SendFile)
        {
        }

        public SendFileProtocolPacket(IDataReader reader) : this()
        {
            UserName = reader.ReadString();
            Name = reader.ReadString();
            Data = reader.ReadBytes();
        }

        public SendFileProtocolPacket(string username, string name, byte[] data) : this()
        {
            UserName = username;
            Name = name;
            Data = data;
        }
    }
}
