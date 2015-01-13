using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Threading;

namespace _550_Assignment2
{
    public class Node
    {
        // --- Node Variables ---
        private int nodeIdentifier;
        private bool isNodeAlive;
        private LockServer objectLockServer;
        private int port;
        private int nextInstanceNumber;
        private List<Node> peerNodes;

        // a dictionary which contains the chosen value for each instance and whether that value has been executed yet
        private Dictionary<int, Tuple<Value, bool>> chosenValues;

        // The message reciever listens for messages from other nodes
        private MessageReceiver messageReceiver;

        // --- Proposer variables ---
        // the proposal number that will be used for the next proposal
        private int proposalNumberCounter;

        // the command requested by the user that the proposer initially tried to pass
        private Dictionary<int, Value> recommendedCommand;

        // the number of promises a proposer has recieved, key is instance, value is counter
        private Dictionary<int, int> proposerPromiseCounter;

        // the proposal with the highest proposal value that a proposer recieves in a promise from an acceptor
        private Dictionary<int, Tuple<int, Value>> highestAcceptedProposalNr;

        // --- Acceptor variables ---
        /*
         * This contains (for every instance)
         *  1. The highest proposal number the acceptor responded with a promise to
         *  2. The highest proposal number the acceptor has accepted
         *  3. The value of the proposal with the highest proposal number that the acceptor has accepted
         */
        private Dictionary<int, Tuple<int, int, Value>> acceptorStates;

        // --- Learner variables ---
        // the number messages recieved by a learner from the acceptors that a proposal is accepted
        // key is instance, value is the propsal and the current count
        private Dictionary<int, Tuple<int, Value, int>> learnerAcceptCounter;


        private Dictionary<int, Tuple<EventWaitHandle,String>> clientInformation;

        public Node(int nodeIdentifier, int port)
        {
            this.nodeIdentifier = nodeIdentifier;
            this.port = port;
            proposalNumberCounter = nodeIdentifier;
            chosenValues = new Dictionary<int, Tuple<Value, bool>>();
            acceptorStates = new Dictionary<int, Tuple<int, int, Value>>();
            peerNodes = new List<Node>();
            recommendedCommand = new Dictionary<int, Value>();
            proposerPromiseCounter = new Dictionary<int, int>();
            highestAcceptedProposalNr = new Dictionary<int, Tuple<int, Value>>();
            learnerAcceptCounter = new Dictionary<int, Tuple<int, Value, int>>();
            nextInstanceNumber = 0;
            objectLockServer = new LockServer();
            clientInformation = new Dictionary<int, Tuple<EventWaitHandle, string>>();
        }

        /*
         * Sets up a new message reciever listening for messages on this node's port
         */
        public void startServer()
        {
            isNodeAlive = true;
            messageReceiver = new MessageReceiver(this.MessageReceived, this);
            messageReceiver.start();
            Thread client = new Thread(() => startListeningForClients());
            client.Start();
        }

        public void stopServer()
        {
            isNodeAlive = false;
            messageReceiver.Kill();
        }

        /*
         * Invokes a new proposal in a new algorithm instance, trying to pass 'command'
         */
        public void startProposerModule(Value command)
        {

            // The node is down and we should not modify the instance number 
            if (!isNodeAlive)
            {
                Console.WriteLine("Please start the servers");
                return;
            }
            // chose instance number and increment so that next proposal will use next instance number
            int thisInstanceNumber = nextInstanceNumber;
            nextInstanceNumber++;
            startProposerModule(command, thisInstanceNumber);
        }

        /*
         * Invokes a new proposal trying to pass 'command' for the algorithm instance 'instanceNumber'
         */
        public void startProposerModule(Value command, int instanceNumber)
        {
            if(proposerPromiseCounter.ContainsKey(instanceNumber))
                proposerPromiseCounter[instanceNumber] = 0;

            // The node is down and we should not send any propose requests. 
            if (!isNodeAlive)
                return;

            // This make sures that everytime a unique proposal is chosen by each server whenever a client request arrives
            proposalNumberCounter += peerNodes.Count;

            /* save the proposed command so we can retry it later if we fail passing it. we don't need to provide this
             functionality for NONE commands */
            if (!recommendedCommand.ContainsKey(instanceNumber) && !(command.getCommand() == Command.NONE))
            {
                recommendedCommand.Add(instanceNumber, command);
            }

            Console.WriteLine("Server " + this.nodeIdentifier + " with port " + port + " is the proposer for instance number  " + instanceNumber + " and command " + command.ToString());
            printMessage("PREPARE SENT", instanceNumber, this.nodeIdentifier, "ALL", proposalNumberCounter, command);

            //Send propose request to all the nodes
            for (int i = 0; i < peerNodes.Count; i++)
            {
                Node peerNode = peerNodes[i];
                Message proposeRequestMessage = new Message(MessageType.PREPARE_REQUEST, this.nodeIdentifier, peerNode.getNodeIdentifier(), instanceNumber, proposalNumberCounter, command, -1);
                sendMessage(proposeRequestMessage);
            }
        }

