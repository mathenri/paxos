using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace _550_Assignment2
{
    /*
     * The message reciever contains a server socket that listens for incomming messages
     */
    public class MessageReceiver
    {
        private bool isNodeAlive;
        private Socket serverSocket;
        private Action<Message> messageReceived;
        Thread hostThread;

        public MessageReceiver(Action<Message> messageReceived, Node server)
        {
            this.messageReceived = messageReceived;
            isNodeAlive = server.getNodeLifeStatus();
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress hostIP = IPAddress.Parse("127.0.0.1");
                IPEndPoint ep = new IPEndPoint(hostIP, server.getPort());
                serverSocket.Bind(ep);
                serverSocket.Listen(1000);
            }
            catch (IOException)
            {
                Console.WriteLine("IO Exception occured while creating listening socket for the server");
            }
        }

        /* 
         * Start listening for messages and pass them on to the messageReciever method
         */
        public void Run()
        {
            BinaryFormatter objectBinaryFormatter = new BinaryFormatter();
            Socket objectSocket = null;
            NetworkStream objectNetworkStream = null;
            using (serverSocket)
            {
                while (isNodeAlive)
                {
                    try
                    {
                        using (objectSocket = serverSocket.Accept())
                        {
                            
                            if (isNodeAlive)
                            {
                                objectNetworkStream = new NetworkStream(objectSocket, false);
                                this.messageReceived((Message)objectBinaryFormatter.Deserialize(objectNetworkStream));
                            }
                        }
                    }
                    catch (IOException e)
                    {
                        Console.WriteLine("IOException while trying to accept connection!");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        public void Kill()
        {
            isNodeAlive = false;
            Console.WriteLine("Server is stopped");
        }

        public void start()
        {
            ThreadStart runProcedure = new ThreadStart(Run);
            hostThread = new Thread(runProcedure);
            hostThread.Start();
        }
    }
}