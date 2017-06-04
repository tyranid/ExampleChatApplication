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

namespace ChatProtocol
{
    public sealed class XorStream : Stream
    {
        private Stream _baseStream;
        private byte _xorKey;

        public Stream BaseStream { get { return _baseStream; } }

        public byte XorKey { get { return _xorKey; } set { _xorKey = value; } }

        public XorStream(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanRead
        {
            get { return _baseStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _baseStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _baseStream.CanWrite; }
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override long Length
        {
            get { return _baseStream.Length; }
        }

        public override long Position
        {
            get
            {
                return _baseStream.Position;
            }
            set
            {
                _baseStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int len = _baseStream.Read(buffer, offset, count);

            for (int i = 0; i < len; ++i)
            {
                buffer[i + offset] = (byte)(buffer[i + offset] ^ _xorKey);
            }

            return len;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] temp = new byte[count];

            for (int i = 0; i < count; i++)
            {
                temp[i] = (byte)(buffer[i] ^ _xorKey);
            }

            _baseStream.Write(temp, 0, temp.Length);
        }

        public override int ReadTimeout
        {
            get
            {
                return _baseStream.ReadTimeout;
            }
            set
            {
                _baseStream.ReadTimeout = value;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return _baseStream.CanTimeout;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return _baseStream.WriteTimeout;
            }
            set
            {
                _baseStream.WriteTimeout = value;
            }
        }

        protected override void Dispose(bool disposing)
        {
            _baseStream?.Dispose();
        }
    }
}
