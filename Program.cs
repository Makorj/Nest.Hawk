using BlackBird;
using Hawk.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static BlackBird.MessageHelper;
using Task = System.Threading.Tasks.Task;

namespace Hawk
{
    class Program
    {
        public static readonly string VERSION = "Version " + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        static bool quit = false;

        static Server server = null;
        static DatabaseManager databaseManager = null;

        static List<Socket> sockets;

        static List<Socket> queleaWorkers;
        static List<Socket> jackdawClients;
        static List<Socket> toRemove = new List<Socket>();

        static public Dictionary<string, Func<string[], int>> CmdCallbacks =
        new Dictionary<string, Func<string[], int>>{
            { "quit", (args) => { quit = true; return 0; }},
            { "server", (args) =>
                {
                    if(args[0] == "start")
                    {
                        server.Start();
                    }
                    else if(args[0] == "stop")
                    {
                        server.Stop();
                    }

                    return 0;
                }
            },
            { "user", (args) =>
                {
                    if(args[0] == "add")
                    {
                        User user = new User
                        {
                            Login = args[1],
                            Password = BlackBird.Security.HashPassword(args[2]),
                            Name = args[3] ?? "Default"
                        };

                        databaseManager.Add<User>(user);
                    }
                    else if(args[0] == "remove")
                    {
                        User user = new User
                        {
                            Id = Int32.Parse(args[1])
                        };

                        databaseManager.Remove<User>(user);
                    }
                    else if(args[0] == "list")
                    {
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine(String.Format("{0,5} {1,20} {2,50} {3,20}", "Id", "Login", "Password", "Name"));
                        Console.ResetColor();

                        int value = 0;
                        foreach(var user in databaseManager.GetAllUser())
                        {
                            if(value%2 == 1)
                            {
                                Console.BackgroundColor = ConsoleColor.Gray;
                                Console.ForegroundColor = ConsoleColor.Black;
                            }

                            value++;
                            Console.WriteLine(user);
                            Console.ResetColor();
                        }
                        Console.WriteLine(" ");
                    }
                    return 0;
                }
            },
            { "list", (args) =>
                {
                    if(args == null)
                    {
                        return 0;
                    }

                    if(args[0] == "clients")
                    {
                        PrintAsTable(jackdawClients, "Connected clients");
                    }
                    else if(args[0] == "quelea")
                    {
                        PrintAsTable(queleaWorkers, "Connected workers");
                    }
                    return 0;
                }
            },
            { "send", (args) =>
                {
                    int index = 0;
                    foreach (Socket socket in sockets)
                    {
                        server.SendMessage("command".ToMessage(), socket);
                        Command command = new UnityCommand("");
                        server.SendMessage(command.ToMessage(), socket);
                        index++;
                    }
                    return 0;
                }
            },
            { "receive", (args) =>
                {
                    int index = 0;
                    foreach (Socket socket in sockets)
                    {
                        while(socket.Available > 0)
                        {
                            Console.WriteLine($"{index} : {server.ReceiveMessageFromSocket(socket)}");
                        }
                        index++;
	                }
                    return 0;
                }
            },
            { "set", (args) =>
                {
                    if(args[0] == "server")
                    {
                        if(args[1] == "-v" || args[1] == "--verbosity")
                        {
                            try
                            {
                                int value = Int32.Parse(args[2]);
                                if(value >= 0 && value <= 4 )
                                {
                                    Server.VerboseLevel = value;
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }
                            catch (Exception)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("[ERROR] Invalid parameter. Verbose level should be an interger between 0 and 4");
                                Console.ResetColor();

                                return 1;
                            }
                        }
                    }
                    else
                    {
                        string path = "";
                        for (int i = 0; i < args.Length; i++)
                        {
                            path += args[i];
                            if(i+1<args.Length)
                                path += " ";
                        }

                    }
                    return 0;
                }
            }
        };

        static void Main(string[] args)
        {

            if(args.Length >= 2)
            {
                try
                {
                    server = new Server(new ServerInfo
                    {
                        ipAddress = IPAddress.Parse(args[0]),
                        port = Int32.Parse(args[1]),
                    }, false);
                }
                catch (Exception)
                {
                    Console.WriteLine("Wrong arguments. Use should be :\n\n\t hawk [IP address] [Port Number]");
                    Environment.Exit(0);
                }
            }
            else
            {
                server = new Server(new ServerInfo
                {
                    ipAddress = IPAddress.Parse("192.168.0.21"),
                    port = 8001,
                }, false);
            }

            Console.Title = "Hawk";
            Console.WriteLine(LOGO);
            Console.WriteLine(VERSION);

            databaseManager = DatabaseManager.GetDatabaseManager();
            sockets = new List<Socket>();
            queleaWorkers = new List<Socket>();
            jackdawClients = new List<Socket>();

            server.Start();

            Console.Write(Environment.UserName + "@" + Environment.MachineName + " $ ");
            Task<string> commandReaderTask = Task.Run(() => Console.ReadLine());
            
            while (!quit)
            {
                if(commandReaderTask.IsCompleted)
                {
                    ProcessCommand(commandReaderTask.Result ?? "");
                    Console.Write(Environment.UserName + "@" + Environment.MachineName + " $ ");
                    commandReaderTask = Task.Run(() => Console.ReadLine());
                }

                while(server.PendingNewConnection)
                {
                    sockets.Add(server.GetPendingSocket);
                    Console.WriteLine("\n [SERVER] Accepting one pending socket");
                }

                ProcessIncomingClientMessage();
            }
        }

