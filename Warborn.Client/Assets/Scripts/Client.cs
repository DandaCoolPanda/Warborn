using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 26950;
    public int myId = 0;

    public TCP tcp;
    public UDP udp;

    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) 
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }

    }

    private void Start()
    {
        tcp = new TCP();
        udp = new UDP();
    }

    public void ConnectToServer()
    {
        InitializeClientData();
        tcp.Connect();

    }
    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>
        {
            { (int)ServerPackets.welcome, ClientHandle.Welcome },
            { (int)ServerPackets.udpTest, ClientHandle.UDPTest }
        };

        Debug.Log("Initialized packets.");
    }




    #region TCP connection
    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet recievedData;
        private byte[] recieveBuffer;

        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = Client.dataBufferSize,
                SendBufferSize = Client.dataBufferSize
            };

            recieveBuffer = new byte[Client.dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult _result)
        {
            socket.EndConnect(_result);
            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            recievedData = new Packet();

            stream.BeginRead(recieveBuffer, 0, Client.dataBufferSize, RecieveCallback, null);
        }


        public void SendData(Packet _packet)
        {
            try
            {
                if(socket != null)
                {
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via TCP: {_ex}");
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

                // TODO: Handle data
                recievedData.Reset(HandleData(_data));
                stream.BeginRead(recieveBuffer, 0, Client.dataBufferSize, RecieveCallback, null);
            }
            catch (Exception ex)
            {
                // TODO: Disconnect
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
                        packetHandlers[_packetId](_packet);
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
        public UdpClient socket;
        public IPEndPoint endPoint;

        public UDP()
        {
            endPoint = new IPEndPoint(IPAddress.Parse(instance.ip), instance.port);
        }

        public void Connect(int _localPort)
        {
            socket = new UdpClient(_localPort);

            socket.Connect(endPoint);
            socket.BeginReceive(RecieveCallback, null);

            using(Packet _packet = new Packet())
            {
                SendData(_packet);
            }
        }

        public void SendData(Packet _packet)
        {
            try
            {
                _packet.InsertInt(instance.myId);
                if(socket != null)
                {
                    socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
                }

            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via UDP: {_ex}");
            }
        }

        private void RecieveCallback(IAsyncResult _result)
        {
            try
            {
                byte[] _data = socket.EndReceive(_result, ref endPoint);
                socket.BeginReceive(RecieveCallback, null);

                if(_data.Length < 4)
                {
                    //TODO: Disconnect
                    return;
                }

                HandleData(_data);

            }
            catch (Exception _ex)
            {

                throw;
            }
        }

        private void HandleData(byte[] _data)
        {
            using(Packet _packet = new Packet(_data))
            {
                int _packetLenght = _packet.ReadInt();
                _data = _packet.ReadBytes(_packetLenght);
            }

            ThreadManager.ExecuteOnMainThread(() =>
            {
                using(Packet _packet = new Packet(_data))
                {
                    int _packetId = _packet.ReadInt();
                    packetHandlers[_packetId](_packet);
                }
            });
        }

    }

    #endregion



}

