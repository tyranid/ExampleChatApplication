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
using System.Text;

namespace ChatProtocol
{
    class TextDataWriter : IDataWriter
    {
        private List<string> _parts;

        private string EscapeString(string s)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '@')
                {
                    builder.Append("@@");
                }
                else if (c <= ' ')
                {
                    builder.AppendFormat("@{0}@", (int)c);
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        public TextDataWriter()
        {
            _parts = new List<string>();
        }

        public void Write(string str)
        {
            _parts.Add(EscapeString(str));
        }

        public void Write(int i)
        {
            _parts.Add(i.ToString());
        }

        public void Write(bool b)
        {
            _parts.Add(b.ToString());
        }

        public void Write(byte[] ba)
        {
            _parts.Add(Convert.ToBase64String(ba));
        }

        public override string ToString()
        {
            return String.Join(" ", _parts);
        }

        public void Write(byte b)
        {
            _parts.Add(String.Format("{0:X}", b));
        }
    }
}
