using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _550_Assignment2
{
    // Command line interface for testsing program
    class StartServer
    {
        public static void Main()
        {
            Console.WriteLine("Initialization of Servers");
            Console.WriteLine("=====================");
            Node server;

            Console.WriteLine("Please enter the number of nodes in the server: ");
            int numberOfNodes = Convert.ToInt32(Console.ReadLine());

            List<Node> peerNodes = new List<Node>();

            while (true)
            {
                Console.WriteLine("Please enter the starting port number [should be > 1000]: ");

                try
                {
                    int portNumber = Convert.ToInt32(Console.ReadLine());
                    if (portNumber >= 1000 && portNumber < 65536)
                    {
                        for (int i = 0; i < numberOfNodes; i++)
                        {
                            Node tempNode = new Node(i, portNumber + i);
                            peerNodes.Add(tempNode);
                        }
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Incorrect port. Please enter a valid port\n\n");
                    }
                }
                catch(Exception)
                {
                    Console.WriteLine("Incorrect port. Please enter a valid port\n\n");
                }
            }

            for (int i = 0; i < numberOfNodes; i++)
            {
                Node tempNode = peerNodes[i];
                tempNode.setPeerNodes(peerNodes);
                Console.WriteLine("Peer server nodes for server: " + i);
                Console.WriteLine("---------------------------------");
                tempNode.displayPeerNodes();
                Console.WriteLine("---------------------------------");
            }

            while (true)
            {
                Console.WriteLine("============================================================================");
                Console.WriteLine("Please enter the command: <HELP> if unsure");
            
                // get input command
                string inputLine = Console.ReadLine();
                string[] inputLineTokens = inputLine.Split(' ');
                string command = inputLineTokens[0];

                // interpret command
                if (command.Equals("exit", StringComparison.InvariantCultureIgnoreCase))
                {
                    Environment.Exit(0);
                }
                else if (command.Equals("Start", StringComparison.InvariantCultureIgnoreCase))
                {
                    for (int i = 0; i < numberOfNodes; i++)
                    {
                        Node tempNode = peerNodes[i];
                        tempNode.startServer();
                        Console.WriteLine("Server " + i + " is started");
                    }

                }
                else if (command.Equals("lock", StringComparison.InvariantCultureIgnoreCase) ||
                    command.Equals("unlock", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        Command clientCommand = stringToCommand(command);
                        int clientNumber = Convert.ToInt32(inputLineTokens[1]);
                        int serverNumber = Convert.ToInt32(inputLineTokens[2]);
                        Node tempNode = peerNodes[serverNumber];
                        tempNode.startProposerModule(new Value(clientCommand, clientNumber));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write Lock/UnLock <client id> <Server Number>");
                    }
                }
                else if (command.Equals("Stop", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        int serverNumber = Convert.ToInt32(inputLineTokens[1]);
                        Node tempNode = peerNodes[serverNumber];
                        tempNode.stopServer();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write Stop <Server Identifier>");
                    }

                }
                else if (command.Equals("Status", StringComparison.InvariantCultureIgnoreCase))
                {
                    for (int i = 0; i < peerNodes.Count; i++)
                    {
                        String status = "stopped";
                        if (peerNodes[i].getNodeLifeStatus())
                            status = "running";
                        Console.WriteLine("Server: "+ i + " Port: "+ peerNodes[i].getPort() + " Status: "+ status);  
                    }
                }
                else if (command.Equals("Order", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Printing command sequence for all server nodes");
                    for (int i = 0; i < peerNodes.Count; i++)
                    {
                        Console.WriteLine("Server " + i);
                        peerNodes[i].displayChosenCommands();
                        Console.WriteLine("-----------------");
                    }
                }
                else if (command.Equals("SetAS", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        int node = Convert.ToInt32(inputLineTokens[1]);
                        int instanceNumber = Convert.ToInt32(inputLineTokens[2]);
                        int highestProposalResponded = Convert.ToInt32(inputLineTokens[3]);
                        int highestProposalAccepted = Convert.ToInt32(inputLineTokens[4]);
                        String clientCommandString = inputLineTokens[5];
                        int clientId = Convert.ToInt32(inputLineTokens[6]);

                        Command clientCommand = stringToCommand(clientCommandString);

                        Value valueForHighestProposalAccepted = new Value(clientCommand, clientId);

                        Node tempNode = peerNodes[node];
                        tempNode.setAcceptorState(instanceNumber, highestProposalResponded, highestProposalAccepted, 
                            valueForHighestProposalAccepted);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write Setas <Server Identifier> <instanceNumber> <highestProposalResponded> <highestProposalAccepted> <value's command> <value's client id>");
                    }
                }
                else if (command.Equals("Delcv", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        int node = Convert.ToInt32(inputLineTokens[1]);
                        int instanceNumber = Convert.ToInt32(inputLineTokens[2]);
                        Node tempNode = peerNodes[node];
                        tempNode.eraseChosenValue(instanceNumber);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write Setas <Server Identifier> <instanceNumber> <highestProposalResponded> <highestProposalAccepted> <value's command> <value's client id>");
                    }
                }
                else if (command.Equals("Showas", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("-----------------");
                    for (int i = 0; i < peerNodes.Count; i++)
                    {
                        Console.WriteLine("Server " + i);
                        peerNodes[i].displayAcceptorStates();
                        Console.WriteLine("-----------------");
                    }                    
                }
                else if (command.Equals("Help", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("start \n" +
                     "stop <server#>\n" +
                      "status\n" +
                       "lock/unlock <client id> <server#>\n" +
                        "order\n" +
                         "exit\n" +
                          "setAS <Server Identifier> <instanceNumber> <highestProposalResponded> <highestProposalAccepted> <value's command> <value's client id>\n" +
                           "showAS\n" + 
                           //"setCV <server#> <instance#> <command> <client#>\n" +
                            "con <command 1> <client# 1> <server# 1> <command 2> <client# 2> <server# 2>\n" +
                            "delCV <server#> <instance#>\n" +
                            "statusLS <server#>\n"+
                            //"setRV <command> <clientId> <instance#> <server#>\n"+
                            "");
                }
                else if (command.Equals("con", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        Command clientCommand1 = stringToCommand(inputLineTokens[1]);
                        int clientNumber1 = Convert.ToInt32(inputLineTokens[2]);
                        int serverNumber1 = Convert.ToInt32(inputLineTokens[3]);
                        Node tempNode1 = peerNodes[serverNumber1];

                        Command clientCommand2 = stringToCommand(inputLineTokens[4]);
                        int clientNumber2 = Convert.ToInt32(inputLineTokens[5]);
                        int serverNumber2 = Convert.ToInt32(inputLineTokens[6]);
                        Node tempNode2 = peerNodes[serverNumber2];

                        tempNode1.startProposerModule(new Value(clientCommand1, clientNumber1));
                        tempNode2.startProposerModule(new Value(clientCommand2, clientNumber2));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write Lock/UnLock <client id> <Server Number>");
                    }
                }
                else if (command.Equals("setCV", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        int serverNumber = Convert.ToInt32(inputLineTokens[1]);
                        int instanceNumber = Convert.ToInt32(inputLineTokens[2]);
                        Command clientCommand = stringToCommand(inputLineTokens[3]);
                        int clientNumber = Convert.ToInt32(inputLineTokens[4]);
                        bool executed = Convert.ToBoolean(inputLineTokens[5]);
                        Node tempNode = peerNodes[serverNumber];
                        tempNode.setChosenValue(instanceNumber, new Value(clientCommand, clientNumber), executed);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command!");
                    }
                }
                else if (command.Equals("setRV", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        Command clientCommand = stringToCommand(inputLineTokens[1]);
                        int clientNumber = Convert.ToInt32(inputLineTokens[2]);
                        int instanceNumber = Convert.ToInt32(inputLineTokens[3]);
                        int serverNumber = Convert.ToInt32(inputLineTokens[4]);
                        Node tempNode = peerNodes[serverNumber];

                        tempNode.setRecommendedValue(instanceNumber, new Value(clientCommand, clientNumber));
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Incorret usage of command! Please write setrv <command> <clientId> <instance#> <server#>");
                    }
                }
                else if (command.Equals("showrv", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine("Printing recommended values for all server nodes");
                    for (int i = 0; i < peerNodes.Count; i++)
                    {
                        Console.WriteLine("Server " + i);
                        peerNodes[i].printRecommendedValues();
                        Console.WriteLine("-----------------");
                    }
                }
                else if (command.Equals("statusLS", StringComparison.InvariantCultureIgnoreCase))
                {
                    int serverNumber = 0; 
                    try
                    {
                        serverNumber = Convert.ToInt32(inputLineTokens[1]);
                        Console.WriteLine("Printing the status of lock server");
                        bool isPrinted = false;
                        for (int i = 0; i < peerNodes.Count; i++)
                        {
                            if (i == serverNumber)
                            {
                                peerNodes[i].printLockServerStatus();
                                isPrinted = true;
                            }
                        }

                        if (!isPrinted)
                            Console.WriteLine("Invalid Server Code");


                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Please specify the server number as well");
                    }
                }
                else
                {
                    Console.WriteLine("Unknown command! Please try again. ");
                }
            }
        }

        /*
         * Translates a string to the corresponding Command
         */
        public static Command stringToCommand(string str) {
            if (str.Equals("lock", StringComparison.InvariantCultureIgnoreCase)) {
                return Command.LOCK;
            } else if (str.Equals("unlock", StringComparison.InvariantCultureIgnoreCase)) {
                return Command.UNLOCK;
            } else {
                throw new Exception();
            }
        } 
    }
}
