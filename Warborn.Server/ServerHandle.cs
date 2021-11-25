using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warborn.Server
{
    public class ServerHandle
    {
        public static void WelcomeRecieved(int _fromClient, Packet _packet)
        {
            int _clientIdCheck = _packet.ReadInt();
            string _username = _packet.ReadString();

            Console.WriteLine($"{Server.clients[_fromClient].tcp.socket.Client.RemoteEndPoint} connected successfully and is now player: {_clientIdCheck}");

            if(_fromClient != _clientIdCheck)
            {
                Console.WriteLine($"Player \"{_username}\" (ID: {_fromClient} has assumed the wrong client ID({_clientIdCheck}))!");
            }

            // TODO: Send player into game
        }

        public static void UDPTestRecieved(int _fromClient, Packet _packet)
        {
            string _msg = _packet.ReadString();

            Console.WriteLine($"Recieved packet via UDP. Contains message: {_msg}");
        }
    }
}
