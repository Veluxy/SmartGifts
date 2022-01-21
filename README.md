## Features
Distribute gifts to any player when they awake from sleep or when manually claimed.
- Gift an item to specific players regardless of status (online/offline/alive/dead/sleeping).
- Gift an item to all players.
- Automatically distribute the gift item when a player awakes from sleep.
- Manually claim gift items using a chat command.
- Delete a gift item entry on map wipe.
- Detailed logging to console, txt file and discord.

## Permissions
 - `smartgifts.admin` -- Allows player to use the admin commands

## Player Commands
Works for chat only
- `/sclaim` -- Manually claim any outstanding gift(s).

## Admin Commands
Works for both chat and console
- `sgift.entry.new_item "<item shortname>" "<item amount>" "<distribution type>" "<players steamID64 (optional)>" "<delete on map wipe (optional)>"` -- Creates a new entry.
- `sgift.entry.list"` -- List all entries id.
- `sgift.entry.view "<entry id>"` -- View details about an entry.
- `sgift.entry.delete "<entry id>"` -- Deletes an entry.
- `sgift.entry.add_pending_players "<entry id>" "<players steamID64>"` -- Add pending players to an entry.
- `sgift.entry.remove_pending_player "<entry id>" "<player steamID64>"` -- Remove pending player from an entry.
- `sgift.entry.add_given_players "<entry id>" "<players steamID64>"` -- Add given players to an entry.
- `sgift.entry.remove_given_player "<entry id>" "<player steamID64>"` -- Remove given player from an entry.

#### Examples
 - `sgift.entry.new_item "wood" "100" "SpecificPlayers" "765111xxxxxxxxxxx,765111xxxxxxxxxxx" "false"`
 - `sgift.entry.new_item "scrap" "200" "MapPlayers" "" "true"`
 - `sgift.entry.new_item "wood" "100" "Everyone"`

#### Notes
- `<players steamID64>` accepts multiple values separated by commas.
- `<delete on map wipe>` only accepts the value **"true"** or **"false"**.

#### Gift Distribution Types
 1. **SpecificPlayers**
    - Adds the given input players to the pending player list.
    - Distributes to players from the pending players list and then remove them from the list.
 2. **MapPlayers**
    - Adds all players that are alive or sleeping on the map (using `BasePlayer.allPlayerList`) to the pending player list.
    - Distributes to players from the pending players list and then remove them from the list.
 3. **ServerPlayers**
    - Adds all players that have joined the server before (using `covalence.Players.All`) to the pending player list.
 4. **Everyone**
    - Distributes to any player that joins the server and then add them to the given players list.

#### Item Shortname
You can find a list of items shortname by searching the web for `rust item shortname list` or from [https://github.com/OrangeWulf/Rust-Docs/blob/master/Items.md](https://github.com/OrangeWulf/Rust-Docs/blob/master/Items.md)

## Configuration
```json
{
  "Prefix": "SmartGifts",
  "PrefixColor": "yellow",
  "GiftClaimCommand": "sclaim",
  "Permissions": {
    "Admin": "smartgifts.admin",
  },
  "DiscordMessages": {
    "Enabled": true,
    "WebhookURL": "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
    "LogCommand": true, // logs admin commands
    "LogPlayerGiveAttempt": true // logs gift distributions
  },
  "FileLogging": {
    "Enabled": true,
    "LogCommand": true,  // logs admin commands
    "LogPlayerGiveAttempt": true // logs gift distributions
  }
}
```

## Data File Example
```json
{
  "Entries": [
    {
      "ID": "81249936",
      "Gift": {
        "Item ShortName": "wood",
        "Item Amount": 500
      },
      "Distribution Type (SpecificPlayers/MapPlayers/ServerPlayers/Everyone)": "MapPlayers",
      "Delete Entry On Map Wipe (true/false)": false,
      "Pending Players": [
        76511100000000000
      ],
      "Given Players": []
    },
    {
      "ID": "81264171",
      "Gift": {
        "Item ShortName": "scrap",
        "Item Amount": 100
      },
      "Distribution Type (SpecificPlayers/MapPlayers/ServerPlayers/Everyone)": "Everyone",
      "Delete Entry On Map Wipe (true/false)": true,
      "Pending Players": [],
      "Given Players": [
        76511100000000000
      ]
    }
  ]
}
```

## Log Location
The log files are located in `oxide/logs/SmartGifts/smartgifts_-YYYY-MM-DD.txt`.

## Localization
```json
{
  "LangVersion": "0.0.2",
  "Error": "An error occured.",
  "Invalid_syntax": "Invalid syntax.",
  "Invalid_item_shortname": "Invalid item shortname.",
  "Invalid_distribution_type_cannot_add_pending_players": "Cannot add to pending players, Invalid distribution type.",
  "Invalid_distribution_type_cannot_add_given_players": "Cannot add to given players, Invalid distribution type.",
  "Required_field_players_steam_id": "Players steamID64 field is required.",
  "Entry_invalid_id": "Invalid entry ID.",
  "Entry_new_item": "Created Item Entry (ID: {entryID} - ShortName: {shortName}, Amount: {amount} - DistributionType: {distributionType} - Players: {playersSteamIDs} - DeleteEntryOnMapWipe: {deleteEntryOnMapWipe})",
  "Entry_list": "All Entry IDs ({entryIDs}).",
  "Entry_view_item": "Entry Details (ID: {entryID} - ShortName: {shortName}, Amount: {amount} - DistributionType: {distributionType} - Players: {playersSteamIDs} - DeleteEntryOnMapWipe: {deleteEntryOnMapWipe})",
  "Entry_delete": "Deleted Entry {entryID}.",
  "Entry_add_pending_players": "(Entry {entryID}): Added to Pending Players (\"{pendingPlayers}\").",
  "Entry_remove_pending_player": "(Entry {entryID}): Removed Pending Player {pendingPlayer}.",
  "Entry_add_given_players": "(Entry {entryID}): Added to Given Players (\"{givenPlayers}\").",
  "Entry_remove_given_player": "(Entry {entryID}): Removed Given Player {givenPlayer}.",
  "Player_give_gift_failed_inventory_full": "Failed to receive gift. Not enough space in your inventory, please clear your inventory and then type <color=##ADD8E6>/sclaim</color> to manually claim it.",
  "Player_give_gifts_failed_inventory_full": "Failed to receive gifts. Not enough space in your inventory, please clear your inventory and then type <color=##ADD8E6>/sclaim</color> to manually claim them.",
  "Player_give_item_success": "You have received a gift of {itemAmount} {itemName} in your inventory.",
  "Player_claim_item_success": "You have claimed a gift of {itemAmount} {itemName} in your inventory.",
  "Player_claim_no_gifts": "No gifts to claim.",
  "Distributor_give_player_item": "(Entry {entryID}): Gave {itemAmount} {itemName} to player {playerName} ({playerID}).",
  "Distributor_failed_give_player_item": "(Entry {entryID}): Failed to give {itemAmount} {itemName} to player {playerName} ({playerID})."
}
```

## Future Development
- Better text formatting.
- Distribute to oxide groups.
- Expiring entry.
- Support Economics.
- Support Server Rewards.
- Simple GUIs.

## Premium
The premium version includes an additional feature called **custom gifts** which allows you to create multiple custom items and custom commands in a single entry.

Custom items allows you to set item skin id and item display name and custom commands allows you to use player data variables.

You can message me directly for more information.
