using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SJTU.IOTLab.ManTracking
{
    class Transport
    {
        private const IPAddress IP = IPAddress.Parse("127.0.0.1");
        private const int PORT = 4399;
        private Socket socket = null;

        private Socket ConnectSocket()
        {
            // Create a socket connection with the specified server and port.
            IPEndPoint ipe = new IPEndPoint(IP, PORT);
            socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(ipe);

            return socket;
        }

        // This method requests the home page content for the specified server.
        private int Send(string data)
        {
            if (socket == null)
                socket = ConnectSocket();
            if (socket == null)
            {
                Console.WriteLine("Connection Failure.");
                return 500;
            }

            Byte[] bytesSent = Encoding.ASCII.GetBytes(data);
            Byte[] bytesReceived = new Byte[256];

            // Send request to the server.
            return socket.Send(bytesSent, bytesSent.Length, 0);
        }
    }
}
