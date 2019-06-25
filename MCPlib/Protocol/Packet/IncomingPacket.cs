using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Packet
{
    abstract class IncomingPacket
    {
        public abstract PacketIncomingType GetBuffer(int protocol, List<byte> cache);
        public abstract void ReadBuffer(List<byte> cache, int protocol);
        protected DataType data = new DataType();
    }
}