        private static void ProcessIncomingClientMessage()
        {
            toRemove.Clear();

            int index = 0;
            Message msg;
            string msgStr;

            foreach (Socket socket in sockets)
            {
                while (socket.Available > 0)
                {
                    msg = server.ReceiveMessageFromSocket(socket);
                    msgStr = msg.ToString();
                    Console.WriteLine($"{index} : {msgStr}");

                    if(msgStr.StartsWith(CLIENT.BASE))
                    {
                        if(msgStr.StartsWith(CLIENT.USER))
                        {
                            if(databaseManager.VerifyUser(msgStr.Split(' ')[2], (msgStr.Split(' ')[3])))
                            {
                                server.SendMessage(HAWK.VALID.ToMessage(), socket);
                                jackdawClients.Add(socket);
                                toRemove.Add(socket);
                            }
                            else
                            {
                                server.SendMessage(HAWK.ERROR.USER_UNKNOWN.ToMessage(), socket);
                            }
                        }
                        else
                        {
                            server.SendMessage(HAWK.CONNECT.ToMessage(), socket);
                        }
                    }
                    else if(msgStr.StartsWith(QUELEA.BASE))
                    {
                        queleaWorkers.Add(socket);
                        toRemove.Add(socket);
                    }
                }
                index++;
            }

            foreach(var item in toRemove)
            {
                sockets.Remove(item);
            }

            toRemove.Clear();
            


            foreach (Socket socket in jackdawClients)
            {
                while (socket.Available > 0)
                {
                    msg = server.ReceiveMessageFromSocket(socket);
                    msgStr = msg.ToString();

                    if (msgStr == CLIENT.DISCONNECT)
                    {
                        Console.WriteLine($"Client disconnected : {(IPEndPoint)socket.RemoteEndPoint}");
                        toRemove.Add(socket);
                    }
                    else if (msgStr == CLIENT.REQUEST.AVAILABLE_WORKER)
                    {
                        if(queleaWorkers.Count <= 0)
                        {
                            server.SendMessage(HAWK.ERROR.NO_AVAILABLE_WORKER.ToMessage(), socket);
                        }
                        else
                        {
                            server.SendMessage(HAWK.OK.ToMessage(), socket);
                        }
                    }
                    else if(msgStr == CLIENT.SEND.COMMAND_LIST)
                    {
                        msg = server.ReceiveMessageFromSocket(socket);
                        msgStr = msg.ToString();

                        if (msgStr == CLIENT.SEND.BUILD)
                        {
                            msg = server.ReceiveMessageFromSocket(socket);

                            foreach (Socket item in queleaWorkers)
                            {
                                server.SendMessage(HAWK.ORDER.BUILD.ToMessage(), item);
                                server.SendMessage(msg, item);
                            }
                        }
                        
                    }
                }
                index++;
            }

            foreach (var item in toRemove)
            {
                jackdawClients.Remove(item);
            }

            toRemove.Clear();

            foreach (Socket socket in queleaWorkers)
            {
                while (socket.Available > 0)
                {
                    msg = server.ReceiveMessageFromSocket(socket);
                    msgStr = msg.ToString();

                    if (msgStr == QUELEA.DONE)
                    {
                        Console.WriteLine($"Worker done : {(IPEndPoint)socket.RemoteEndPoint}");
                        //toRemove.Add(socket);
                    }
                    else
                    {
                        Console.WriteLine($"{index} Received : {msgStr}");
                    }
                }
                index++;
            }

            foreach (var item in toRemove)
            {
                queleaWorkers.Remove(item);
            }

            toRemove.Clear();

        }

        private static void ProcessCommand(string command)
        {
            List<string> cmd_args = new List<string>(command.Split(' '));
            string[] args_only = null;
            if (cmd_args.Count > 1)
            {
                args_only = cmd_args.GetRange(1, cmd_args.Count - 1).ToArray();
            }
            if (CmdCallbacks.ContainsKey(cmd_args[0]))
                Console.WriteLine("Command returned with : " + CmdCallbacks[cmd_args[0]](args_only));
            else
                Console.WriteLine("Unknown command. Type help to list all available commands");
        }

        public static void PrintAsTable(IList list, string name)
        {
            Console.WriteLine($"--- {name} ---");
            foreach (var item in list)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine($"--- Total number : {list.Count} ---");
        }

        #region Resources
        private readonly static string LOGO = @"
__/\\\________/\\\_____/\\\\\\\\\_____/\\\______________/\\\__/\\\________/\\\_        
 _\/\\\_______\/\\\___/\\\\\\\\\\\\\__\/\\\_____________\/\\\_\/\\\_____/\\\//__       
  _\/\\\_______\/\\\__/\\\/////////\\\_\/\\\_____________\/\\\_\/\\\__/\\\//_____      
   _\/\\\\\\\\\\\\\\\_\/\\\_______\/\\\_\//\\\____/\\\____/\\\__\/\\\\\\//\\\_____     
    _\/\\\/////////\\\_\/\\\\\\\\\\\\\\\__\//\\\__/\\\\\__/\\\___\/\\\//_\//\\\____    
     _\/\\\_______\/\\\_\/\\\/////////\\\___\//\\\/\\\/\\\/\\\____\/\\\____\//\\\___   
      _\/\\\_______\/\\\_\/\\\_______\/\\\____\//\\\\\\//\\\\\_____\/\\\_____\//\\\__  
       _\/\\\_______\/\\\_\/\\\_______\/\\\_____\//\\\__\//\\\______\/\\\______\//\\\_ 
        _\///________\///__\///________\///_______\///____\///_______\///________\///__

";
        #endregion
    }
}
