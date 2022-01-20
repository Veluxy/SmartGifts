# Features
Distribute gifts to any player when they awake from sleep or claim them manually.
- Gift an item to specific players regardless of status (online/offline/alive/dead/sleeping).
- Gift an item to all players.
- Automatically distribute the gift item when a player awakes from sleep.
- Manually claim gift items using a chat command.
- Delete a gift item entry on map wipe.
- Detailed logging to console, txt file and discord.

# Permissions
 - `smartgifts.admin` -- Allows player to use the admin commands

# Player Commands
Works for chat only
- `/sclaim` -- Manually claim any outstanding gift(s).

# Admin Commands
Works for both chat and console
- `sgift.entry.new_item "<item shortname>" "<item amount>" "<distribution type>" "<players steamID64 (separated with commas|optional)>" "<delete on map wipe? (true/false|optional)>"` -- Creates a new entry.
- `sgift.entry.list"` -- List all entries id.
- `sgift.entry.view "<entry id>"` -- View details about an entry.
- `sgift.entry.delete "<entry id>"` -- Deletes an entry.
- `sgift.entry.add_pending_players "<entry id>" "<players steamID64 (separated with commas)>"` -- Add pending players to an entry.
- `sgift.entry.remove_pending_player "<entry id>" "<player steamID64 (separated with commas)>"` -- Remove pending player from an entry.
- `sgift.entry.add_given_players "<entry id>" "<players steamID64 (separated with commas)>"` -- Add given players to an entry.
- `sgift.entry.remove_given_player "<entry id>" "<player steamID64 (separated with commas)>"` -- Remove given player from an entry.

## Example
 - `sgift.entry.new_item "wood" "100" "SpecificPlayers" "765111xxxxxxxxxxx,765111xxxxxxxxxxx,765111xxxxxxxxxxx" "false"`
 - `sgift.entry.new_item "scrap" "200" "MapPlayers" "" "true"`
 - `sgift.entry.new_item "wood" "100" "Everyone"`

## Gift Distribution Types
 1. **SpecificPlayers**
    - Adds the given input players to the pending player list.
    - Distributes to players from the pending players list and then remove them from the list.
 2. **MapPlayers**
    - Adds all players that exists on the map (alive or sleeping) using `BasePlayer.allPlayerList` to the pending player list.
    - Distributes to players from the pending players list and then remove them from the list.
 3. **ServerPlayers**
    - Adds all players that exists have joined the server before using `covalence.Players.All` to the pending player list.
 4. **Everyone**
    - Distributes to any player that joins the server and then add them to the given players list.

## Item Shortname
You can find a list of items shortname by searching for `rust item shortname list` or from here https://github.com/OrangeWulf/Rust-Docs/blob/master/Items.md

# Configuration
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

# Data File Example
```
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
        765111xxxxxxxxxxx
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
        765111xxxxxxxxxxx
      ]
    }
  ]
}
```

# Log Location
The log files are located in `oxide/logs/SmartGifts/smartgifts_-YYYY-MM-DD.txt`.

# Localization

# Future Development
- Better text formatting.
- Distribute to oxide groups.
- Expiring entry.
- Support Economics.
- Support Server Rewards.
- Simple GUIs.

# Premium
The premium version includes an additional feature called **custom gifts** which allows you to create multiple custom items and custom commands in a single entry.

Custom items allows you to set item skin id and item display name and custom commands allows you to use player data variables.

You can message me directly for more information.