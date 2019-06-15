using MCPlib.Protocol.PacketLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCPlib.Protocol.Client;

namespace MCPlib.Protocol
{
    abstract class MCVersion
    {
        private Dictionary<int, PacketIncomingType> IncomingType { get; set; }
        private Dictionary<int,PacketOutgoingType> OutgoingType { get; set; }
        public abstract int protocolVersion { get; }
        public abstract PacketIncomingType getPacketIncomingType(int packetID);
        public abstract PacketOutgoingType getPacketOutgoingType(int packetID);
        public abstract int getPacketIncomingID(PacketIncomingType packet);
        public abstract int getPacketOutgoingID(PacketOutgoingType packet);


        public const int MC172Version = 4;
        public const int MC1710Version = 5;
        public const int MC18Version = 47;
        public const int MC19Version = 107;
        public const int MC191Version = 108;
        public const int MC110Version = 210;
        public const int MC111Version = 315;
        public const int MC17w13aVersion = 318;
        public const int MC112pre5Version = 332;
        public const int MC17w31aVersion = 336;
        public const int MC17w45aVersion = 343;
        public const int MC17w46aVersion = 345;
        public const int MC17w47aVersion = 346;
        public const int MC18w01aVersion = 352;
        public const int MC18w06aVersion = 357;
        public const int MC113pre4Version = 386;
        public const int MC113pre7Version = 389;
        public const int MC113Version = 393;
        public const int MC114Version = 477;
        public const int MC1142Version = 485;
    }
}
