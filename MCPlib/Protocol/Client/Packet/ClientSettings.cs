using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Client.Packet
{
    class ClientSettings
    {
        public string language { get; set; }
        public byte viewDistance { get; set; }
        public byte difficulty { get; set; }
        public byte chatMode { get; set; }
        public bool chatColors { get; set; }
        public byte skinParts { get; set; }
        public byte mainHand { get; set; }
        public ClientSettings(string language, byte viewDistance, byte difficulty, byte chatMode, bool chatColors, byte skinParts, byte mainHand)
        {
            this.language = language;
            this.viewDistance = viewDistance;
            this.difficulty = difficulty;
            this.chatMode = chatMode;
            this.chatColors = chatColors;
            this.skinParts = skinParts;
            this.mainHand = mainHand;
        }
    }
}