        /*
         * Sends a message via a socket to a destination node. 
         */
        private void sendMessage(Message message)
        {
            Node destinationNode = peerNodes[message.ReceiverID];
            BinaryFormatter objectBinaryFormatter = new BinaryFormatter();

            try
            {
                // all nodes will run on localhost
                IPAddress destinationIPAddress = IPAddress.Parse("127.0.0.1");

                IPEndPoint objectIPEndPoint = new IPEndPoint(destinationIPAddress, destinationNode.getPort());
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.SendTimeout = 10000;
                    socket.Connect(objectIPEndPoint);
                    using (NetworkStream outputStream = new NetworkStream(socket, false))
                    {
                        objectBinaryFormatter.Serialize(outputStream, message);
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("Unable to connect to server " + destinationNode.getNodeIdentifier() + " at port: " + destinationNode.getPort());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to send message to server " + destinationNode.getNodeIdentifier() + " at port: " + destinationNode.getPort());
            }
        }

        /*
         * Recieves a message, decodes it, and takes an action depending on what type of message it is
         */
        private void MessageReceived(Message receivedMessage)
        {
            int recievedInstanceNumber = receivedMessage.InstanceNumber;
            int proposalNumber = receivedMessage.ProposalNumber;

            // The message is a prepare request sent by a proposer. 
            // This code will be executed by the acceptor
            if (receivedMessage.MsgType == MessageType.PREPARE_REQUEST)
            {
                handlePrepareReqeustReceived(receivedMessage);
            } // if messageType =0 is finished

            // This means that this message is a PROMISE from an acceptor. This will be thus executed by Proposer. 
            else if (receivedMessage.MsgType == MessageType.PROMISE)
            {
                handlePromiseReceived(receivedMessage);
            }
            // this message will be received by a proposer when his prepare request was turned down by an acceptor
            // if he gets a majority of these he will retry his proposal with an incremented proposal value
            else if (receivedMessage.MsgType == MessageType.PREPARE_REQUEST_NACK)
            {
                handlePrepareRequestNackReceived(receivedMessage);
            }
            // This code will be executed by the acceptors.
            // Acceptor is going to accept a message only if it has not responded to a message higher than the proposal number of accepted message
            // Acceptor is going to set the acceptor state and broadcast ACCEPTED message to all listeners
            else if (receivedMessage.MsgType == MessageType.ACCEPT_REQUEST)
            {
                handleAcceptRequestReceived(receivedMessage);
            }
            // This code will be executed by learners. They will keep track of accepted requests. 
            // If majority is receieved the value is chosen
            // If not then just increment the count of accepted requests
            else if (receivedMessage.MsgType == MessageType.ACCEPTED)
            {
                handleAcceptedReceived(receivedMessage);
            }
        }

        private void handlePrepareReqeustReceived(Message receivedMessage)
        {
            int recievedInstanceNumber = receivedMessage.InstanceNumber;
            int proposalNumber = receivedMessage.ProposalNumber;

            printMessage("PREPARE RECEIVED", recievedInstanceNumber, receivedMessage.SenderID, receivedMessage.ReceiverID + "", proposalNumber, receivedMessage.Val);

            /* if the recieved instance number is greater than or equal to the instance number this node will use for its
             next proposal, increase this node's next instance number so it won't try to pass an algorithm instance
             it knows has already been run */
            if (recievedInstanceNumber >= nextInstanceNumber)
            {
                nextInstanceNumber = recievedInstanceNumber + 1;
            }

            // check if the acceptor has already got a proposal in this instance
            int highestProposalResponded = -1;
            int highestProposalAccepted = -1;
            Value valueForHighestProposalAccepted = null;
            try
            {
                var acceptorStateForInstance = this.acceptorStates[recievedInstanceNumber];
                highestProposalResponded = acceptorStateForInstance.Item1;
                highestProposalAccepted = acceptorStateForInstance.Item2;
                valueForHighestProposalAccepted = acceptorStateForInstance.Item3;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            // if proposal number is less than the proposal number of a proposal the acceptor has already recieved ...
            if (proposalNumber < highestProposalResponded)
            {
                // send a NACK to the proposer
                Message nack = new Message(MessageType.PREPARE_REQUEST_NACK, this.nodeIdentifier, receivedMessage.SenderID, recievedInstanceNumber, proposalNumber, new Value(Command.NONE, -1), -1);
                this.sendMessage(nack);
                printMessage("PROMISE NACK SENT", recievedInstanceNumber, this.nodeIdentifier, receivedMessage.SenderID + "", proposalNumber, new Value(Command.NONE, -1));
            }
            //  ... else if the proposal number higher than any proposal number the acceptor has already recieved ...
            else if (proposalNumber > highestProposalResponded)
            {
                // Code to identify which node we need to send response
                Node proposer = peerNodes[receivedMessage.SenderID];

                // If proposer is null
                if (proposer != null)
                {
                    // update the acceptor state with the new proposal number
                    Tuple<int, int, Value> newAcceptorState = new Tuple<int, int, Value>(proposalNumber, highestProposalAccepted, valueForHighestProposalAccepted);
                    acceptorStates[recievedInstanceNumber] = newAcceptorState;

                    // respond to the proposer with a promise
                    Message promise = new Message(MessageType.PROMISE, this.nodeIdentifier, receivedMessage.SenderID, recievedInstanceNumber, proposalNumber, valueForHighestProposalAccepted, highestProposalAccepted);
                    this.sendMessage(promise);
                    if (valueForHighestProposalAccepted == null)
                        valueForHighestProposalAccepted = new Value(Command.NONE, -1);
                    printMessage("PROMISE SENT", recievedInstanceNumber, receivedMessage.ReceiverID, receivedMessage.SenderID + "", proposalNumber, valueForHighestProposalAccepted);
                }
            }
        }

        private void handlePromiseReceived(Message promise)
        {
            int recievedInstanceNumber = promise.InstanceNumber;
            int proposalNumber = promise.ProposalNumber;
            printMessage("PROMISE RECEIVED", recievedInstanceNumber, promise.SenderID, this.nodeIdentifier + "", proposalNumber, promise.Val);

            // Proposer got a promise for a proposal that is outdated (it has already started a new proposal)
            if (promise.ProposalNumber < proposalNumberCounter)
            {
                // Do nothing
                Console.WriteLine("Current Proposal is " + proposalNumberCounter + ". I got a promise for " + promise.ProposalNumber + ". I will not do anything");
            }
            // The proposer receives a response for its proposal request
            else
            {
                int highestAcceptedProposalNumberInPromise = promise.HighestAcceptedProposalNumber;
                Value valueForHighestAcceptedPropsalNumberInPromise = promise.Val;

                // save the response that contains the highest proposal number
                if (highestAcceptedProposalNr.ContainsKey(recievedInstanceNumber))
                {
                    int maxProposalAccepted = highestAcceptedProposalNr[recievedInstanceNumber].Item1;
                    if (maxProposalAccepted < highestAcceptedProposalNumberInPromise)
                    {
                        highestAcceptedProposalNr[recievedInstanceNumber] = new Tuple<int, Value>(highestAcceptedProposalNumberInPromise, valueForHighestAcceptedPropsalNumberInPromise);
                    }
                }
                else
                {
                    // promises that don't contain a value (proposal number -1) should not be saved
                    if (highestAcceptedProposalNumberInPromise >= 0)
                    {
                        highestAcceptedProposalNr.Add(recievedInstanceNumber, new Tuple<int, Value>(highestAcceptedProposalNumberInPromise, valueForHighestAcceptedPropsalNumberInPromise));
                    }
                }

                // keep a count of the number of promises received
                int countResponseRequest;
                if (proposerPromiseCounter.ContainsKey(promise.InstanceNumber))
                {
                    countResponseRequest = proposerPromiseCounter[promise.InstanceNumber];
                    countResponseRequest++;
                    proposerPromiseCounter[promise.InstanceNumber] = countResponseRequest;

                }
                else
                {
                    countResponseRequest = 1;
                    proposerPromiseCounter.Add(promise.InstanceNumber, countResponseRequest);
                }

                int totalNodes = peerNodes.Count;
                int majority = totalNodes / 2 + 1;

                // If the number of Response requests are more than majority than Send accept request to everyone
                if (countResponseRequest >= majority)
                {
                    Value value;
                    /* the accept request should contain the value of the proposal with the highest proposal number
                     of the promises recieved */
                    if (highestAcceptedProposalNr.ContainsKey(recievedInstanceNumber))
                    {
                        int maxProposalAccepted = highestAcceptedProposalNr[recievedInstanceNumber].Item1;
                        //Console.WriteLine("Max Proposal Accepted should always be > -1: " + maxProposalAccepted);
                        value = highestAcceptedProposalNr[recievedInstanceNumber].Item2;
                    }
                    else
                    {
                        /* if there was now value already accepted returned by any acceptor, we can suggest the value
                         that was originally requested by the client that invoked this algorithm instance */
                        value = recommendedCommand[recievedInstanceNumber];
                    }

                    // send the accept request to all nodes
                    for (int i = 0; i < peerNodes.Count; i++)
                    {
                        Message acceptRequest = new Message(MessageType.ACCEPT_REQUEST, this.nodeIdentifier, peerNodes[i].getNodeIdentifier(), recievedInstanceNumber,
                            proposalNumberCounter, value, -1);
                        sendMessage(acceptRequest);
                    }
                    printMessage("ACCEPT REQUEST SENT", recievedInstanceNumber, this.nodeIdentifier, "ALL", proposalNumberCounter, value);
                }
            }
        }

        private void handlePrepareRequestNackReceived(Message receivedMessage)
        {
            int recievedInstanceNumber = receivedMessage.InstanceNumber;
            int proposalNumber = receivedMessage.ProposalNumber;

            printMessage("PROMISE NACK RECEIVED", recievedInstanceNumber, receivedMessage.SenderID, this.nodeIdentifier + "", proposalNumber, receivedMessage.Val);
            // disregard outdated nacks
            if (proposalNumber == proposalNumberCounter)
            {
                Console.WriteLine("Node " + this.nodeIdentifier + " received a NACK for it's proposal "+proposalNumber+". Incrementing proposal number and retrying...");
                startProposerModule(recommendedCommand[recievedInstanceNumber], recievedInstanceNumber);
            }
        }

        private void handleAcceptRequestReceived(Message receivedMessage)
        {
            int recievedInstanceNumber = receivedMessage.InstanceNumber;
            int proposalNumber = receivedMessage.ProposalNumber;

            printMessage("ACCEPT REQUEST RECEIVED", recievedInstanceNumber, receivedMessage.SenderID, receivedMessage.ReceiverID + "", proposalNumber, receivedMessage.Val);

            // Get the acceptor state for the server for current instance 
            int highestProposalResponded = -1;
            int highestProposalAccepted = -1;
            Value valueForHighestProposalAccepted = null;

            bool isEntryExist = false;
            try
            {
                var acceptorStateForInstance = this.acceptorStates[recievedInstanceNumber];
                highestProposalResponded = acceptorStateForInstance.Item1;
                highestProposalAccepted = acceptorStateForInstance.Item2;
                valueForHighestProposalAccepted = acceptorStateForInstance.Item3;
                isEntryExist = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            // ACCEPT this proposal
            if (receivedMessage.ProposalNumber >= highestProposalResponded)
            {
                if (isEntryExist)
                    acceptorStates[recievedInstanceNumber] = new Tuple<int, int, Value>(receivedMessage.ProposalNumber, receivedMessage.ProposalNumber, receivedMessage.Val);
                else
                    acceptorStates.Add(recievedInstanceNumber, new Tuple<int, int, Value>(receivedMessage.ProposalNumber, receivedMessage.ProposalNumber, receivedMessage.Val));

                // send messages to all the learners
                for (int i = 0; i < peerNodes.Count; i++)
                {
                    Message acceptedMessage = new Message(MessageType.ACCEPTED, this.nodeIdentifier, peerNodes[i].getNodeIdentifier(), recievedInstanceNumber, receivedMessage.ProposalNumber, receivedMessage.Val, -1);
                    sendMessage(acceptedMessage);
                }
                printMessage("ACCEPTED SENT", recievedInstanceNumber, this.nodeIdentifier, "ALL", proposalNumber, receivedMessage.Val);
            }
        }

        private void handleAcceptedReceived(Message receivedMessage)
        {
            int recievedInstanceNumber = receivedMessage.InstanceNumber;
            int receivedProposalNumber = receivedMessage.ProposalNumber;

            printMessage("ACCEPTED RECEIVED", recievedInstanceNumber, receivedMessage.SenderID, this.nodeIdentifier + "", receivedProposalNumber, receivedMessage.Val);

            // keep a count of received ACCEPTED messages
            // only count accepted messages that has has a propsal number greater than or equal to the greatest proposal 
            // number we received
            int countOfAcceptedRequests = -1;
            Tuple<int, Value, int> learnerCounterState;
            int currentCount;
            if (learnerAcceptCounter.ContainsKey(recievedInstanceNumber))
            {
                learnerCounterState = learnerAcceptCounter[recievedInstanceNumber];
                int currentProposalNumber = learnerCounterState.Item1;
                Value currentValue = learnerCounterState.Item2;
                currentCount = learnerCounterState.Item3;

                if (currentProposalNumber < receivedProposalNumber && !receivedMessage.Val.Equals(currentValue)) 
                {
                    learnerAcceptCounter[recievedInstanceNumber] = new Tuple<int, Value, int>(receivedProposalNumber, receivedMessage.Val, 1);
                } 
                else 
                {
                    currentCount++;
                    learnerAcceptCounter[recievedInstanceNumber] = new Tuple<int, Value, int>(currentProposalNumber, currentValue, currentCount);
                }
            }
            else
            {
                learnerAcceptCounter.Add(recievedInstanceNumber, new Tuple<int, Value, int>(receivedProposalNumber, receivedMessage.Val, 1));
            }

            /* If this learner has got a majority of ACCEPTED messages, the value for this instance is chosen. Add
             it to chosen values*/
            int majority = peerNodes.Count / 2 + 1;
            learnerCounterState = learnerAcceptCounter[recievedInstanceNumber];
            currentCount = learnerCounterState.Item3;

            if (currentCount == majority)
            {
                if (chosenValues.ContainsKey(recievedInstanceNumber))
                {
                    chosenValues[recievedInstanceNumber] = new Tuple<Value, bool>(receivedMessage.Val, false);
                }
                else
                {
                    chosenValues.Add(recievedInstanceNumber, new Tuple<Value, bool>(receivedMessage.Val, false));
                }
                Console.WriteLine("\n$$$$$$$$$$ VALUE CHOSEN!! Value:" + receivedMessage.Val.ToString() + " is chosen for instance " + recievedInstanceNumber + " for server :" + this.nodeIdentifier + " $$$$$$$$$$\n");

                // run trough list of chosen commands previous to this one 
                for (int i = 0; i < recievedInstanceNumber; i++)
                {
                    if (!chosenValues.ContainsKey(i))
                    {
                        /* If there is a command previous to this one that this learner hasn't received yet,
                         propose a NONE command for that instance. This will make a value accepted by a majority of 
                         acceptors in that instance being accepted again, and then sent to this learner. If no
                         value was accepted for that insatnce by no acceptor, the value NONE will be chosen */
                        Console.WriteLine("Learner at node " + this.nodeIdentifier + " have not yet received a chose value for instance " + i + ". Retrying a proposal for this instance...");
                        startProposerModule(new Value(Command.NONE, -1), i);
                        return;
                    }
                    else if (!chosenValues[i].Item2)
                    {
                        /* While running through previous commands, check if there are commands that has been chosen \
                         but haven't yet been executed. If found, execute them */
                        Console.WriteLine("Learner at node " + this.nodeIdentifier + " found that the command " + chosenValues[i].Item1 + " for instance " + i + " was not executed. Executing this command...");
                        var outputLockServer = objectLockServer.ExecuteCommand(chosenValues[i].Item1.getCommand(), chosenValues[i].Item1.getClientId());

                        if (clientInformation.ContainsKey(outputLockServer.Item1))
                        {
                            EventWaitHandle signalClient = clientInformation[outputLockServer.Item1].Item1;
                            clientInformation[outputLockServer.Item1] = new Tuple<EventWaitHandle, string>(signalClient, outputLockServer.Item2);
                            signalClient.Set();
                        }
                        if (outputLockServer.Item3 != -1)
                        {
                            if (clientInformation.ContainsKey(outputLockServer.Item3))
                            {
                                EventWaitHandle signalClient = clientInformation[outputLockServer.Item3].Item1;
                                clientInformation[outputLockServer.Item3] = new Tuple<EventWaitHandle, string>(signalClient, outputLockServer.Item4);
                                signalClient.Set();
                            }
                        }

                        chosenValues[i] = new Tuple<Value, bool>(chosenValues[i].Item1, true);
                    }
                }

                // if all commands previous to this one is received and executed, and this one is not executed, execute this one
                if (!chosenValues[recievedInstanceNumber].Item2)
                {
                    var outputLockServer = objectLockServer.ExecuteCommand(receivedMessage.Val.getCommand(), receivedMessage.Val.getClientId());

                    if (clientInformation.ContainsKey(outputLockServer.Item1))
                    {
                        EventWaitHandle signalClient = clientInformation[outputLockServer.Item1].Item1;
                        clientInformation[outputLockServer.Item1] = new Tuple<EventWaitHandle, string>(signalClient, outputLockServer.Item2);
                        signalClient.Set();
                    }

                    if (outputLockServer.Item3 != -1)
                    {
                        if (clientInformation.ContainsKey(outputLockServer.Item3))
                        {
                            EventWaitHandle signalClient = clientInformation[outputLockServer.Item3].Item1;
                            clientInformation[outputLockServer.Item3] = new Tuple<EventWaitHandle, string>(signalClient, outputLockServer.Item4);
                            signalClient.Set();
                        }
                    }

                    chosenValues[recievedInstanceNumber] = new Tuple<Value, bool>(chosenValues[recievedInstanceNumber].Item1, true);
                }

                /* if this node tried to pass a command for this instance, and that command was not the one that was 
                 chosen, retry passing that command in a new instance */
                if (recommendedCommand.ContainsKey(recievedInstanceNumber) &&
                    !recommendedCommand[recievedInstanceNumber].Equals(receivedMessage.Val))
                {
                    Console.WriteLine("Node " + this.nodeIdentifier + " found that its recommanded command for instance " + recievedInstanceNumber + " was not chosen! Retrying a proposal for this command in a new instance");
                    startProposerModule(recommendedCommand[recievedInstanceNumber]);
                }
            }
        }

        /*
         * Print all of this node's peer nodes
         */
        public void displayPeerNodes()
        {
            Console.WriteLine("List of Peer Servers");
            for (int i = 0; i < peerNodes.Count; i++)
            {
                Console.WriteLine(peerNodes[i].getPort());
            }
        }

        public void displayChosenCommands()
        {
            var list = chosenValues.Keys.ToList();
            list.Sort();
            foreach (var key in list)
            {
                Console.WriteLine("Instance: " + key + ", Value: " + chosenValues[key].Item1 + ", Executed: " + chosenValues[key].Item2);
            }
        }

        public void setAcceptorState(int instanceNumber, int highestProposalResponded, int highestProposalAccepted,
            Value valueForHighestProposalAccepted)
        {
            acceptorStates[instanceNumber] = new Tuple<int, int, Value>(highestProposalResponded,
                highestProposalAccepted, valueForHighestProposalAccepted);
            recommendedCommand[instanceNumber] = valueForHighestProposalAccepted;

        }

        public void displayAcceptorStates()
        {

            var list = acceptorStates.Keys.ToList();
            list.Sort();
            foreach (var key in list)
            {
                if(acceptorStates[key].Item3 == null)
                    Console.WriteLine("Instance: " + key + ", Responded: " + acceptorStates[key].Item1 + ", Accepted: " + acceptorStates[key].Item2 + ", Value: NULL");
                else
                    Console.WriteLine("Instance: " + key + ", Responded: " + acceptorStates[key].Item1 + ", Accepted: " + acceptorStates[key].Item2 + ", Value: " + acceptorStates[key].Item3.ToString());
            }
            Console.WriteLine("");
        }

        public void setChosenValue(int instanceNumber, Value value, bool isExecuted)
        {
            chosenValues.Add(instanceNumber, new Tuple<Value, bool>(value, isExecuted));
        }

        public void eraseChosenValue(int instanceNumber)
        {
            chosenValues.Remove(instanceNumber);
            learnerAcceptCounter.Remove(instanceNumber);
        }

        public int getPort()
        {
            return this.port;
        }

        public int getNodeIdentifier()
        {
            return this.nodeIdentifier;
        }

        public bool getNodeLifeStatus()
        {
            return this.isNodeAlive;
        }

        public void setPeerNodes(List<Node> peerNodes)
        {
            this.peerNodes = peerNodes;
        }

        public void printLockServerStatus()
        {
            objectLockServer.PrintLockServerInformation();
        }

        /*
         * Prints a debug message for messages being sent and received
         */
        public void printMessage(string type, int instanceNumber, int from, string to, int proposalNumber, Value value)
        {
            if (value == null)
            {
                value = new Value(Command.NONE, -1);
            }
            string tabs = "\t";
            if (type.Equals("PROMISE SENT") || type.Equals("ACCEPTED SENT") || type.Equals("PREPARE SENT"))
            {
                tabs = "\t\t";
            }
            Console.WriteLine("- I(" + instanceNumber + "):\t" + type + tabs + "(" + proposalNumber + ", " + value.ToString() + ")\tFrom: " + from + "\tTo: " + to + "\tat " + DateTime.Now.Millisecond);
        }

        public void setRecommendedValue(int instanceNumber, Value value)
        {
            if (recommendedCommand.ContainsKey(instanceNumber))
            {
                recommendedCommand[instanceNumber] = value;
            }
            else
            {
                recommendedCommand.Add(instanceNumber, value);
            }
        }

        public void printRecommendedValues()
        {
            foreach (KeyValuePair<int, Value> recCmd in recommendedCommand)
            {
                Console.WriteLine("Instance: " + recCmd.Key.ToString() + ", Value: " + recCmd.Value.ToString());
            }
            Console.WriteLine("");
        }

        public void startListeningForClients()
        {
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.
            // Dns.GetHostName returns the name of the 
            // host running the application.

            IPAddress hostIP = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(hostIP, port-1000);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and 
            // listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                // Start listening for connections.
                while (true)
                {
                    Console.WriteLine("Server "+ nodeIdentifier+" is waiting for a client connection...");
                    // Program is suspended while waiting for an incoming connection.
                    Socket handler = listener.Accept();

                    Thread client = new Thread(() => ClientHandler(handler));
                    client.Start();     

                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        public void ClientHandler(Socket handler)
        {
            try
            {
                String data = null;
                byte[] bytes = new Byte[1024];

                while (true)
                {
                    bytes = new byte[1024];
                    int bytesRec = handler.Receive(bytes);
                    data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    if (data.IndexOf("<EOF>") > -1)
                    {
                        break;
                    }
                }

                // Show the data on the console.
                Console.WriteLine("Text received : {0}", data);

                // Echo the data back to the client.
                EventWaitHandle waitHandle = new AutoResetEvent(false);
                String clientMessage = null;
                string[] clientData = data.Split(' ');

                String clientCommand = clientData[0];
                int clientNumber = Convert.ToInt32(clientData[1]);

                if (clientInformation.ContainsKey(clientNumber))
                {
                    clientInformation[clientNumber] = new Tuple<EventWaitHandle, string>(waitHandle, clientMessage);
                }
                else
                {
                    clientInformation.Add(clientNumber, new Tuple<EventWaitHandle, string>(waitHandle, clientMessage));
                }

                startProposerModule(new Value(stringToCommand(clientCommand), clientNumber));

                waitHandle.WaitOne();

                String message = clientInformation[clientNumber].Item2;
                byte[] msg = Encoding.ASCII.GetBytes(message);
                handler.Send(msg);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("The client terminated abruptly: " + e.Message);
            }
        }

        public Command stringToCommand(string str)
        {
            if (str.Equals("lock", StringComparison.InvariantCultureIgnoreCase))
            {
                return Command.LOCK;
            }
            else if (str.Equals("unlock", StringComparison.InvariantCultureIgnoreCase))
            {
                return Command.UNLOCK;
            }
            else
            {
                throw new Exception();
            }
        } 
    }

    /*
     * A value is a command requested by a client and the id of that client. This is what the Paxos algorithm tries to 
     * pass.
     */
    [Serializable]
    public class Value
    {
        private Command command;
        private int clientId;

        public Value(Command command, int clientId)
        {
            this.command = command;
            this.clientId = clientId;
        }

        public Command getCommand() { return command; }
        public int getClientId() { return clientId; }

        public override String ToString()
        {
            return "<" + command.ToString() + "," + clientId + ">";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Value otherValue = (Value)obj;
            return (command == otherValue.command) && (clientId == otherValue.clientId);
        }
    }
}
