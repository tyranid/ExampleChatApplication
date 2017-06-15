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

namespace ChatProtocol
{
    public enum ProtocolCommandId
    {
        Hello,
        ReKey,
        Goodbye,
        Message,  
        SendFile,
        Target,
        GetUserList,
        UserList,
        Ping,
    }

    public abstract class ProtocolPacket
    {
        public ProtocolCommandId CommandId { get; private set; }

        protected ProtocolPacket(ProtocolCommandId command_id)
        {
            CommandId = command_id;
        }

        public abstract void GetData(IDataWriter writer);

        public static ProtocolPacket FromData(ProtocolCommandId command, IDataReader reader)
        {
            switch (command)
            {
                case ProtocolCommandId.Message:
                    return new MessageProtocolPacket(reader);
                case ProtocolCommandId.Hello:
                    return new HelloProtocolPacket(reader);
                case ProtocolCommandId.Goodbye:
                    return new GoodbyeProtocolPacket(reader);
                case ProtocolCommandId.Target:
                    return new TargetProtocolPacket(reader);
                case ProtocolCommandId.UserList:
                    return new UserListProtocolPacket(reader);
                case ProtocolCommandId.SendFile:
                    return new SendFileProtocolPacket(reader);
                case ProtocolCommandId.GetUserList:
                    return new GetUserListProtocolPacket();
                case ProtocolCommandId.ReKey:
                    return new ReKeyProtocolPacket(reader);
                case ProtocolCommandId.Ping:
                    return new PingProtocolPacket();
                default:
                    throw new ArgumentException(String.Format("Unsupported command {0}", command));
            }
        }
    }
}
