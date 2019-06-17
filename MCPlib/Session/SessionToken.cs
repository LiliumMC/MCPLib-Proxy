using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Session
{
    class SessionToken
    {
        public byte[] SecretKey { get; set; }
        public byte[] Token { get; set; }
        public SessionToken(byte[] SecretKey, byte[] Token)
        {
            this.SecretKey = SecretKey;
            this.Token = Token;
        }
    }
}
