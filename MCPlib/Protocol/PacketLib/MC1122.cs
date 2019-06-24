using MCPlib.Protocol.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.PacketLib
{
    class MC1122:MCVersion
    {
        public override int protocolVersion
        {
            get
            {
                return 340;
            }
        }
        public override PacketIncomingType getPacketIncomingType(int packetID)
        {
            switch (packetID)
            {
                case 0x0E:return PacketIncomingType.TabCompleteResult;
                case 0x0F:return PacketIncomingType.ChatMessage;
                case 0x11:return PacketIncomingType.ConfirmTransaction;
                case 0x18:return PacketIncomingType.PluginMessage;
                case 0x1A:return PacketIncomingType.Disconnect;
                case 0x1F:return PacketIncomingType.KeepAlive;
                case 0x20:return PacketIncomingType.ChunkData;
                case 0x23:return PacketIncomingType.JoinGame;
                case 0x35:return PacketIncomingType.Respawn;
                case 0x46:return PacketIncomingType.SpawnPosition;
                default: return PacketIncomingType.UnknownPacket;
            }
        }
        public override int getPacketIncomingID(PacketIncomingType packet)
        {
            switch (packet)
            {
                case PacketIncomingType.TabCompleteResult:return 0x0E;
                case PacketIncomingType.ChatMessage:return 0x0F;
                case PacketIncomingType.ConfirmTransaction:return 0x11;
                case PacketIncomingType.PluginMessage:return 0x18;
                case PacketIncomingType.Disconnect:return 0x1A;
                case PacketIncomingType.KeepAlive:return 0x1F;
                case PacketIncomingType.ChunkData:return 0x20;
                case PacketIncomingType.JoinGame:return 0x23;
                case PacketIncomingType.Respawn:return 0x35;
                case PacketIncomingType.SpawnPosition:return 0x46;
                default: return 0xFF;
            }
        }

        public override int getPacketOutgoingID(PacketOutgoingType packet)
        {
            switch (packet)
            {
                case PacketOutgoingType.TabComplete:return 0x01;
                case PacketOutgoingType.ChatMessage:return 0x02;
                case PacketOutgoingType.ClientStatus:return 0x03;
                case PacketOutgoingType.ClientSettings:return 0x04;
                case PacketOutgoingType.PluginMessage:return 0x09;
                case PacketOutgoingType.KeepAlive:return 0x0B;
                case PacketOutgoingType.PlayerPositionAndLook:return 0x0E;
                case PacketOutgoingType.ResourcePackStatus:return 0x18;
                default: return 0xFF;
            }
        }

        public override PacketOutgoingType getPacketOutgoingType(int packetID)
        {
            switch (packetID)
            {
                case 0x01:return PacketOutgoingType.TabComplete;
                case 0x02:return PacketOutgoingType.ChatMessage;
                case 0x03:return PacketOutgoingType.ClientStatus;
                case 0x04:return PacketOutgoingType.ClientSettings;
                case 0x09:return PacketOutgoingType.PluginMessage;
                case 0x0B:return PacketOutgoingType.KeepAlive;
                case 0x0E:return PacketOutgoingType.PlayerPositionAndLook;
                case 0x18:return PacketOutgoingType.ResourcePackStatus;
                default: return PacketOutgoingType.Unknown;
            }
        }
    }
}
