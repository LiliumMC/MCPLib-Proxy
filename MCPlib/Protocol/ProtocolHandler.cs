using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCPlib.Protocol.PacketLib;
using MCPlib.Crypto;
using MCPlib.Crypto.Streams;
using MCPlib.Protocol.Packet;
using MCPlib.Protocol.Packet.Client;
using MCPlib.Protocol.Packet.Server;

namespace MCPlib.Protocol
{
    class ProtocolHandler:DataType, IMinecraftCo
    {
        public ProtocolHandler(TcpClient client,MCServer server)
        {
            this.Client = client;
            this._Server = server;
            Lobby = Data.Servers.servers[ServerData.LobbyServer];
            Dimensions = new Dictionary<int, string>();
            Dimensions.Add(1, "default");
            Dimensions.Add(-1, "default");
            Dimensions.Add(0, "default");
        }
        private TcpClient Client;
        private AesStream EncStream;
        private ProtocolConnection Proxy;
        private MCServer _Server;
        private Data.Servers.Server Lobby;
        private MCVersion protocol;

        private string Username { get; set; }
        private ClientSettings clientSettings { get; set; }
        private Dictionary<int, string> Dimensions { get; set; }
        private bool login_phase = true;
        private bool trans_server = false;
        private int compression_treshold=0;
        private bool encrypted = false;


        private Thread cRead;

