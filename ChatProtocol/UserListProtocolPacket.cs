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

using System.Collections.Generic;
using System.Linq;

namespace ChatProtocol
{
    public class UserListEntry
    {
        public string UserName { get; private set; }
        public string HostName { get; private set; }

        public UserListEntry(string userName, string hostName)
        {
            UserName = userName;
            HostName = hostName;
        }
    }

    public class UserListProtocolPacket : ProtocolPacket
    {
        public IEnumerable<UserListEntry> UserList { get; private set; }

        public override void GetData(IDataWriter writer)
        {
            writer.Write(UserList.Count());
            foreach(UserListEntry ent in UserList)
            {
                writer.Write(ent.UserName);
                writer.Write(ent.HostName);
            }
        }

        private UserListProtocolPacket() 
            : base(ProtocolCommandId.UserList)
        {
        }

        public UserListProtocolPacket(IDataReader reader) 
            : this()
        {
            int len = reader.ReadInt32();

            List<UserListEntry> user_list = new List<UserListEntry>(len);

            for (int i = 0; i < len; i++)
            {
                string userName = reader.ReadString();
                string hostName = reader.ReadString();

                user_list.Add(new UserListEntry(userName, hostName));
            }

            UserList = user_list.AsReadOnly();
        }

        public UserListProtocolPacket(IEnumerable<UserListEntry> entries)
            : this()
        {
            UserList = new List<UserListEntry>(entries).AsReadOnly();
        }
    }
}
