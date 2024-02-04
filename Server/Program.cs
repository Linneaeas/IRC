using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading;

namespace Server;
class Program
{
    private static List<string> loggedInClients = new List<string>();
    private static List<Socket> sockets = new List<Socket>();
    private static List<Socket> connectedClients = new List<Socket>();
    private static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    static void Main(string[] args)
    {
        DatabaseHandler.Initialize();

        serverSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 25500));
        serverSocket.Listen(5);

        Console.WriteLine("Server is listening...");

        Thread acceptThread = new Thread(AcceptClients);//Creates a new thread for accepting incoming client connections.
        acceptThread.Start();//Starts the thread to handle the AcceptClients method.


        while (true)//checks for incoming connections and messages from clients.
        {
            if (serverSocket.Poll(0, SelectMode.SelectRead))//Checks if the server socket is ready to be read (i.e., if there are incoming connections).
            {
                Socket client = serverSocket.Accept();//Accepts an incoming client connection, and the client socket is added to the sockets list.
                Console.WriteLine("A client has connected!");
                sockets.Add(client);
            }

            foreach (Socket client in sockets)// Iterates through all connected clients.
            {
                if (client.Poll(0, SelectMode.SelectRead))//Checks if there is incoming data from a client.
                {
                    byte[] incoming = new byte[5000];
                    int read = client.Receive(incoming);// Receives the incoming message from the client.

                    string message = System.Text.Encoding.UTF8.GetString(incoming, 0, read);

                    Console.WriteLine("From a client: " + message);

                    ProcessMessage(client, message);
                }
            }
        }
    }

    static void ProcessMessage(Socket client, string message)
    {
        string[] parts = message.Split('|');

        string username, password, createAccountResponse, loginResponse, logoutResponse, sendMessageResponse, chatMessage;

        switch (parts[0])
        {
            case "CREATE_ACCOUNT":
                username = parts[1];
                password = parts[2];

                if (DatabaseHandler.IsUsernameExists(username))
                {
                    createAccountResponse = "ACCOUNT_CREATION_FAILED";
                }
                else
                {
                    DatabaseHandler.InsertUser(username, password);
                    Console.WriteLine($"Creating account for user: {username} with password: {password}");
                    createAccountResponse = "ACCOUNT_CREATED";
                }
                SendResponse(client, createAccountResponse);
                break;

            case "LOGIN":
                username = parts[1];
                password = parts[2];

                if (DatabaseHandler.AuthenticateUser(username, password))
                {
                    connectedClients.Add(client);
                    loggedInClients.Add(username);
                    Console.WriteLine($"Login successful for user: {username}");
                    loginResponse = "LOGIN_SUCCESSFUL";
                    SendConnectedClientsList(client);
                }
                else
                {
                    Console.WriteLine($"Login failed for user: {username}");
                    loginResponse = "LOGIN_FAILED";
                }
                SendResponse(client, loginResponse);
                break;

            case "LOGOUT":
                if (parts.Length == 2)
                {
                    username = parts[1];
                    connectedClients.Remove(client);
                    loggedInClients.Remove(username);
                    logoutResponse = "LOGOUT_SUCCESSFUL";
                    SendResponse(client, logoutResponse);
                }
                else
                {
                    Console.WriteLine("Invalid format for LOGOUT message.");
                }
                break;

            case "SEND_MESSAGE":
                string fromUsername = parts[1];
                string toUsernamesString = parts[2];
                List<string> toUsernames = toUsernamesString.Split(',').ToList();
                chatMessage = parts[3];

                DatabaseHandler.InsertMessage(fromUsername, toUsernames, chatMessage);
                sendMessageResponse = "MESSAGE_SENT";
                SendResponse(client, sendMessageResponse);
                break;

            default:
                Console.WriteLine("Invalid message received in ProcessMessage.");
                break;
        }
    }

    static void AcceptClients()
    {
        try
        {
            while (true)
            {
                Socket client = serverSocket.Accept();
                connectedClients.Add(client);

                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();//Each connected client has its own dedicated thread to handle incoming messages from that client.
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error in AcceptClients: " + ex.Message);
        }
    }

    static void HandleClient(Socket client)//Reads messages from the client and sends back a response. If an exception occurs, the client is removed from the list of connected clients, and its socket is closed.
    {
        try
        {
            while (true)
            {
                byte[] buffer = new byte[5000];
                int bytesRead = client.Receive(buffer);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received from client {client.RemoteEndPoint}: {receivedMessage}");

                SendResponse(client, "Server: " + receivedMessage);//Echo mess back to client
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleClient for {client.RemoteEndPoint}: {ex.Message}");
            connectedClients.Remove(client);
            client.Close();
        }
    }

    static void SendConnectedClientsList(Socket client)
    {
        string connectedClientsList = string.Join(",", loggedInClients);
        string responseMessage = $"CONNECTED_CLIENTS|{connectedClientsList}";
        SendResponse(client, responseMessage);
    }

    static void SendResponse(Socket client, string responseMessage)
    {
        byte[] responseData = Encoding.UTF8.GetBytes(responseMessage);
        client.Send(responseData);
    }
}