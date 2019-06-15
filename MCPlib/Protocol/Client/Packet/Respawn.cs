using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Client.Packet
{
    class Respawn:IncomingPacket
    {
        public int Dimension { get; set; }
        public byte Difficulty { get; set; }
        public byte Gamemode { get; set; }
        public string LevelType { get; set; }
        public Respawn(int Dimension, byte Difficulty, byte Gamemode, string LevelType)
        {
            this.Dimension = Dimension;
            this.Difficulty = Difficulty;
            this.Gamemode = Gamemode;
            this.LevelType = LevelType;
        }
        public Respawn() { }
        public override PacketIncomingType GetBuffer(int protocol, List<byte> cache)
        {
            cache.Clear();
            byte[] dimension = BitConverter.GetBytes(Dimension);Array.Reverse(dimension);
            cache.AddRange(dimension);
            if (protocol < MCVersion.MC114Version)
                cache.Add(Difficulty);
            cache.Add(Gamemode);
            cache.AddRange(data.getString(LevelType));
            return PacketIncomingType.Respawn;
        }

        public override void ReadBuffer(List<byte> cache, int protocol)
        {
            this.Dimension = data.readNextInt(cache);
            if (protocol < MCVersion.MC114Version)
                Difficulty=data.readNextByte(cache);           // Difficulty - 1.13 and below
            Gamemode = data.readNextByte(cache);
            LevelType = data.readNextString(cache);
        }
    }
}
