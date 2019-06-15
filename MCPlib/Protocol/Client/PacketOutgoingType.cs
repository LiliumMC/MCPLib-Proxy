using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCPlib.Protocol.Client
{
    public enum PacketOutgoingType
    {
        KeepAlive,
        ResourcePackStatus,
        ChatMessage,
        PlayerBlockPlacement,
        ClientStatus,
        ClientSettings,
        PluginMessage,
        TabComplete,
        PlayerPosition,
        PlayerPositionAndLook,
        TeleportConfirm,
        Unknown
    }
}
