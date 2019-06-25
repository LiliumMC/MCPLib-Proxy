using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Packet.Server
{
    class ClientSettings:OutgoingPacket
    {
        public string language { get; set; }
        public byte viewDistance { get; set; }
        public byte difficulty { get; set; }
        public byte chatMode { get; set; }
        public bool chatColors { get; set; }
        public byte skinParts { get; set; }
        public byte mainHand { get; set; }
        public override void ReadBuffer(List<byte> cache,int protocol)
        {
            this.language = data.readNextString(cache);
            this.viewDistance = data.readNextByte(cache);
            this.chatMode = (protocol >= MCVersion.MC19Version) ? (byte)data.readNextVarInt(cache) : data.readNextByte(cache);
            this.chatColors = data.readNextByte(cache) == 1 ? true : false;
            this.difficulty = 0;
            if (protocol < MCVersion.MC18Version)
                difficulty = data.readNextByte(cache);
            this.skinParts = data.readNextByte(cache);
            this.mainHand = 0;
            if (protocol >= MCVersion.MC19Version)
            {
                mainHand = (byte)data.readNextVarInt(cache);
            }
        }
        public override PacketOutgoingType GetBuffer(int protocol,List<byte> fields)
        {
            fields.Clear();
            fields.AddRange(data.getString(language));
            fields.Add(viewDistance);
            fields.AddRange(protocol >= MCVersion.MC19Version
                ? data.getVarInt(chatMode)
                : new byte[] { chatMode });
            fields.Add(chatColors ? (byte)1 : (byte)0);
            if (protocol < MCVersion.MC18Version)
            {
                fields.Add(difficulty);
                fields.Add((byte)(skinParts & 0x1)); //show cape
            }
            else fields.Add(skinParts);
            if (protocol >= MCVersion.MC19Version)
                fields.AddRange(data.getVarInt(mainHand));
            return PacketOutgoingType.ClientSettings;
        }
    }
}
