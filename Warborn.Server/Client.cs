using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Warborn.Server
{
    public class Client
    {
        public int id;
        public TCP tcp;
        public UDP udp;

        public Client(int _id)
        {
            id = _id;
            tcp = new TCP(id);
            udp = new UDP(id);
        }

        #region TCP connection
        public class TCP
        {
            /*
             * Data buffer over TCP is going to have 4MB
             * Serving for both SEND and RECIEVE
             */
            public static int dataBufferSize = 4096;

            // TcpClient socket retrieved from connection of the TcpListener inside of Server class logic
            public TcpClient socket;

            // Unique Id of TCP Connection retrieved inside of Server class
            private readonly int id;

            private NetworkStream stream;
            private byte[] recieveBuffer;
            private Packet recievedData;

            public TCP(int _id)
            {
                id = _id;
            }

            public void Connect(TcpClient _socket)
            {
                socket = _socket;
                socket.ReceiveBufferSize = dataBufferSize;
                socket.SendBufferSize = dataBufferSize;

                stream = socket.GetStream();
                recievedData = new Packet();

                recieveBuffer = new byte[dataBufferSize];
                stream.BeginRead(recieveBuffer, 0, dataBufferSize, RecieveCallback, null);

                ServerSend.Welcome(id, $"{id} client, welcome to the Server!");
            }

            public void SendData(Packet _packet)
            {
                try
                {
                    if (socket != null)
                    {
                        stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                    }
                }
                catch (Exception _ex)
                {
                    Console.WriteLine($"Error sending data to player {id} via TCP: {_ex}");
                }
            }

            private void RecieveCallback(IAsyncResult _result)
            {
                try
                {
                    int _byteLenght = stream.EndRead(_result);
                    if (_byteLenght <= 0)
                    {
                        // TODO: Disconnect client
                        return;
                    }

                    byte[] _data = new byte[_byteLenght];
                    Array.Copy(recieveBuffer, _data, _byteLenght);

                    recievedData.Reset(HandleData(_data));
                    // TODO: Handle data
                    stream.BeginRead(recieveBuffer, 0, dataBufferSize, RecieveCallback, null);
                }
                catch (Exception _ex)
                {
                    Console.WriteLine($"Error recieving TCP data: {_ex}");
                    // TODO: Disconnect client
                }
            }

            private bool HandleData(byte[] _data)
            {
                int _packetLenght = 0;
                recievedData.SetBytes(_data);

                // Making sure that we need to create a packet,
                // because the information sent acrross the network always starts with INT(4-bytes) which is the lenght of the packet
                if (recievedData.UnreadLength() >= 4)
                {
                    _packetLenght = recievedData.ReadInt();
                    if (_packetLenght <= 0)
                    {
                        return true;
                    }
                }

                while (_packetLenght > 0 && _packetLenght <= recievedData.UnreadLength())
                {
                    byte[] _packetBytes = recievedData.ReadBytes(_packetLenght);
                    ThreadManager.ExecuteOnMainThread(() =>
                    {
                        using (Packet _packet = new Packet(_packetBytes))
                        {
                            int _packetId = _packet.ReadInt();
                            Server.packetHandlers[_packetId](id, _packet);
                        }
                    });

                    _packetLenght = 0;

                    if (recievedData.UnreadLength() >= 4)
                    {
                        _packetLenght = recievedData.ReadInt();
                        if (_packetLenght <= 0)
                        {
                            return true;
                        }
                    }
                }

                if (_packetLenght <= 1)
                {
                    return true;
                }

                return false;
            }


        }
        #endregion

        #region UDP connection
        public class UDP
        {
            public IPEndPoint endPoint;
            private int id;

            public UDP(int _id)
            {
                id = _id;
            }

            public void Connect(IPEndPoint _endPpoint)
            {
                endPoint = _endPpoint;

                ServerSend.UDPTest(id);
            }

            public void SendData(Packet _packet)
            {
                Server.SendUDPData(endPoint, _packet);
            }

            public void HandleData(Packet _packetData)
            {
                int _packetLenght = _packetData.ReadInt();
                byte[] _packetBytes = _packetData.ReadBytes(_packetLenght);

                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        Server.packetHandlers[_packetId](id, _packet);
                    }
                });

            }
        }
        #endregion
    }
}
