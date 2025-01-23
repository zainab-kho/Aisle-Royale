using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client;

public static class GlobalVariables
{
    public static string CurrentUser { get; set; } = string.Empty;
    public const int BufferSize = 1024;
}

[Serializable]
public class GroceryList
{
    public required string ItemName { get; set; }
    public required int ItemPrice { get; set; }
    public required int ItemCount { get; set; }
    public required int RandomQuantity { get; set; }
}

public class Cart
{
    public required string ItemName { get; init; }
    public int QuantityBought { get; set; }
}

public static class GlobalState
{
    
}

public static class Client
{
    private static readonly GamePlay StartGame = new();
    public static readonly ConcurrentQueue<string?> MessageQueue = new();

    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: client <ip> <port>");
            Environment.Exit(1);
        }

        var host = args[0];
        var port = int.Parse(args[1]);

        var client = new TcpClient(host, port);
        var receiveThread = new Thread(() => ReceiveMessage(client));
        receiveThread.Start();

        Thread.Sleep(100);
        ClientStart(client);
    }

    private static void ReceiveMessage(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[GlobalVariables.BufferSize];

            while (true)
            {
                // read message from the server
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                var serverMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                //Console.WriteLine("Received from server: " + serverMessage);

                var action = serverMessage.Split(':');

                if (action[0] == "WaitingForPlayers")
                {
                    Console.WriteLine(action[1]);
                    continue;
                }

                // add the message to the queue
                MessageQueue.Enqueue(serverMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error receiving message from server: " + ex.Message);
        }
    }

    public static void SendMessage(TcpClient client, string message)
    {
        var stream = client.GetStream();
        var messageBytes = Encoding.ASCII.GetBytes(message);
        stream.Write(messageBytes, 0, messageBytes.Length);
    }

    private static void ClientStart(TcpClient client)
    {
        // getting network stream to send messages to the server
        var stream = client.GetStream();
        string? receivedGroceryList = null;
        string? playerLimitMessage = null;

        if (MessageQueue.TryDequeue(out var playerLimitCheck))
            playerLimitMessage = playerLimitCheck; // retrieve the response

        if (playerLimitMessage != null)
        {
            var playerLimit = playerLimitMessage.Split(':');

            // if more than three people in the game
            if (playerLimit[0] == "PlayerLimitReached")
            {
                Console.WriteLine(playerLimit[1]);
                return;
            }
        }

        // getting username from client
        var username = GetUsername(client);

        CheckGameStatus();

        PrintRules(username);

        var groceryList = GetGroceryList(client, receivedGroceryList);

        Console.WriteLine("Your personalized shopping list: ");
        if (groceryList != null)
        {
            foreach (var item in groceryList)
                Console.WriteLine(
                    $"${item.ItemPrice} - {item.ItemName} - quantity: {item.RandomQuantity}");

            // setting grocery list so GamePlay can access
            GamePlay.SetGroceryList(groceryList);
        }

        Console.Write("\nPlease press enter to continue.");
        Console.ReadKey(true);

        // starting Gameplay
        StartGame.Play(client);

        var gameFinished = GameFinished();

        PrintResults(gameFinished);

        SendMessage(client, "Exit");
    }

    private static void PrintResults(string[] gameFinished)
    {
        Console.Clear();
        Console.WriteLine("Scoreboard: ");
        Console.WriteLine(new string('-', 35));

        var results = gameFinished[1].Split('\n');
        Console.WriteLine($"First place: {results[1]}");
        Console.WriteLine($"Second place: {results[2]}");
        Console.WriteLine($"Third place: {results[3]}");
        Console.WriteLine();
    }

    private static string[] GameFinished()
    {
        Console.WriteLine("\nWaiting for all players to finish...");

        string? gameFinishedMessage = null;

        while (gameFinishedMessage == null)
            // wait for all players
            if (MessageQueue.TryDequeue(out var serverFinished))
                gameFinishedMessage = serverFinished;

        var gameFinished = gameFinishedMessage.Split('-');

        return gameFinished;
    }

    private static List<GroceryList>? GetGroceryList(TcpClient client, string? receivedGroceryList)
    {
        // receive randomized grocery list for client
        SendMessage(client, "GetInventory");

        while (receivedGroceryList == null)
            if (MessageQueue.TryDequeue(out var serverGroceryList))
                receivedGroceryList = serverGroceryList; // retrieve the response

        // deserialize the received grocery list
        var groceryList = JsonSerializer.Deserialize<List<GroceryList>>(receivedGroceryList);

        // ensure groceryList is not null
        if (groceryList == null)
        {
            Console.WriteLine("Error: Failed to retrieve the grocery list.");
            return groceryList;
        }

        return groceryList;
    }

    private static void PrintRules(string username)
    {
        Console.WriteLine($"\nWelcome {username}!");
        Console.WriteLine("The rules of this game are:");
        Console.WriteLine("1. Add the list of items from your list to your cart.");
        Console.WriteLine("2. Don't go below your budget.");
        Console.WriteLine("3. The player with the most items from their list and the least money spent wins!");
        Console.WriteLine("Your budget is $40\n");
    }

    private static void CheckGameStatus()
    {
        string? gameReadyMessage = null;

        // wait for all players
        while (gameReadyMessage == null)
            if (MessageQueue.TryDequeue(out var serverReady))
                gameReadyMessage = serverReady; // retrieve the response

        var gameReady = gameReadyMessage.Split(':');

        if (gameReady[0] == "AllPlayersConnected") return;

        Console.WriteLine("Players are not connecting. Please try again.");
        Environment.Exit(1);
    }

    private static string GetUsername(TcpClient client)
    {
        Console.Write("Please enter a username: ");

        var username = Console.ReadLine() ?? string.Empty;

        while (string.IsNullOrEmpty(username))
        {
            Console.Write("Please enter a valid username: ");
            username = Console.ReadLine() ?? string.Empty;
        }

        GlobalVariables.CurrentUser = username;

        // sending username to Server
        SendMessage(client, username.ToLower());
        return username;
    }
}