using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server;

public class Inventory
{
    private readonly List<GroceryList> _inventory = new();
    private readonly List<List<GroceryList>> _randomGroceryLists = new();

    // initialize lists for each player
    public Inventory()
    {
        for (var i = 0; i < 3; i++)
            _randomGroceryLists.Add(new List<GroceryList>());
    }

    private int CheckPlayer(TcpClient client)
    {
        // loop through each player and check if the client matches
        foreach (var player in Players.PlayersList)
            if (player.Value == client)
                return player.Key; // return the player index (key)

        return -1;
    }

    public void LoadInventory(string data)
    {
        try
        {
            var lines = File.ReadAllLines(data);
            var outputLines = new List<string>();

            // process each line in file
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split(',');

                if (values.Length == 3)
                    try
                    {
                        var item = new GroceryList
                        {
                            ItemName = values[0].Trim(),
                            ItemPrice = int.Parse(values[1].Trim()),
                            ItemCount = int.Parse(values[2].Trim())
                        };

                        _inventory.Add(item);

                        // add lines to list to add to file
                        outputLines.Add($"{item.ItemName},{item.ItemPrice},{item.ItemCount}");
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine($"Error: Invalid data format in line: {string.Join(",", values)}");
                    }
                else
                    Console.WriteLine($"Error: Invalid data format in line: {line}");
            }

            // write the processed lines to a new file
            File.WriteAllLines("grocerylist.csv", outputLines);
        }
        catch (FileNotFoundException fnf)
        {
            Console.WriteLine($"Error: File not found: {fnf.Message}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error loading inventory: {e.Message}");
        }
    }

    public void RandomizeGroceryList(TcpClient client, string username)
    {
        var rnd = new Random(); // for randomized quantity
        var random = new Random(); // for randomized list

        var currentUser = CheckPlayer(client);

        if (currentUser == -1)
            return;

        // get 6 random items from the inventory
        _randomGroceryLists[currentUser] = _inventory
            .OrderBy(_ => random.Next())
            .DistinctBy(item => item.ItemName)
            .Take(6)
            .Select(item => new GroceryList
            {
                ItemName = item.ItemName,
                ItemPrice = item.ItemPrice,
                ItemCount = item.ItemCount,
                RandomQuantity = rnd.Next(1, 5) // randomized quantity
            })
            .ToList();

        // serialize the list to json
        var serializedData = JsonSerializer.Serialize(_randomGroceryLists[currentUser]);
        Server.SendMessage(client, serializedData);
    }

    public void UpdateInventory(TcpClient client, string data, string item, string quantityStr)
    {
        var lines = File.ReadAllLines(data);
        var quantity = int.Parse(quantityStr);
        var itemFound = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var values = line.Split(',');

            if (values[0].Trim() == item)
            {
                var currentQuantity = int.Parse(values[2].Trim());
                var newQuantity = currentQuantity - quantity;

                newQuantity = Math.Max(newQuantity, 0);

                // update the line with the new quantity
                lines[i] = $"{values[0].Trim()},{values[1].Trim()},{newQuantity}";
                itemFound = true;

                Console.WriteLine("Updated file: " + lines[i]);
                break;
            }
        }

        if (!itemFound) Console.WriteLine($"{item} not found");

        File.WriteAllLines(data, lines);

        RefreshClientList(client, data);
    }

    public void RefreshClientList(TcpClient client, string data)
    {
        var stream = client.GetStream();
        var lines = File.ReadAllLines(data);

        var currentUser = CheckPlayer(client);

        if (currentUser == -1)
            return;

        foreach (var line in lines)
        {
            var values = line.Split(',');

            foreach (var grocery in _randomGroceryLists[currentUser])
                if (grocery.ItemName == values[0])
                    grocery.ItemCount = int.Parse(values[2]);
        }

        // serialize the list to json
        var serializedData = JsonSerializer.Serialize(_randomGroceryLists[currentUser]);
        var write = Encoding.UTF8.GetBytes(serializedData);
        stream.Write(write, 0, write.Length);
    }

    public void CalculateAssets(TcpClient client, string username, int budget, int itemsBought)
    {
        var currentUser = CheckPlayer(client);

        if (currentUser == -1)
            return;

        // calculating player assets
        var assets = budget + itemsBought * 10 * 7;

        if (budget >= 40 || budget <= 0)
            assets = 0;

        Players.PlayersScores[currentUser] = assets;
    }
}