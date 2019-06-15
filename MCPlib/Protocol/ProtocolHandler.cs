using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCPlib.Protocol.PacketLib;
using MCPlib.Protocol.Client;
using MCPlib.Protocol.Client.Packet;

namespace MCPlib.Protocol
{
    class ProtocolHandler:IMinecraftCo
    {
        public ProtocolHandler(Socket client,MCServer server)
        {
            this.Client = client;
            this._Server = server;
            Lobby = Data.Servers.servers[ServerData.LobbyServer];
            Dimensions = new Dictionary<int, string>();
            Dimensions.Add(1, "default");
            Dimensions.Add(-1, "default");
            Dimensions.Add(0, "default");
        }
        private Socket Client;
        private ProtocolConnection Proxy;
        private MCServer _Server;
        private Data.Servers.Server Lobby;
        private MCVersion protocol;

        private ClientSettings clientSettings { get; set; }
        private Dictionary<int, string> Dimensions { get; set; }
        private bool login_phase = true;
        private bool trans_server = false;
        private int compression_treshold=0;



        private Thread cRead;

        private void Receive(byte[] buffer, int start, int offset, SocketFlags f)
        {
            int read = 0;
            while (read < offset)
            {
                read += Client.Receive(buffer, start + read, offset - read, f);
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
                        if (next_state == 0x01)//PING
                        {
                            byte[] the_state = readDataRAW(readNextVarIntRAW());
                            responsePing();
                        }
                        else if (next_state == 0x02)//Player
                        {
                            getProtocol(protocol_ver);
                            List<byte> player_data = new List<byte>(readDataRAW(readNextVarIntRAW()));
                            int tmp_id = readNextVarInt(player_data);
                            string username = readNextString(player_data);

                            Debug.Log("Player " + username + " Join the game.", "Server");
                            CreateProxyBridge(Lobby, username);
                        }
                    }
                }
                catch
                {
                    Close();
                }
            }
        }
        private void CreateProxyBridge(Data.Servers.Server server, string name)
        {
            if (this.Client.Connected)
            {
                TcpClient remote = new TcpClient();
                try
                {
                    remote.Connect(server.Host,server.Port);
                    if (remote.Connected)
                    {
                        Proxy = new ProtocolConnection(remote, server.Protocol, this);
                        if (Proxy.Login(name))
                        {
                            if (cRead==null || !cRead.IsAlive)
                                handlePacket();
                        }                
                    }
                }catch(Exception e)
                {
                    Debug.Log(e.Message, "Exception");
                    this.Close();
                }
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
                            int packet_len = readNextVarInt(packetData);

                            if (packet_len > 0)//封包已压缩
                            {
                                byte[] uncompress = ZlibUtils.Decompress(packetData.ToArray());
                                packetData = new List<byte>(uncompress);
                                packetID = readNextVarInt(packetData);
                            }
                            else
                            {
                                packetID = readNextVarInt(packetData);
                            }
                        }
                        else
                        {
                            packetID = readNextVarInt(packetData);
                        }
                        var type = protocol.getPacketOutgoingType(packetID);
                        if (packetID == 0x03 && login_phase)
                        {
                            if (protocol.protocolVersion >= MCVersion.MC18Version)
                                compression_treshold = readNextVarInt(packetData);
                        }
                        switch (type)
                        {
                            case PacketOutgoingType.ChatMessage:
                                string chatmsg = readNextString(packetData);
                                Debug.Log("Chat:" + chatmsg, Proxy.Username);
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
                    ProtocolConnection.doPing(Lobby.Host, Lobby.Port, ref packet_ping);
                }
                Client.Send(concatBytes(getVarInt(packet_ping.Length), packet_ping));
                byte[] response = readDataRAW(readNextVarIntRAW());
                Client.Send(concatBytes(getVarInt(response.Length), response));
            }
            catch
            {
                Close();
            }
        }

        public static void Handler(Socket client, MCServer server)
        {
            ProtocolHandler connection = new ProtocolHandler(client, server);
            ThreadPool.QueueUserWorkItem(new WaitCallback(connection.DoShakeHands));
        }
        private int readNextVarIntRAW()
        {
            int n = 0;
            int r = 0;
            byte[] tmp = new byte[1];
            do
            {
                Receive(tmp, 0, 1, SocketFlags.None);
                r |= ((tmp[0] & 0x7F) << (7 * n));
                n++;
                if (n > 5)
                    throw new OverflowException("VarInt is too big");
            } while ((r & 128) != 0);
            return r;
        }
        private static int readNextVarInt(List<byte> cache)
        {
            int n = 0;
            int r = 0;
            int k = 0;
            do
            {
                k = readNextByte(cache);
                r |= ((k & 0x7F) << (7 * n));
                n++;
                if (n > 5)
                    throw new OverflowException("VarInt is too big");
            } while ((r & 128) != 0);
            return r;
        }
        private static byte readNextByte(List<byte> cache)
        {
            byte result = cache[0];
            cache.RemoveAt(0);
            return result;
        }
        private static string readNextString(List<byte> cache)
        {
            int length = readNextVarInt(cache);
            if (length > 0)
            {
                return Encoding.UTF8.GetString(readData(length, cache));
            }
            else return "";
        }
        private byte[] readDataRAW(int offset)
        {
            if (offset > 0)
            {
                try
                {
                    byte[] cache = new byte[offset];
                    Receive(cache, 0, offset, SocketFlags.None);
                    return cache;
                }
                catch (OutOfMemoryException) { }
            }
            return new byte[] { };
        }
        private static byte[] readData(int offset, List<byte> cache)
        {
            byte[] result = cache.Take(offset).ToArray();
            cache.RemoveRange(0, offset);
            return result;
        }
        private static short readNextShort(List<byte> cache)
        {
            byte[] rawValue = readData(2, cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt16(rawValue, 0);
        }
        private static int readNextInt(List<byte> cache)
        {
            byte[] rawValue = readData(4, cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt32(rawValue, 0);
        }
        private static ushort readNextUShort(List<byte> cache)
        {
            byte[] rawValue = readData(2, cache);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToUInt16(rawValue, 0);
        }
        private static byte[] concatBytes(params byte[][] bytes)
        {
            List<byte> result = new List<byte>();
            foreach (byte[] array in bytes)
                result.AddRange(array);
            return result.ToArray();
        }

        private static byte[] getVarInt(int paramInt)
        {
            List<byte> bytes = new List<byte>();
            while ((paramInt & -128) != 0)
            {
                bytes.Add((byte)(paramInt & 127 | 128));
                paramInt = (int)(((uint)paramInt) >> 7);
            }
            bytes.Add((byte)paramInt);
            return bytes.ToArray();
        }
        private static byte[] getString(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            return concatBytes(getVarInt(bytes.Length), bytes);
        }
        private void SendMessage(string message)
        {
            if (String.IsNullOrEmpty(message))
                return;
            string result = String.Empty;
            if (message.Contains(ServerData.vColorChar))
            {
                string[] data = message.Split(ServerData.vColorChar);
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
                result = "{\"text\":\"" + message + "\"}";
            }
            SendPacket(PacketIncomingType.ChatMessage, getString(result));
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
            if (protocol.protocolVersion> MCVersion.MC18Version && the_packet.Length > compression_treshold)
            {
                int sizeUncompressed = the_packet.Length;
                the_packet = concatBytes(getVarInt(sizeUncompressed), ZlibUtils.Compress(the_packet));
            }
            SendRAW(concatBytes(getVarInt(the_packet.Length), the_packet));
        }
        private void SendRAW(byte[] buffer)
        {
            Client.Send(buffer);
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
                    getProtocol(s.Protocol);
                    Proxy.Dispose();
                    SendMessage(string.Format(ServerData.MsgServerTransform, name));
                    CreateProxyBridge(s,Proxy.Username);
                }
                else
                    SendMessage(ServerData.MsgServerNotFound);
            }
        }
        private void Close()
        {
            Debug.Log("Closed the Client","Connection");
            if (cRead != null)
            {
                cRead.Abort();
            }
            if (this.Client != null)
            {
                this.Client.Close(5);
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
                        foreach(int d in Dimensions.Keys)
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
                        SendPacket(PacketIncomingType.KickPacket, getString(message));
                    else
                        SendPacket(0x01, getString(message));
                    break;
            }
            Debug.Log("Connection Lost: " + message);
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
    }
}
