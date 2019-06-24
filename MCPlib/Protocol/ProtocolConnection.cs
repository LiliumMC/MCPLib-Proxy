using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MCPlib.Protocol.PacketLib;
using MCPlib.Protocol.Client;
using MCPlib.Protocol.Client.Packet;
using MCPlib.Crypto.Streams;
using MCPlib.Crypto;

namespace MCPlib.Protocol
{
    class ProtocolConnection:DataType
    {
        public ProtocolConnection(TcpClient Client) : this(Client, 0,null) { }
        public ProtocolConnection(TcpClient Client, int ProtocolVersion,IMinecraftCo Handle)
        {
            this.c = Client;
            this.protocolversion = ProtocolVersion;
            this.handler = Handle;
        }

        private int protocolversion;
        private int compression_treshold = 0;
        private bool login_phase = true;
        private bool encrypted = false;
        TcpClient c;
        Thread netRead;
        AesStream s;

        private IMinecraftCo handler;

        private void Receive(byte[] buffer, int start, int offset, SocketFlags f)
        {
            int read = 0;
            while (read < offset)
            {
                if(encrypted)
                    read += s.Read(buffer, start + read, offset - read);
                else
                    read += c.Client.Receive(buffer, start + read, offset - read, f);
            }
        }
        public bool Login()
        {
            byte[] protocol_version = getVarInt(protocolversion);
            string server_address = handler.getServerHost();
            byte[] server_port = BitConverter.GetBytes(handler.getServerPort()); Array.Reverse(server_port);
            byte[] next_state = getVarInt(2);
            byte[] handshake_packet = concatBytes(protocol_version, getString(server_address), server_port, next_state);
            SendPacket(0x00, handshake_packet);
            byte[] login_packet = getString(handler.getUsername());
            SendPacket(0x00, login_packet);

            int packetID = -1;
            List<byte> packetData = new List<byte>();
            while (true)
            {
                readNextPacket(ref packetID, packetData);
                if (packetID == 0x00)
                {
                    handler.OnConnectionLost(Conn.DisconnectReason.LoginRejected, readNextString(packetData));
                    return false;
                }
                else if (packetID == 0x01)//Encrypt
                {
                    string serverID = readNextString(packetData);
                    byte[] Serverkey = readNextByteArray(packetData);
                    byte[] token = readNextByteArray(packetData);
                    return SwitchToEncrypted(serverID, Serverkey, token);
                }
                else if (packetID == 0x02)//Logined
                {
                    Debug.Log("Login Success");
                    login_phase = false;
                    handler.OnLogin(packetData);
                    StartUpdating();
                    return true;
                }
                else
                {
                    if (packetID == 0x03 && login_phase)
                    {
                        if(protocolversion >= MCVersion.MC18Version)
                        {
                            compression_treshold = readNextVarInt(packetData);
                            handler.setCompression(compression_treshold);
                        }
                    }
                    else
                        handler.receivePacket(packetID, packetData);
                }
            }
        }
        public void StartUpdating()
        {
            netRead = new Thread(() =>
              {
                  try
                  {
                      int packetID = -1;
                      List<byte> packetData = new List<byte>();
                      while (true)
                      {
                          readNextPacket(ref packetID, packetData);
                          handler.receivePacket(packetID, packetData);
                      }
                  }
                  catch(Exception e)
                  {
                      if (handler != null)
                          handler.OnConnectionLost(Conn.DisconnectReason.ConnectionLost, e.Message);
                  }
              });
            netRead.Start();
        }
        public byte[] getArray(byte[] array)
        {
            if (protocolversion < MCVersion.MC18Version)
            {
                byte[] length = BitConverter.GetBytes((short)array.Length);
                Array.Reverse(length);
                return concatBytes(length, array);
            }
            else return concatBytes(getVarInt(array.Length), array);
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
        private byte[] readNextByteArray(List<byte> cache)
        {
            int len = protocolversion >= MCVersion.MC18Version
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
        private void readNextPacket(ref int packetID, List<byte> packetData)
        {
            packetData.Clear();
            int size = readNextVarIntRAW(); //Packet size
            packetData.AddRange(readDataRAW(size)); //Packet contents
            //Handle packet decompression
            if (protocolversion >= MCVersion.MC18Version
                && compression_treshold > 0)
            {
                int sizeUncompressed = readNextVarInt(packetData);
                if (sizeUncompressed != 0) // != 0 means compressed, let's decompress
                {
                    byte[] toDecompress = packetData.ToArray();
                    byte[] uncompressed = ZlibUtils.Decompress(toDecompress, sizeUncompressed);
                    packetData.Clear();
                    packetData.AddRange(uncompressed);
                }
            }

            packetID = readNextVarInt(packetData); //Packet ID
        }
        public bool SendPluginChannelPacket(string channel, byte[] data)
        {
            if (protocolversion < MCVersion.MC18Version)
            {
                byte[] length = BitConverter.GetBytes((short)data.Length);
                Array.Reverse(length);

                SendPacket(PacketOutgoingType.PluginMessage, concatBytes(getString(channel), length, data));
            }
            else
            {
                SendPacket(PacketOutgoingType.PluginMessage, concatBytes(getString(channel), data));
            }

            return true;
        }
        public void SendPacket(OutgoingPacket packet)
        {
            List<byte> buffer = new List<byte>();
            PacketOutgoingType type = packet.GetBuffer(protocolversion, buffer);
            SendPacket(type, buffer);
        }
        private void SendPacket(PacketOutgoingType packet, IEnumerable<byte> packetData)
        {
            SendPacket(handler.getProtocol().getPacketOutgoingID(packet), packetData);
        }
        public void SendPacket(int packetID, IEnumerable<byte> packetData)
        {
            //Console.Write(packetID + " ");
            byte[] the_packet = concatBytes(getVarInt(packetID), packetData.ToArray());
            if (compression_treshold > 0)
            {
                if (the_packet.Length >= compression_treshold)
                {
                    byte[] compressed_packet = ZlibUtils.Compress(the_packet);
                    the_packet = concatBytes(getVarInt(the_packet.Length), compressed_packet);
                }
                else
                {
                    byte[] uncompressed_length = getVarInt(0); //Not compressed (short packet)
                    the_packet = concatBytes(uncompressed_length, the_packet);
                }
            }
            SendRAW(concatBytes(getVarInt(the_packet.Length), the_packet));
        }
        public void SendRAW(byte[] buffer)
        {
            if(encrypted)
                s.Write(buffer, 0, buffer.Length);
            else
                c.Client.Send(buffer);
        }
        public bool SwitchToEncrypted(string serverID, byte[] Serverkey, byte[] token)
        {
            if (ServerData.OnlineMode)
            {
                var crypto = CryptoHandler.DecodeRSAPublicKey(Serverkey);
                byte[] secretKey = CryptoHandler.GenerateAESPrivateKey();
                byte[] key_enc = crypto.Encrypt(secretKey, false);
                byte[] token_enc = crypto.Encrypt(token, false);
                //Console.WriteLine(key_enc.Length + " " + token_enc.Length);

                SendPacket(0x01, concatBytes(getArray(key_enc), getArray(token_enc)));
                
                this.s = CryptoHandler.getAesStream(c.GetStream(), secretKey);
                encrypted = true;

                int packetID = -1;
                List<byte> packetData = new List<byte>();
                while (true)
                {
                    readNextPacket(ref packetID, packetData);
                    if (packetID == 0x00)
                    {
                        handler.OnConnectionLost(Conn.DisconnectReason.LoginRejected, readNextString(packetData));
                        return false;
                    }
                    else if (packetID == 0x02)//Logined
                    {
                        Debug.Log("Login Success");
                        login_phase = false;
                        handler.OnLogin(packetData);
                        StartUpdating();
                        return true;
                    }
                    else
                    {
                        if (packetID == 0x03 && login_phase)
                        {
                            if (protocolversion >= MCVersion.MC18Version)
                                compression_treshold = readNextVarInt(packetData);
                        }
                        handler.receivePacket(packetID, packetData);
                    }
                }
            }
            else
                handler.OnConnectionLost(Conn.DisconnectReason.LoginRejected, ServerData.MsgEncryptReject);
            return false;
        }
        public void doPing(string host,ushort port,ref byte[] data)
        {
            c.Connect(host,port);
            byte[] packet_id = getVarInt(0);
            byte[] protocol_version = getVarInt(-1);
            byte[] server_port = BitConverter.GetBytes(port); Array.Reverse(server_port);
            byte[] next_state = getVarInt(1);
            byte[] packet = concatBytes(packet_id, protocol_version, getString(host), server_port, next_state);
            byte[] tosend = concatBytes(getVarInt(packet.Length), packet);

            c.Client.Send(tosend, SocketFlags.None);

            byte[] status_request = getVarInt(0);
            byte[] request_packet = concatBytes(getVarInt(status_request.Length), status_request);

            c.Client.Send(request_packet, SocketFlags.None);

            int packetLength = readNextVarIntRAW();
            if (packetLength > 0)
            {
                data = readDataRAW(packetLength);
            }
            c.Close();
        }
        public void Dispose()
        {
            handler = null;
            if (netRead != null)
                netRead.Abort();
            c.Close();
        }
    }
}
