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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChatProtocol
{
    class TextDataReader : IDataReader
    {
        Queue<string> _parts;

        public TextDataReader(string line)
        {
            _parts = new Queue<string>(line.Split(' '));
        }

        public bool ReadBoolean()
        {
            return bool.Parse(_parts.Dequeue());
        }

        public byte[] ReadBytes()
        {
            return Convert.FromBase64String(_parts.Dequeue());
        }

        public int ReadInt32()
        {
            return int.Parse(_parts.Dequeue());
        }

        private string UnescapeString(string str)
        {
            if (!str.Contains("@"))
            {
                return str;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < str.Length; ++i)
            {
                if (str[i] == '@')
                {
                    int next = str.IndexOf('@', i + 1);
                    if (next == -1)
                    {
                        throw new ArgumentException("Invalid escape string detected");
                    }
                    if (next == i + 1)
                    {
                        builder.Append('@');
                    }
                    else
                    {
                        string s = str.Substring(i + 1, next - i - 1);
                        int c = int.Parse(s);
                        builder.Append((char)c);
                    }
                    i = next;
                }
                else
                {
                    builder.Append(str[i]);
                }
            }
            return builder.ToString();
        }

        public string ReadString()
        {
            return UnescapeString(_parts.Dequeue());
        }

        public byte ReadByte()
        {
            return byte.Parse(_parts.Dequeue(), NumberStyles.HexNumber);
        }
    }
}