        private void Receive(byte[] buffer, int start, int offset, SocketFlags f)
        {
            int read = 0;
            while (read < offset)
            {
                if(encrypted)
                    read += EncStream.Read(buffer, start + read, offset - read);
                else
                    read += Client.Client.Receive(buffer, start + read, offset - read, f);
            }
        }
        private void DoShakeHands(object state)
        {
            if (_Server._IsStarted)
            {
                try
                {
                    int packet_len = readNextVarIntRAW();
                    if (packet_len > 0)
                    {
                        List<byte> packetData = new List<byte>(readDataRAW(packet_len));
                        int packet_id = readNextVarInt(packetData);
                        int protocol_ver = readNextVarInt(packetData);
                        string host = readNextString(packetData);
                        ushort port = readNextUShort(packetData);
                        Debug.Log(string.Format("Connected with {0}:{1}", host, port));
                        byte next_state = packetData[packetData.Count - 1];
                        if (next_state == 0x01)//Status
                        {
                            byte[] the_state = readDataRAW(readNextVarIntRAW());
                            responsePing();
                        }
                        else if (next_state == 0x02)//Login
                        {
                            getProtocol(protocol_ver);
                            List<byte> player_data = new List<byte>(readDataRAW(readNextVarIntRAW()));
                            int tmp_id = readNextVarInt(player_data);
                            string username = readNextString(player_data);
                            this.Username = username;
                            if (!ServerData.OnlineMode || StartEncrypt())
                            {
                                Debug.Log("Player " + username + " Join the game.", "Server");
                                CreateProxyBridge(Lobby);
                            }
                        }
                    }
                }
                catch
                {
                    Close();
                }
            }
        }
        private bool StartEncrypt()
        {
            CryptoHandler crypto = new CryptoHandler();

            List<byte> encryptionRequest = new List<byte>();
            string serverID = "";
            if (protocol.protocolVersion < MCVersion.MC172Version)
                serverID = "lilium-pre";
            encryptionRequest.AddRange(getString(serverID));
            encryptionRequest.AddRange(getArray(crypto.getPublic()));
            byte[] token = new byte[4];
            var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();rng.GetBytes(token);
            encryptionRequest.AddRange(getArray(token));
            SendPacket(0x01, encryptionRequest);

            List<byte> encryptResponse = new List<byte>(readDataRAW(readNextVarIntRAW()));
            if (readNextVarInt(encryptResponse) == 0x01)
            {
                List<byte> dec = new List<byte>();
                dec.AddRange(crypto.Decrypt(readNextByteArray(encryptResponse)));
                dec.RemoveRange(0, dec.Count - 16);
                byte[] key_dec = dec.ToArray();
                byte[] token_dec = token;
                
                EncStream = CryptoHandler.getAesStream(Client.GetStream(), key_dec);
                this.encrypted = true;
                return true;
            }
            return false;
        }
        private async void CreateProxyBridge(Data.Servers.Server server)
        {
            if (this.Client.Connected)
            {
                await Task.Run(() =>
                {
                    TcpClient remote = new TcpClient();
                    try
                    {
                        remote.Connect(server.Host, server.Port);
                        if (remote.Connected)
                        {
                            ProtocolConnection tmp = new ProtocolConnection(remote, server.Protocol, this);
                            if (tmp.Login(server))
                            {
                                if (Proxy != null)
                                    Proxy.Dispose();
                                Proxy = tmp;
                                if (cRead == null || !cRead.IsAlive)
                                    handlePacket();
                            }
                            else
                                tmp.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log(e.Message, "Exception");
                        if (trans_server)
                            SendMessage(string.Format(ServerData.MsgConnectFailed, e.Message));
                        else
                            this.Close();
                    }
                });
            }
        }
        private void handlePacket()
        {
            cRead = new Thread(() =>
            {
                try
                {
                    while (Client.Connected)
                    {
                        int data_len = readNextVarIntRAW();
                        int packetID = 0;
                        List<byte> packetData = new List<byte>(readDataRAW(data_len));
                        if (compression_treshold > 0)
                        {
                            int compressed_length = readNextVarInt(packetData);

                            if (compressed_length > 0)//封包已压缩
                            {
                                byte[] uncompress = ZlibUtils.Decompress(packetData.ToArray(), compressed_length);
                                packetData = new List<byte>(uncompress);
                            }
                        }
                        packetID = readNextVarInt(packetData);
                        var type = protocol.getPacketOutgoingType(packetID);

                        switch (type)
                        {
                            case PacketOutgoingType.ChatMessage:
                                string chatmsg = readNextString(packetData);
                                Debug.Log("Chat:" + chatmsg, Username);
                                if (chatmsg.StartsWith("/"))
                                {
                                    if (OnCommand(chatmsg))
                                        continue;
                                }
                                packetData = new List<byte>(getString(chatmsg));
                                break;
                            case PacketOutgoingType.ClientSettings:                       
                                clientSettings = new ClientSettings();
                                clientSettings.ReadBuffer(packetData, protocol.protocolVersion);
                                Proxy.SendPacket(clientSettings);
                                continue;
                        }
                        //Console.Write(packetID+" ");
                        Proxy.SendPacket(packetID, packetData);
                    }
                }
                catch
                {
                    Close();
                }
            });
            cRead.Start();
        }

        private void getProtocol(int protocol_ver)
        {
            switch (protocol_ver)
            {
                case 5:protocol =new MC1710(); return;
                case 340:protocol = new MC1122(); return;
            }
        }

        private void responsePing()
        {
            try
            {
                byte[] packet_ping = new byte[0];
                if (ServerData.CustomMOTD)
                {
                    string dataStr = "{\"description\":\"" + ServerData.ServerName + "\",\"players\":{\"max\":1,\"online\":0},\"version\":{\"name\":\"TEST\",\"protocol\":5}}";
                    byte[] packet_id = getVarInt(0);
                    packet_ping = concatBytes(packet_id, getString(dataStr));
                }
                else
                {
                    var tmp = new ProtocolConnection(new TcpClient());
                    tmp.doPing(Lobby.Host, Lobby.Port, ref packet_ping);
                }
                Client.Client.Send(concatBytes(getVarInt(packet_ping.Length), packet_ping));
                byte[] response = readDataRAW(readNextVarIntRAW());
                Client.Client.Send(concatBytes(getVarInt(response.Length), response));
            }
            finally
            {
                Close();
            }
        }

        public static void Handler(TcpClient client, MCServer server)
        {
            ProtocolHandler connection = new ProtocolHandler(client, server);
            ThreadPool.QueueUserWorkItem(new WaitCallback(connection.DoShakeHands));
        }
        public int readNextVarIntRAW()
        {
            int i = 0;
            int j = 0;
            int k = 0;
            byte[] tmp = new byte[1];
            while (true)
            {
                Receive(tmp, 0, 1, SocketFlags.None);
                k = tmp[0];
                i |= (k & 0x7F) << j++ * 7;
                if (j > 5) throw new OverflowException("VarInt too big");
                if ((k & 0x80) != 128) break;
            }
            return i;
        }
        public byte[] readNextByteArray(List<byte> cache)
        {
            int len = protocol.protocolVersion >= MCVersion.MC18Version
                ? readNextVarInt(cache)
                : readNextShort(cache);
            return readData(len, cache);
        }
        public byte[] readDataRAW(int length)
        {
            if (length > 0)
            {
                byte[] cache = new byte[length];
                Receive(cache, 0, length, SocketFlags.None);
                return cache;
            }
            return new byte[] { };
        }
        public byte[] getArray(byte[] array)
        {
            if (protocol.protocolVersion < MCVersion.MC18Version)
            {
                byte[] length = BitConverter.GetBytes((short)array.Length);
                Array.Reverse(length);
                return concatBytes(length, array);
            }
            else return concatBytes(getVarInt(array.Length), array);
        }
        private void SendMessage(string message)
        {
            ChatMessage msg = new ChatMessage(message);
            SendPacket(msg);
        }
        private void SendPacket(PacketIncomingType type, IEnumerable<byte> packetData)
        {
            SendPacket(protocol.getPacketIncomingID(type), packetData);
        }
        private void SendPacket(IncomingPacket packet)
        {
            List<byte> buffer = new List<byte>();
            PacketIncomingType type = packet.GetBuffer(protocol.protocolVersion, buffer);
            SendPacket(type, buffer);
        }
        private void SendPacket(int packetID, IEnumerable<byte> packetData)
        {
            byte[] the_packet = concatBytes(getVarInt(packetID), packetData.ToArray());
            if (protocol.protocolVersion> MCVersion.MC18Version && compression_treshold > 0)
            {
                if(the_packet.Length > compression_treshold)
                {
                    int sizeUncompressed = the_packet.Length;
                    the_packet = concatBytes(getVarInt(sizeUncompressed), ZlibUtils.Compress(the_packet));
                }
                else
                    the_packet = concatBytes(getVarInt(0), the_packet);
            }
            SendRAW(concatBytes(getVarInt(the_packet.Length), the_packet));
        }
        private void SendRAW(byte[] buffer)
        {
            if (encrypted)
                EncStream.Write(buffer, 0, buffer.Length);
            else
                Client.Client.Send(buffer);
        }
        private void ServerTransfer(List<string> args)
        {
            if (args.Count >= 2)
            {
                string name = args[1];
                if (Data.Servers.servers.ContainsKey(name))
                {
                    Data.Servers.Server s= Data.Servers.servers[name];
                    trans_server = true;
                    //getProtocol(s.Protocol);
                    SendMessage(string.Format(ServerData.MsgServerTransform, name));
                    CreateProxyBridge(s);
                }
                else
                    SendMessage(ServerData.MsgServerNotFound);
            }
        }
        private void Close()
        {
            //Debug.Log("Closed the Client","Connection");
            if (cRead != null)
            {
                cRead.Abort();
            }
            if (this.Client != null)
            {
                this.Client.Close();
            }
            if (this.Proxy != null)
            {
                this.Proxy.Dispose();
            }
        }
        public void receivePacket(int packetID, List<byte> packetData)
        {
            PacketIncomingType type = protocol.getPacketIncomingType(packetID);
            switch (type)
            {
                case PacketIncomingType.JoinGame:
                    
                    List<byte> tmp = new List<byte>();
                    tmp.AddRange(packetData);
                    int EntityID = readNextInt(tmp);
                    byte GameMode = readNextByte(tmp);
                    int Dimension = 0;
                    byte Difficulty = 0;
                    if (protocol.protocolVersion >= MCVersion.MC191Version)
                        Dimension = readNextInt(tmp);
                    else
                        Dimension = readNextByte(tmp);
                    if (protocol.protocolVersion < MCVersion.MC114Version)
                        Difficulty = readNextByte(tmp);
                    readNextByte(tmp);
                    string LevelType = readNextString(tmp);
                    if (Dimensions.ContainsKey(Dimension))
                        Dimensions[Dimension] = LevelType;
                    else
                        Dimensions.Add(Dimension, LevelType);
                    if (trans_server)
                    {
                        SendPacket(packetID, packetData);
                        Proxy.SendPacket(clientSettings);
                        foreach (int d in Dimensions.Keys)
                        {
                            Respawn respawnPacket = new Respawn(d, Difficulty, GameMode, Dimensions[d]);
                            SendPacket(respawnPacket);
                        }
                        trans_server = false;
                        return;
                    }
                    break;
                case PacketIncomingType.KickPacket:
                    OnConnectionLost(Conn.DisconnectReason.InGameKick, readNextString(packetData));
                    return;

            }
            //Console.Write(packetID + " ");
            SendPacket(packetID, packetData);

        }
        public string getServerHost()
        {
            return Lobby.Host;
        }

        public ushort getServerPort()
        {
            return Lobby.Port;
        }

        public void OnConnectionLost(Conn.DisconnectReason reason, string message)
        {
            switch (reason)
            {
                case Conn.DisconnectReason.ConnectionLost:
                    if (trans_server)
                    {
                        if (this.Proxy != null)
                        {
                            this.Proxy.Dispose();
                            this.Proxy = null;
                            return;
                        }
                    }
                    break;
                case Conn.DisconnectReason.InGameKick:
                    SendPacket(PacketIncomingType.KickPacket, getString(message));
                    break;
                case Conn.DisconnectReason.LoginRejected:
                    if(!login_phase)
                        SendPacket(PacketIncomingType.ChatMessage, getString(message));
                    else
                        SendPacket(0x00, getString(message));
                    break;
            }
            Debug.Log("Connection Lost: " + message,Username);
            this.Close();
        }
        public void OnLogin(List<byte> login_packet)
        {
            if (login_phase)
            {
                SendPacket(0x02, login_packet);
                login_phase = false;
            }
        }

        public bool OnCommand(string command)
        {
            if (command.StartsWith("/server"))
            {
                if (Command.hasArg(command))
                {
                    List<string> args = Command.getArg(command);
                    switch (args[0])
                    {
                        case "tp":
                            ServerTransfer(args);
                            return true;
                    }
                    return true;
                }      
            }
            return false;
        }
        public MCVersion getProtocol()
        {
            return protocol;
        }
        public string getUsername()
        {
            return Username;
        }
        public void setCompression(int threshold)
        {
            if (login_phase)
            {
                SendPacket(0x03, getVarInt(threshold));
                compression_treshold = threshold;
            }
        }
    }
}
