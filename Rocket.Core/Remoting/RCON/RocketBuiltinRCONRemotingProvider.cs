﻿using Rocket.API.Logging;
using Rocket.API.Providers;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
namespace Rocket.Core.Remoting.RCON
{
    public class RocketBuiltinRconRemotingProvider : MonoBehaviour
    {
        private static List<RconConnection> clients = new List<RconConnection>();
        private TcpListener listener;
        private bool exiting = false;
        private Thread waitingThread;
        ILoggingProvider Logging;
        private static Queue<string> commands = new Queue<string>();

        public void Awake()
        {
            Logging = R.Providers.GetProvider<ILoggingProvider>();
           // listener = new TcpListener(IPAddress.Any, 27015);
            listener.Start();

            // R.Logger.Log("Waiting for new connection...");

            waitingThread = new Thread(() =>
            {
                while (!exiting)
                {
                    RconConnection newclient = new RconConnection(listener.AcceptTcpClient());
                    clients.Add(newclient);
                    newclient.Send("RocketRcon v" + Assembly.GetExecutingAssembly().GetName().Version + "\r\n");
                    ThreadPool.QueueUserWorkItem(handleConnection, newclient);
                }
            });
            waitingThread.Start();
        }

        private void handleConnection(object obj)
        {
            try
            {
                RconConnection newclient = (RconConnection)obj;
                string command = "";
                while (newclient.Client.Client.Connected)
                {
                    Thread.Sleep(100);
                    command = newclient.Read();
                    if (command == "") break;
                    command = command.TrimEnd('\n', '\r', ' ');
                    if (command == "quit") break;
                    if (command == "ia")
                    {
                        //newclient.Send("Toggled interactive mode");
                        newclient.Interactive = !newclient.Interactive;
                    }
                    if (command == "") continue;
                    if (command == "login")
                    {
                        if (newclient.Authenticated)
                            newclient.Send("Notice: You are already logged in!\r\n");
                        else
                            newclient.Send("Syntax: login <password>");
                        continue;
                    }
                    if (command.Split(' ').Length > 1 && command.Split(' ')[0] == "login")
                    {
                        if (newclient.Authenticated)
                        {
                            newclient.Send("Notice: You are already logged in!\r\n");
                            continue;
                        }
                        else
                        {

                            //if (command.Split(' ')[1] == R.Settings.Instance.RCON.Password)
                            //{
                            //    newclient.Authenticated = true;
                            //    //newclient.Send("Success: You have logged in!\r\n");
                            //    //R.Logger.Log("Client has logged in!");
                            //    continue;
                            //}
                            //else
                            //{
                            //    newclient.Send("Error: Invalid password!\r\n");
                            //    R.Logger.Error("Client has failed to log in.");
                            //    break;
                            //}
                        }
                    }

                    if (command == "set")
                    {
                        newclient.Send("Syntax: set [option] [value]");
                        continue;
                    }
                    if (!newclient.Authenticated)
                    {
                        newclient.Send("Error: You have not logged in yet!\r\n");
                        continue;
                    }
                    if (command != "ia")
                        Logging.Log(LogLevel.INFO, "Client has executed command \"" + command + "\"");

                    lock (commands)
                    {
                        commands.Enqueue(command);
                    }
                    command = "";
                }


                clients.Remove(newclient);
                newclient.Send("Good bye!");
                Thread.Sleep(1500);
                Logging.Log(LogLevel.INFO, "Client has disconnected! (IP: " + newclient.Client.Client.RemoteEndPoint + ")");
                newclient.Close();

            }
            catch (Exception ex)
            {
                Logging.Log(LogLevel.ERROR, null, ex);
            }
        }

        private void FixedUpdate()
        {
            lock (commands)
            {
              //  while (commands.Count != 0)
                   // R.Execute(new ConsolePlayer(), commands.Dequeue());
            }
        }

        public static void Broadcast(string message)
        {
            foreach (RconConnection client in clients)
            {
                if (client.Authenticated)
                    client.Send(message);
            }
        }

        private void OnDestroy()
        {
            exiting = true;
            // Force all connected RCON clients to disconnect from the server on shutdown. The server will get stuck in the shutdown process until all clients disconnect.
            foreach (RconConnection client in clients)
            {
                client.Close();
            }
            clients.Clear();
            waitingThread.Abort();
            listener.Stop();
        }

        public static string Read(TcpClient client)
        {
            byte[] _data = new byte[1];
            string data = "";
            NetworkStream _stream = client.GetStream();

            while (true)
            {
                try
                {
                    int k = _stream.Read(_data, 0, 1);
                    if (k == 0)
                        return "";
                    char kk = Convert.ToChar(_data[0]);
                    data += kk;
                    if (kk == '\n')
                        break;
                }
                catch
                {
                    return "";
                }
            }
            return data;
        }

        public static void Send(TcpClient client, string text)
        {
            byte[] data = new UTF8Encoding().GetBytes(text);
            client.GetStream().Write(data, 0, data.Length);
        }
    }
}