using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

// data structures for holding player info
public static class Players
{
    public static Dictionary<int, TcpClient?> PlayersList { get; set; } = new();
    public static Dictionary<int, int> PlayersScores { get; set; } = new();
}

// serializable class for grocery list items
[Serializable]
public class GroceryList
{
    public required string ItemName { get; set; }
    public required int ItemPrice { get; set; }
    public required int ItemCount { get; set; }
    public int? RandomQuantity { get; set; }
}

// entry point
internal static class Program
{
    private static void Main(string[] args)
    {
        // check command args for port number
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: Server {0} <port>", AppDomain.CurrentDomain.FriendlyName);
            Environment.Exit(1);
        }

        var listenPort = int.Parse(args[0]);
        var server = new Server();
        server.Start(listenPort);
    }
}

public class Server
{
    private readonly List<TcpClient> _clients = new(); // list to hold all the Clients
    private readonly Dictionary<TcpClient, string> _clientUsernames = new(); // holds all usernames
    private readonly Inventory _inventory = new(); // new instance of GroceryInventory
    private readonly List<string> _finishedPlayers = new(); // to make sure all clients are finished

    public void Start(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Listening on port {port}");

        // infinite loop for handling client connections
        while (true)
        {
            var client = listener.AcceptTcpClient();

            // get client address info
            var remoteEndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
            var clientHostname = remoteEndPoint.Address.ToString();
            var clientPort = remoteEndPoint.Port;

            Console.WriteLine($"Connected to ({clientHostname}, {clientPort})");
            _clients.Add(client);

            var playerLimit = CheckPlayerLimit(client, _clients);
            if (playerLimit)
            {
                client.Close();
                _clients.Remove(client);

                continue; // if player limit is reached, skip client
            }

            // new thread to handle client communication
            var clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    private bool CheckPlayerLimit(TcpClient client,
        List<TcpClient> clients)
    {
        var playerLimit = false;

        if (clients.Count > 3)
        {
            var playerLimitAlert = "PlayerLimitReached:This game is full. Please try again later.";
            Console.WriteLine("Player limit has been reached.\n");

            SendMessage(client, playerLimitAlert);
            playerLimit = true;

            Console.WriteLine("Client disconnected");
        }
        else
        {
            SendMessage(client, "PlayerLimitNotReached:Player count not reached.");
            Console.WriteLine($"Player count: {clients.Count}");
        }

        return playerLimit;
    }

    private void HandleClient(TcpClient client)
    {
        // clean up inventory
        var inventoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "processed_inventory.csv");
        var lines = File.ReadAllLines(inventoryFilePath);

        // sort and rewrite data in file
        var sortedLines = lines.OrderBy(line => line).ToList();
        File.WriteAllLines(inventoryFilePath, sortedLines);

        // updated inventory file for gameplay
        var inventoryFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grocerylist.csv");

        try
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];
            var sentMessage = false;

            // get username from client
            GetUsername(client, stream, buffer);

            // make sure all players are ready
            WaitForPlayers(client, sentMessage);

            while (true)
            {
                // reading message from client
                var bytesReads = stream.Read(buffer, 0, buffer.Length);
                if (bytesReads <= 0)
                    break;

                var clientMessage = Encoding.UTF8.GetString(buffer, 0, bytesReads);
                var action = clientMessage.Split(':');

                // close client here
                if (action[0] == "Exit")
                    break;

                MessageAction(client, action, inventoryFilePath, inventoryFile);

                if (_finishedPlayers.Count == 3) ScoreBoard();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error handling client " + e.Message);
        }
        finally
        {
            RemoveClient(client);
        }
    }

    private void RemoveClient(TcpClient client)
    {
        lock (_clientUsernames)
        {
            if (_clients.Contains(client))
                _clients.Remove(client);
        }

        client.Close();
        Console.WriteLine("Client disconnected");
    }


    private void ScoreBoard()
    {
        Console.WriteLine("All players have finished.");

        var playerScores = new List<(string username, int score)>();

        for (var i = 0; i < 3; i++)
        {
            var player = Players.PlayersList.GetValueOrDefault(i); // Get TcpClient by player slot
            if (player != null)
                if (_clientUsernames.ContainsKey(player) && Players.PlayersScores.TryGetValue(i, out var score))
                    playerScores.Add((_clientUsernames[player], score));
        }

        // sort players by score in descending order
        var rankedPlayers = playerScores.OrderByDescending(player => player.score).ToList();

        var rankingMessage = string.Join("\n", rankedPlayers.Select(player => $"{player.username}: {player.score}"));
        BroadcastMessage($"FinalScores-\n{rankingMessage}");

        _finishedPlayers.Clear();
        _clientUsernames.Clear();
        Console.WriteLine();
    }

    private void BroadcastMessage(string message)
    {
        // send message to each client
        foreach (var client in _clientUsernames.Keys)
            SendMessage(client, message);
    }


    // handle client actions
    private void MessageAction(TcpClient client, string[] action, string inventoryFilePath, string inventoryFile)
    {
        switch (action[0])
        {
            case "GetInventory":
                _inventory.LoadInventory(inventoryFilePath);
                _inventory.RandomizeGroceryList(client, _clientUsernames[client]);
                break;
            case "UpdateInventory":
                _inventory.UpdateInventory(client, inventoryFile, action[1], action[2]);
                break;
            case "RefreshInventory":
                _inventory.RefreshClientList(client, inventoryFile);
                break;
            case "FinalList":
                var currentUser = action[1];
                var budget = int.Parse(action[2]);
                var itemsBought = int.Parse(action[3]);

                _inventory.CalculateAssets(client, currentUser, budget, itemsBought);
                _finishedPlayers.Add(currentUser);
                break;
            default:
                Console.WriteLine("Unknown action " + action[0]);
                break;
        }
    }

    // ensure all players are connected before starting the game
    private void WaitForPlayers(TcpClient client, bool sentMessage)
    {
        while (_clientUsernames.Count < 3)
            if (!sentMessage)
            {
                sentMessage = true;
                var playersNeeded = 3 - _clientUsernames.Count;
                var message =
                    $"WaitingForPlayers:Player count is {_clientUsernames.Count}. We need {playersNeeded} more player(s).";
                SendMessage(client, message);
            }

        // check if _clientUsernames updated
        if (_clientUsernames.Keys.Last() == client)
        {
            BroadcastMessage("AllPlayersConnected");
            Console.WriteLine("Game is starting...\n");
        }
    }

    // get the username of the client and assign them a slot
    private void GetUsername(TcpClient client, NetworkStream stream, byte[] buffer)
    {
        // reading username from client
        var usernameBytes = stream.Read(buffer, 0, buffer.Length);
        if (usernameBytes <= 0)
            Console.WriteLine("Error reading from client.");

        var username = Encoding.UTF8.GetString(buffer, 0, usernameBytes);

        lock (_clientUsernames)
        {
            _clientUsernames[client] = username;

            var playerSlot = _clientUsernames.Count;
            if (playerSlot <= 3) // make sure player count is only 3
            {
                Players.PlayersList[playerSlot - 1] = client; // index 0 - 2
                Players.PlayersScores[playerSlot - 1] = 0; // initialize score to 0 for the player
            }
        }

        Console.WriteLine($"({client.Client.RemoteEndPoint}) has connected with username: {username}");
    }

    // helper to send message
    public static void SendMessage(TcpClient client, string message)
    {
        try
        {
            var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes(message);
            if (stream.CanWrite)
            {
                // write the message
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(); // ensure all data is sent
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending message to client: {e.Message}");
        }
    }
}