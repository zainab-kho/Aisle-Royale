# Description
This project is a multiplayer grocery store simulation game where players manage a unique grocery list and budget. Players browse available items, add them to their cart, and make purchases. During the game, they must handle low-stock situations through riddles, negotiations, or rolling doubles. If they are too slow, another player can take the item. The game ends with the player who has the most assets winning.

## How to Run
1. Clone this repository to your local machine.
2. Open the solution in Visual Studio or Rider.
3. Build and run the project.
4. Use the client to interact with the server, using 'localhost:<port> '.

## How to Play

1. Start the game and select your player.
2. The game only starts when 3 players are logged in. Each player manages a unique grocery list and budget.
3. Browse the list of items and add them to your cart.
4. If you add a low-stock item to your cart, you can:
   - Pay double the price
   - Solve a riddle.
   - Roll doubles.
6. If you’re too slow, another player can steal your item.
7. Keep track of your budget and make strategic purchases.
8. The game ends when the player with the most assets wins.

## Features

### Client-side:
- Browse and purchase items from a grocery list.
- Handle low-stock items through riddles, negotiations, and rolling doubles.
- Manage your budget and checkout process.

### Server-side:
- Manages player inventory and assets.
- Handles concurrent requests from multiple players.
- Ensures smooth gameplay with correct inventory and player states.

## Technologies Used
- C#
- System.IO
- System.Net.Sockets
- System.Random
- **JSON Serializer** (for serializing data)
- **JetBrains Rider** (IDE)
