using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SJTU.IOTLab.ManTracking.Helper
{
    class Transporter
    {
        private const string IP = "192.168.1.109";
        private const int PORT = 4399;
        private Socket socket = null;
        public STATUS status = STATUS.DISCONNECTED;
        public enum STATUS { DISCONNECTED = -1, CONNECTED = 0 }

        public STATUS Connect()
        {
            // Create a socket connection with the specified server and port.
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(IP), PORT);
            socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(ipe);
            }
            catch
            {
                return status = STATUS.DISCONNECTED;
            }

            return status = STATUS.CONNECTED;
        }

        // This method requests the home page content for the specified server.
        public int Send(string data)
        {
            if (status == STATUS.DISCONNECTED)
            {
                Connect();
                if (status == STATUS.DISCONNECTED)
                {
                    Console.WriteLine("Connection Failure.");
                    return 500;
                }
            }

            Byte[] bytesSent = Encoding.ASCII.GetBytes(data);
            Byte[] bytesReceived = new Byte[256];

            // Send request to the server.
            return socket.Send(bytesSent, bytesSent.Length, 0);
        }
    }
}
