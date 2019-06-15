using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Client
{
    public enum PacketIncomingType
    {
        KeepAlive,
        JoinGame,
        ChatMessage,
        TimeUpdate,
        EntityEquipment,
        SpawnPosition,
        UpdateHealth,
        Respawn,
        PlayerPositionAndLook,
        HeldItemChange,
        UseBed,
        Animation,
        SpawnPlayer,
        ChunkData,
        MultiBlockChange,
        BlockChange,
        MapChunkBulk,
        UnloadChunk,
        ConfirmTransaction,
        PlayerListUpdate,
        TabCompleteResult,
        PluginMessage,
        KickPacket,
        NetworkCompressionTreshold,
        ResourcePackSend,
        UnknownPacket
    }
}
