# nacho-game

Nachomen is a turn based multiplayer game using a Neo smart contract as backend.

In Nachomen players can collect different "luchadores", which can then be pit against other players luchadores.

The game frontend is implemented as a C# game client running on Unity engine.

The smart contract does all the game logic, including game accounts, turn-based battles, and gym (training luchadores) and auction (selling/buying luchadores).

Each time a new Neo address joins the game, the contract generates a new random luchador as a non-fungible token.