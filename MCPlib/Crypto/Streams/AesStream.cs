using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Crypto.Streams
{
    class AesStream:Stream
    {
        CipherStream cstream;
        public AesStream(Stream stream, byte[] key)
        {
            BaseStream = stream;
            BufferedBlockCipher enc = GenerateAES(key, true);
            BufferedBlockCipher dec = GenerateAES(key, false);
            cstream = new CipherStream(stream, dec, enc);
        }
        public Stream BaseStream { get; set; }
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int ReadByte()
        {
            byte[] temp = new byte[1];
            Read(temp, 0, 1);
            return temp[0];
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return cstream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void WriteByte(byte b)
        {
            Write(new byte[] { b }, 0, 1);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            cstream.Write(buffer, offset, count);
        }
        private BufferedBlockCipher GenerateAES(byte[] key, bool forEncryption)
        {
            BufferedBlockCipher cipher = new BufferedBlockCipher(new CfbBlockCipher(new AesEngine(), 8));
            cipher.Init(forEncryption, new ParametersWithIV(new KeyParameter(key), key));
            return cipher;
        }
    }
}
