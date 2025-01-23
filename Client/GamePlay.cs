using System.Net.Sockets;
using System.Text.Json;

namespace Client;

public class GamePlay
{
    private static List<GroceryList> _groceryList = new();
    private List<GroceryList> _availableItems = new();
    private readonly List<Cart> _cart = new();

    public static void SetGroceryList(List<GroceryList> groceryList)
    {
        _groceryList = groceryList;
    }

    public void Play(TcpClient client)
    {
        _availableItems = new List<GroceryList>(_groceryList);
        int budget = 40,
            budgetLimitCnt = 0;

        while (true)
        {
            try
            {
                var itemAdded = true;
                var price = 0; // restarting price

                // print out new grocery list
                NewGroceryList(budget);

                Console.Write("Please enter an item to add to your cart: ");
                var input = Console.ReadLine();

                var itemInput = NullCheck(input, "an item");

                // validate if item exists in shopping cart
                if (!ValidItem(_availableItems, itemInput)) continue;

                Console.Write("Please enter a quantity: ");
                input = Console.ReadLine();

                const string enterValidQuantity = "Error: Please enter a valid quantity.\n";

                // check if input is a number
                if (!int.TryParse(input, out var quantity))
                {
                    Console.WriteLine(enterValidQuantity);
                    PressEnter();

                    continue;
                }

                if (quantity <= 0)
                {
                    Console.WriteLine(enterValidQuantity);
                    PressEnter();

                    continue;
                }

                Console.WriteLine();

                RefreshInventory(client);

                // process item purchase
                foreach (var grocery in _availableItems.ToList())
                {
                    if (grocery.ItemName != itemInput) continue;

                    price = quantity * grocery.ItemPrice;

                    // check if item is in low stock
                    if (CheckStock(grocery, ref quantity, ref itemAdded, ref price)) break;
                }
                
                if (!itemAdded)
                {
                    Console.WriteLine(new string('-', 50));
                    Console.WriteLine();
                    Console.WriteLine($"You still have ${budget} left.\n");
                    Console.WriteLine("Type 'checkout' or press [enter] to continue.");

                    if (Console.ReadLine() == "checkout")
                        break;

                    continue;
                }


                RefreshInventory(client);

                var availability = CheckAvailability(_availableItems, itemInput, quantity);
                if (!availability)
                    continue;

                // process successful purchase
                if (ProcessCheckout(client, price, itemInput, quantity, ref budget, ref budgetLimitCnt)) break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        Console.WriteLine("\nThank you for your purchase!");
        var sendMessage = $"FinalList:{GlobalVariables.CurrentUser}:{budget}:{_cart.Count}";

        Client.SendMessage(client, sendMessage);
    }

    private bool ProcessCheckout(TcpClient client, int price, string itemInput, int quantity, ref int budget,
        ref int budgetLimitCnt)
    {
        if (budget - price < 0)
        {
            Console.WriteLine("This item goes below your budget!\n");
            budgetLimitCnt++;
        }
        else
        {
            budget -= price;

            AddToCart(_cart, itemInput, quantity);

            Console.WriteLine($"{itemInput} x({quantity}) has been added to your cart.");
            Console.WriteLine($"You have ${budget} left.\n");

            UpdateInventory(client, itemInput, quantity);
            UpdateList(itemInput, quantity);

            _groceryList = new List<GroceryList>(_availableItems);
        }

        if (budget <= 0 || budgetLimitCnt == 3)
        {
            Console.WriteLine("You don't have enough money for any other items. Time to checkout!");
            return true;
        }

        Console.WriteLine("Type 'checkout' to purchase your cart or press [enter] to continue.");
        var choice = Console.ReadLine();
        if (choice?.ToLower() == "checkout")
            return true;
        return false;
    }

    private void NewGroceryList(int budget)
    {
        Console.Clear();
        Console.WriteLine($"\t\tGood luck {GlobalVariables.CurrentUser}!");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"You have ${budget} left.");
        Console.WriteLine("Your current shopping list: ");

        // print out all the available items
        foreach (var item in _availableItems)
            Console.WriteLine(
                $"${item.ItemPrice} - {item.ItemName} - quantity: {item.RandomQuantity}");

        Console.WriteLine();
    }

    private bool CheckStock(GroceryList grocery, ref int quantity, ref bool itemAdded, ref int price)
    {
        if (grocery.ItemCount - quantity <= 2)
        {
            if (grocery.ItemCount <= 0)
            {
                Console.WriteLine("Item is out of stock!");
                itemAdded = false;
                return true;
            }

            itemAdded = LowStock(grocery, ref quantity, ref price);
        }

        return false;
    }

    private void PressEnter()
    {
        Console.WriteLine("Press [enter] to continue.");
        Console.ReadLine();
        Console.WriteLine();
    }
    
    private bool ValidItem(List<GroceryList> availableItems, string? itemName)
    {
        var validItem = availableItems.Any(item => item.ItemName == itemName);

        if (!validItem)
        {
            Console.WriteLine("Error: Item is not in shopping list.\n");
            PressEnter();
        }

        return validItem;
    }

    private string NullCheck(string? input, string inputType)
    {
        var nullError = "Error: Input cannot be null or empty.";

        if (inputType == "YesOrNo")
            while (string.IsNullOrEmpty(input) && input != null && input[0] != 'y' && inputType != "n")
            {
                Console.WriteLine("Please enter 'y' or 'n'.");
                input = input.ToLower();
            }
        else
            while (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine(nullError);
                Console.Write($"\nPlease enter {inputType} to add to your cart: ");
                input = Console.ReadLine();
            }

        input = input!.ToLower();
        return input;
    }

    private bool LowStock(GroceryList grocery, ref int quantity, ref int price)
    {
        Console.WriteLine($"Wait! There is only {grocery.ItemCount} left in stock!");

        if (grocery.ItemCount > quantity)
        {
            Console.Write($"Would you still like to get these {quantity} items? (y/n): ");
        }
        else
        {
            Console.Write($"Would you like to get the {grocery.ItemCount} left in stock? (y/n): ");
            quantity = grocery.ItemCount;
        }

        var lowStockInput = Console.ReadLine();

        lowStockInput = NullCheck(lowStockInput, "YesOrNo");

        if (lowStockInput[0] == 'n') return false;

        var input = LowStockOptions();
        switch (input)
        {
            case 1:
                price = DoublePrice(grocery, quantity);
                break;
            case 2:
                if (!RandomRiddle())
                    return false;
                break;
            case 3:
                if (!RollDoubles())
                    return false;
                break;
            default:
                Console.WriteLine("Invalid input!");
                return false;
        }
        
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();
        
        return true;
    }

    private static int LowStockOptions()
    {
        // menu
        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
        Console.WriteLine("Welcome to the low stock mode!");
        Console.WriteLine("You have 3 options:");
        Console.WriteLine("1. Pay double the price.");
        Console.WriteLine("2. Answer a riddle.");
        Console.WriteLine("3. Roll doubles.");
        Console.Write("Please enter your option [1, 2, 3]: ");

        int input = Convert.ToInt16(Console.ReadLine());

        return input;
    }

    private static int DoublePrice(GroceryList grocery, int quantity)
    {
        var price = grocery.ItemPrice * 2 * quantity;
        Console.WriteLine($"\nYour new total for {grocery.ItemName} is ${price}");
        return price;
    }

    private bool RandomRiddle()
    {
        var riddles = new List<(string Question, string Answer)>
        {
            ("What gets bigger the more you take away? a ... ", "hole"),
            ("What has a head and a tail but no body? a ...", "coin"),
            ("I am easy to lift but hard to throw. what am i? ... ", "feather"),
            ("What goes through a glass without breaking?", "light"),
            ("What travels around the world but remains in the corner?", "stamp")
        };

        var random = new Random();
        var index = random.Next(0, riddles.Count);
        var triesLeft = 3;
        var tries = "tries";
        var success = false;

        Console.WriteLine("\nYou have 3 tries. Please type one word only for the answer.");
        Console.WriteLine(riddles[index].Question);
        while (triesLeft > 0)
        {
            Console.Write("Answer: ");
            var answer = Console.ReadLine();

            while (string.IsNullOrWhiteSpace(answer)) Console.Write("Please enter a valid answer: ");

            answer = answer.Trim().ToLower();

            Console.WriteLine();

            if (answer == "quit")
                break;

            if (answer == riddles[index].Answer)
            {
                Console.WriteLine("Correct!");
                success = true;
                break;
            }

            triesLeft--;

            if (triesLeft == 1)
                tries = "try";

            Console.WriteLine($"\nWrong answer! You have {triesLeft} {tries} left.");
        }

        return success;
    }

    private bool RollDoubles()
    {
        Random rand = new Random();
        int dice1 = rand.Next(1, 7);
        int dice2 = rand.Next(1, 7);

        Console.WriteLine($"\nYou rolled a {dice1} and {dice2}");
        
        return dice1 == dice2;
    }

    // method to update the inventory in live time
    private void UpdateInventory(TcpClient client, string? item, int quantity)
    {
        var updateMessage = $"UpdateInventory:{item}:{quantity}";
        Client.SendMessage(client, updateMessage);

        ReceiveUpdatedClientList();
    }

    private void ReceiveUpdatedClientList()
    {
        string? receivedGroceryList = null;

        while (receivedGroceryList == null)
            if (Client.MessageQueue.TryDequeue(out var serverGroceryList))
                receivedGroceryList = serverGroceryList; // retrieve the response

        // deserialize the received grocery list
        var groceryList = JsonSerializer.Deserialize<List<GroceryList>>(receivedGroceryList);

        // ensure groceryList is not null
        if (groceryList == null) Console.WriteLine("Error: Failed to retrieve updated grocery list.");

        if (groceryList == null)
            return;

        foreach (var grocery in groceryList)
        foreach (var item in _availableItems)
            if (item.ItemName == grocery.ItemName)
                grocery.RandomQuantity = item.RandomQuantity;

        SetGroceryList(groceryList);
    }

    private void UpdateList(string itemInput, int quantity)
    {
        for (var i = 0; i < _availableItems.Count; i++)
        {
            var item = _availableItems[i];

            if (item.ItemName == itemInput)
            {
                if (item.RandomQuantity == quantity)
                    // remove the entire item
                    _availableItems.Remove(item);
                else if (item.RandomQuantity > quantity)
                    _availableItems[i] = new GroceryList
                    {
                        ItemName = item.ItemName, // keeping the name as-is
                        ItemPrice = item.ItemPrice, // keeping the price as-is
                        ItemCount =
                            item.ItemCount - quantity, // setting quantity to 0 after purchase
                        RandomQuantity =
                            item.RandomQuantity - quantity // setting random quantity to 0 after purchase
                    };

                break;
            }
        }

        SetGroceryList(_availableItems);
    }

    private void RefreshInventory(TcpClient client)
    {
        string? receivedUpdatedList = null;

        Client.SendMessage(client, "RefreshInventory");

        while (receivedUpdatedList == null)
            if (Client.MessageQueue.TryDequeue(out var serverGroceryList))
                receivedUpdatedList = serverGroceryList; // retrieve the response

        var updatedGroceryList = JsonSerializer.Deserialize<List<GroceryList>>(receivedUpdatedList);

        // checking if received list is null
        if (updatedGroceryList == null)
        {
            Console.WriteLine("Error: Failed to retrieve updated grocery list.");
            return;
        }

        foreach (var grocery in updatedGroceryList)
        {
            var matchedItem = _availableItems.FirstOrDefault(item => item.ItemName == grocery.ItemName);

            if (matchedItem != null) grocery.RandomQuantity = matchedItem.RandomQuantity;
        }

        SetGroceryList(updatedGroceryList);

        _availableItems = updatedGroceryList
            .Where(grocery => _availableItems.Any(item => item.ItemName == grocery.ItemName))
            .ToList();
    }

    private bool CheckAvailability(List<GroceryList> groceryList, string? item, int quantity)
    {
        foreach (var grocery in groceryList)
            if (grocery.ItemName == item)
            {
                if (grocery.ItemCount == 0)
                {
                    Console.WriteLine("Sorry, this item is now out of stock!");
                    PressEnter();

                    return false;
                }

                if (quantity > grocery.ItemCount)
                {
                    Console.WriteLine($"Sorry, we don't have {quantity} items left!");
                    Console.WriteLine("Please add the item to your cart again.\n");
                    PressEnter();

                    return false;
                }
            }

        return true;
    }

    private void AddToCart(List<Cart> cart, string itemAdded, int quantityBought)
    {
        bool itemExists = false;

        foreach (var item in cart)
        {
            if (item.ItemName == itemAdded) // item added
            {
                item.QuantityBought += quantityBought;
                itemExists = true;
                break;
            }
        }

        if (!itemExists)
        {
            cart.Add(new Cart()
            {
                ItemName = itemAdded,
                QuantityBought = quantityBought,
            });
        }
    }
    
}