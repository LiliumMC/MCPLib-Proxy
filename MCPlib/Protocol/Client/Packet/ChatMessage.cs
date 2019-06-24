using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Client.Packet
{
    class ChatMessage : IncomingPacket
    {
        public string Message { get; set; }
        public byte Position { get; set; }
        public ChatMessage() { }
        public ChatMessage(string Message) : this(Message, 0) { }
        public ChatMessage(string Message, byte Position)
        {
            this.Message = Message;
            this.Position = Position;
        }

        public override PacketIncomingType GetBuffer(int protocol, List<byte> cache)
        {
            cache.Clear();
            string result = String.Empty;
            if (Message.Contains(ServerData.vColorChar))
            {
                string[] data = Message.Split(ServerData.vColorChar);
                List<string> extra = new List<string>();
                result = "{\"extra\":[";
                foreach (string t in data)
                {
                    if (!string.IsNullOrEmpty(t))
                    {
                        string colorTag = ServerData.getColorTag(t[0]);
                        if (colorTag != "")
                        {
                            extra.Add("{" + string.Format("\"color\":\"{0}\",\"text\":\"{1}\"", colorTag, t.Remove(0, 1)) + "}");
                        }
                        else
                            extra.Add("{" + string.Format("\"text\":\"{0}\"", t) + "}");
                    }
                }
                result += string.Join(",", extra) + "],\"text\":\"\"}";
            }
            else
            {
                result = "{\"text\":\"" + Message + "\"}";
            }
            cache.AddRange(data.getString(result));
            if (protocol > MCVersion.MC1710Version)
                cache.Add(Position);
            return PacketIncomingType.ChatMessage;
        }

        public override void ReadBuffer(List<byte> cache, int protocol)
        {
            Message = Handler.ChatParser.ParseText(data.readNextString(cache));
            if (protocol > MCVersion.MC1710Version)
                Position = data.readNextByte(cache);
        }
    }
}
