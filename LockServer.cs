using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _550_Assignment2
{

    public enum LockServerState
    {
        STARTED,
        LOCKED,
        UNLOCKED,
        TERMINATED
    }

    public enum Command
    {
        LOCK,
        UNLOCK,
        TERMINATE,
        NONE,
    }

    public class LockServer
    {
        /* These variables contains the state of the lock server */
        int clientWithLock;
        Queue<int> waitingClients;
        LockServerState currentState;
        String clientMessage;

        public LockServer()
        {
            clientWithLock = -1;
            waitingClients = new Queue<int>();
            currentState = LockServerState.STARTED;
            PrintLockServerInformation();
        }

        public Tuple<int,string, int, string> ExecuteCommand(Command inputCommand, int requestingClient)
        {
            if (inputCommand == Command.NONE) {
                return new Tuple<int,string, int, string>(-1,null, -1, null);
            }
            if (currentState == LockServerState.TERMINATED)
            {
                clientMessage = " The servers are offline. Please try again later \n";

            }
            else if (currentState == LockServerState.UNLOCKED)
            {
                if (inputCommand == Command.TERMINATE)
                {
                    currentState = LockServerState.TERMINATED;
                    clientMessage = "Servers are terminated \n";
                }
                else if (inputCommand == Command.LOCK)
                {
                    clientWithLock = requestingClient;
                    currentState = LockServerState.LOCKED;
                    clientMessage = "Lock is acquired by client " + requestingClient;
                    return new Tuple<int, string, int, string>(requestingClient, clientMessage, -1, null);
                }
                else if (inputCommand == Command.UNLOCK)
                {
                    return new Tuple<int, string, int, string>(requestingClient, "First acquire a lock and then unlock it", -1, null);
                }

            }
            else if (currentState == LockServerState.STARTED)
            {

                if (inputCommand == Command.LOCK)
                {
                    clientWithLock = requestingClient;
                    currentState = LockServerState.LOCKED;
                    clientMessage = "Lock is acquired by client " + requestingClient;
                    return new Tuple<int, string, int, string>(requestingClient, clientMessage, -1, null);
                }

            }
            else if (currentState == LockServerState.LOCKED)
            {
                int numberOfWaitingClients = waitingClients.Count;

                // If the lock is already acquired, add the requesting client to a queue
                if (inputCommand == Command.LOCK)
                {
                    waitingClients.Enqueue(requestingClient);
                    Console.WriteLine("\n########## Client " + requestingClient + " is added to the queue ##########\n");
                }
                else if (inputCommand == Command.UNLOCK)
                {
                    if (requestingClient == clientWithLock && numberOfWaitingClients == 0)
                    {
                        currentState = LockServerState.UNLOCKED;
                        clientMessage = "Lock is released by the client " + clientWithLock;
                        clientWithLock = -1;
                        return new Tuple<int, string, int, string>(requestingClient, clientMessage, -1, null);
                    }
                    else if (requestingClient == clientWithLock && numberOfWaitingClients > 0)
                    {
                        int clientWithOldLock = clientWithLock;
                        String clientOldMessage = "Lock is released by the client " + clientWithLock;
                        int clientAtFrontOfQueue = waitingClients.Dequeue();
                        clientWithLock = clientAtFrontOfQueue;
                        clientMessage = "\nLock is acquired by client " + clientWithLock;
                        return new Tuple<int, string, int, string>(clientAtFrontOfQueue, clientMessage, clientWithOldLock, clientOldMessage);
                    }

                }

            }
            else
            {
                Console.WriteLine("\n########## Debug message: ALERT!!\n Lock server reached in a state where it should never have been! ##########\n");
            }

            PrintLockServerInformation();
            return new Tuple<int, string, int, string>(-1, null, -1, null);
        }

        public LockServerState ExecuteCommand(Command inputCommand)
        {
            if (inputCommand == Command.NONE) {
                return currentState;
            }
            if (currentState == LockServerState.TERMINATED)
            {
                clientMessage = " The servers are offline. Please try again later \n";
            }
            else if (currentState == LockServerState.UNLOCKED)
            {
                if (inputCommand == Command.TERMINATE)
                {
                    currentState = LockServerState.TERMINATED;
                    clientMessage = "Servers are terminated \n";
                }
            }
            else if (currentState == LockServerState.STARTED)
            {

                if (inputCommand == Command.TERMINATE)
                {
                    clientWithLock = -1;
                    currentState = LockServerState.TERMINATED;
                    clientMessage = "Servers are terminated";
                }

            }
            PrintLockServerInformation();
            return currentState;
        }

        public void PrintLockServerInformation()
        {
            System.Console.WriteLine("\n########## Lock Server State: " + currentState + " ##########\n");

            if (clientWithLock != -1)
            { 
                System.Console.WriteLine("\n########## Client # with Lock: " + clientWithLock + " ##########\n");
            }
            if (clientMessage != null)
            {
                Console.WriteLine("\n########## Message to the client: " + clientMessage + " ##########\n");
            }

            String listOfWaitingClients = "";
            for (int i = 0; i < waitingClients.Count; i++)
            {
                listOfWaitingClients += waitingClients.ElementAt(i);
                listOfWaitingClients += " ";        
            }
            Console.WriteLine("\n########## List of waiting clients: " + listOfWaitingClients + " ##########\n");
        }

        public LockServerState CurrentState { get; private set; }

        public Boolean CanWeTurnOffServer()
        {
            return currentState == LockServerState.TERMINATED;
        }
    }
}

