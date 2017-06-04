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
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace ChatProtocol
{
    public static class NetworkUtils
    {
        public const int DEFAULT_PORT = 12345;
        public const int BINARY_MAGIC = 0x42494e58;
        public const int TEXT_MAGIC = 0x54455854;

        const int BLOCK_SIZE = 8192;

        internal static void WriteNetworkOrder(this BinaryWriter writer, int value)
        {
            writer.Write(IPAddress.HostToNetworkOrder(value));
        }

        internal static int ReadInt32NetworkOrder(this BinaryReader reader)
        {
            return IPAddress.NetworkToHostOrder(reader.ReadInt32());
        }

        internal static byte[] ReadAllBytes(this BinaryReader reader, int length)
        {
            MemoryStream curr_data = new MemoryStream();
            int curr_len = 0;

            while (curr_len < length)
            {
                int read_len = Math.Min(length - curr_len, BLOCK_SIZE);
                byte[] data = reader.ReadBytes(read_len);
                if (data.Length == 0)
                {
                    throw new EndOfStreamException();

                }
                curr_data.Write(data, 0, data.Length);
                curr_len += data.Length;
            }

            return curr_data.ToArray();
        }

        internal static void WriteLengthBytes(this BinaryWriter writer, byte[] data)
        {
            writer.WriteNetworkOrder(data.Length);
            writer.Write(data);
        }

        internal static byte[] ReadLengthBytes(this BinaryReader reader)
        {
            int length = reader.ReadInt32NetworkOrder();
            return reader.ReadAllBytes(length);
        }

        public static async Task<byte[]> ReadAllBytesAsync(Stream stm, int length)
        {
            byte[] ba = new byte[length];
            int curr_length = 0;
            while (curr_length < length)
            {
                int read_length = await stm.ReadAsync(ba, curr_length, length - curr_length);
                if (read_length == 0)
                {
                    throw new EndOfStreamException();
                }
                curr_length += read_length;
            }
            return ba;
        }

        public static async Task<int> ReadNetworkOrderInt32Async(Stream stm)
        {
            byte[] bytes = await ReadAllBytesAsync(stm, 4);
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes, 0));
        }

        public static void WriteNetworkOrderInt32(Stream stm, int value)
        {
            byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            stm.Write(bytes, 0, bytes.Length);
        }

        public static DnsEndPoint ParseEndpoint(string endpoint)
        {
            int port = 0;
            string host = null;

            endpoint = endpoint.Trim();

            if (endpoint.Length == 0)
            {
                throw new ArgumentException("Invalid endpoint string");
            }

            int lastColon = endpoint.LastIndexOf(':');

            port = ParseInt(endpoint.Substring(lastColon + 1));
            host = endpoint.Substring(0, lastColon);

            host.Trim('[', ']');

            return new DnsEndPoint(host, port);
        }

        public static int ParseInt(string s)
        {
            if (s.StartsWith("0x"))
            {
                return int.Parse(s, NumberStyles.HexNumber);
            }
            else
            {
                return int.Parse(s);
            }
        }

    }
}
