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
    public class HelloProtocolPacket : ProtocolPacket
    {
        public string UserName { get; set; }
        public string HostName { get; set; }
        public bool SupportsSecurityUpgrade { get; set; }
        public byte XorKey { get; set; }

        public override void GetData(IDataWriter writer)
        {
            writer.Write(UserName);
            writer.Write(HostName);
            writer.Write(SupportsSecurityUpgrade);
            if (SupportsSecurityUpgrade)
            {
                writer.Write(XorKey);
            }
        }

        private HelloProtocolPacket() : base(ProtocolCommandId.Hello)
        {
        }

        public HelloProtocolPacket(IDataReader reader) : this()
        {
            UserName = reader.ReadString();
            HostName = reader.ReadString();
            SupportsSecurityUpgrade = reader.ReadBoolean();
            if (SupportsSecurityUpgrade)
            {
                XorKey = reader.ReadByte();
            }
        }

        public HelloProtocolPacket(string userName, string hostName, 
            bool supportsSecurityUpgrade, byte xorkey) : this()
        {
            UserName = userName;
            HostName = hostName;
            SupportsSecurityUpgrade = supportsSecurityUpgrade;
            XorKey = xorkey;
        }
    }
}
