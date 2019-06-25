using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.PacketLib
{
    class MC1710 : MCVersion
    {
        public override int protocolVersion { get
            {
                return 5;
            } }
        public override PacketIncomingType getPacketIncomingType(int packetID)
        {
            switch (packetID)
            {
                case 0x00:return PacketIncomingType.KeepAlive;
                case 0x01:return PacketIncomingType.JoinGame;
                case 0x02:return PacketIncomingType.ChatMessage;
                case 0x05:return PacketIncomingType.SpawnPosition;
                case 0x07:return PacketIncomingType.Respawn;
                case 0x0C:return PacketIncomingType.SpawnPlayer;
                case 0x21: return PacketIncomingType.ChunkData;
                case 0x22: return PacketIncomingType.MultiBlockChange;
                case 0x32:return PacketIncomingType.ConfirmTransaction;
                case 0x3F: return PacketIncomingType.PluginMessage;
                case 0x40: return PacketIncomingType.KickPacket;
                case 0x46: return PacketIncomingType.NetworkCompressionTreshold;
                default:return PacketIncomingType.UnknownPacket;
            }
        }
        public override int getPacketIncomingID(PacketIncomingType packet)
        {
            switch (packet)
            {
                case PacketIncomingType.KeepAlive: return 0x00;
                case PacketIncomingType.JoinGame: return 0x01;
                case PacketIncomingType.ChatMessage: return 0x02;
                case PacketIncomingType.Respawn:return 0x07;
                case PacketIncomingType.Entity:return 0x14;
                case PacketIncomingType.PluginMessage: return 0x3F;
                case PacketIncomingType.KickPacket: return 0x40;
                default: return 0xFF;
            }
        }

        public override int getPacketOutgoingID(PacketOutgoingType packet)
        {
            switch (packet)
            {
                case PacketOutgoingType.KeepAlive:return 0x00;
                case PacketOutgoingType.ChatMessage:return 0x01;
                case PacketOutgoingType.PlayerBlockPlacement:return 0x08;
                case PacketOutgoingType.ClientSettings:return 0x15;
                default:return 0xFF;
            }
        }

        public override PacketOutgoingType getPacketOutgoingType(int packetID)
        {
            switch (packetID)
            {
                case 0x00:return PacketOutgoingType.KeepAlive;
                case 0x01:return PacketOutgoingType.ChatMessage;
                case 0x08:return PacketOutgoingType.PlayerBlockPlacement;
                case 0x15:return PacketOutgoingType.ClientSettings;
                default:return PacketOutgoingType.Unknown;
            }
        }
    }
}
