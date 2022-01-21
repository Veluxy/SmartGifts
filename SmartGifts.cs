using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SmartGifts", "TechnoMaster", "0.0.2")]
    [Description("Distribute gifts to any player when they awake from sleep or when manually claimed.")]
    internal class SmartGifts : RustPlugin
    {
        [PluginReference]
        private Plugin DiscordMessages;

        #region Fields

        private List<ulong> onlinePlayersCache = new List<ulong>();
        private bool mapWiped = false;
        private string uidPrefix;

        private static string DEFAULT_ADMIN_PERM = "smartgifts.admin";
        private static string DEFAULT_DISCORD_WEBHOOK = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private static string DEFAULT_PREFIX = "SmartGifts";
        private static string DEFAULT_PREFIX_COLOR = "yellow";
        private static string DEFAULT_CLAIM_COMMAND = "sclaim";

        #endregion Fields

        #region Hooks

        private void Init()
        {
            if (!string.IsNullOrEmpty(_configData.cPermissions.admin) && !permission.PermissionExists(_configData.cPermissions.admin)) permission.RegisterPermission(_configData.cPermissions.admin, this);

            uidPrefix = UnityEngine.Random.Range(1, 99).ToString();

            LoadData();

            if (mapWiped)
            {
                DeleteMapWipeEntries();
                SaveData();
                RefreshPlayersCache();
            }

            if (_configData.cDiscordMessages.enabled && (string.IsNullOrEmpty(_configData.cDiscordMessages.webhookURL) || _configData.cDiscordMessages.webhookURL == DEFAULT_DISCORD_WEBHOOK))
                PrintWarning("Discord Webhook URL not specified. Please set webhook url in plugin config.");

            cmd.AddChatCommand(_configData.claimCommand, this, "ClaimGift");
        }

        private void OnNewSave(string filename) => mapWiped = true;

        private void OnPlayerConnected(BasePlayer player) => AddPlayerToCache(player.userID);

        private void OnPlayerDisconnected(BasePlayer player, string reason) => RemovePlayerFromCache(player.userID);

        private void OnPlayerSleepEnded(BasePlayer player) => DistributeGifts(player);

        private void Loaded() => AddOnlinePlayersToCache();

        #endregion Hooks

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Prefix")]
            public string prefix = DEFAULT_PREFIX;

            [JsonProperty(PropertyName = "PrefixColor")]
            public string prefixColor = DEFAULT_PREFIX_COLOR;

            [JsonProperty(PropertyName = "GiftClaimCommand")]
            public string claimCommand = DEFAULT_CLAIM_COMMAND;

            [JsonProperty(PropertyName = "Permissions")]
            public ConfigPermissions cPermissions = new ConfigPermissions();

            [JsonProperty(PropertyName = "DiscordMessages")]
            public ConfigDiscordMessages cDiscordMessages = new ConfigDiscordMessages();

            [JsonProperty(PropertyName = "FileLogging")]
            public ConfigFileLogging cFileLogging = new ConfigFileLogging();
        }

        private class ConfigPermissions
        {
            [JsonProperty(PropertyName = "Admin")]
            public string admin = DEFAULT_ADMIN_PERM;
        }

        private class ConfigDiscordMessages
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool enabled = true;

            [JsonProperty(PropertyName = "WebhookURL")]
            public string webhookURL = DEFAULT_DISCORD_WEBHOOK;

            [JsonProperty(PropertyName = "LogCommand")]
            public bool logCommand = true;

            [JsonProperty(PropertyName = "LogPlayerGiveAttempt")]
            public bool logPlayerGiveAttempt = true;
        }

        private class ConfigFileLogging
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool enabled = true;

            [JsonProperty(PropertyName = "LogCommand")]
            public bool logCommand = true;

            [JsonProperty(PropertyName = "LogPlayerGiveAttempt")]
            public bool logPlayerGiveAttempt = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                    LoadDefaultConfig();
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Data

        private static string GIFT_ENTRIES_DATA_FILE = "SmartGifts_Entries";

        private GiftEntriesData giftEntriesData;

        private class GiftEntriesData
        {
            [JsonProperty(PropertyName = "Entries", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Entry> Entries = new List<Entry>();
        }

        private class Entry
        {
            [JsonProperty(PropertyName = "ID")]
            public string ID;

            [JsonProperty(PropertyName = "Gift", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Gift Gift;

            [JsonProperty(PropertyName = "Distribution Type (SpecificPlayers/MapPlayers/ServerPlayers/Everyone)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public DistributionTypes DistributionType;

            [JsonProperty(PropertyName = "Delete Entry On Map Wipe (true/false)")]
            public bool DeleteEntryOnMapWipe = false;

            [JsonProperty(PropertyName = "Pending Players")]
            public List<ulong> PendingPlayers = new List<ulong>();

            [JsonProperty(PropertyName = "Given Players")]
            public List<ulong> GivenPlayers = new List<ulong>();
        }

        private class Gift
        {
            [JsonProperty(PropertyName = "Item ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Item Amount")]
            public int Amount;
        }

        private enum DistributionTypes
        {
            SpecificPlayers,
            MapPlayers,
            ServerPlayers,
            Everyone,
        }

        private void LoadData()
        {
            try
            {
                giftEntriesData = Interface.Oxide.DataFileSystem.ReadObject<GiftEntriesData>(GIFT_ENTRIES_DATA_FILE);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (giftEntriesData == null) giftEntriesData = new GiftEntriesData();

            SaveData();
        }

        private void SaveData()
        {
            SaveGiftEntries();
        }

        private void SaveGiftEntries()
        {
            Interface.Oxide.DataFileSystem.WriteObject(GIFT_ENTRIES_DATA_FILE, giftEntriesData);
        }

        #endregion Data

        #region Commands

        #region > player

        private void ClaimGift(BasePlayer player, string command, string[] args)
        {
            var hasGifts = DistributeGifts(player, true);
            if (!hasGifts) PrintPlayer(Translate("Player_claim_no_gifts"), player);
        }

        #endregion > player

        #region > entry

        [ConsoleCommand("sgift.entry.new_item")]
        private void cmdEntryNewItem(ConsoleSystem.Arg arg) => entryNewItem(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.new_item")]
        private void chatEntryNewItem(BasePlayer player, string command, string[] args) => entryNewItem(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.list")]
        private void cmdEntryList(ConsoleSystem.Arg arg) => entryList(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.list")]
        private void chatEntryList(BasePlayer player, string command, string[] args) => entryList(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.view")]
        private void cmdEntryView(ConsoleSystem.Arg arg) => entryView(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.view")]
        private void chatEntryView(BasePlayer player, string command, string[] args) => entryView(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.delete")]
        private void cmdEntryDelete(ConsoleSystem.Arg arg) => entryDelete(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.delete")]
        private void chatEntryDelete(BasePlayer player, string command, string[] args) => entryDelete(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.add_pending_players")]
        private void cmdEntryAddPendingPlayers(ConsoleSystem.Arg arg) => entryAddPendingPlayers(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.add_pending_players")]
        private void chatEntryAddPendingPlayers(BasePlayer player, string command, string[] args) => entryAddPendingPlayers(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.remove_pending_player")]
        private void cmdEntryRemovePendingPlayer(ConsoleSystem.Arg arg) => entryRemovePendingPlayer(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.remove_pending_player")]
        private void chatEntryRemovePendingPlayer(BasePlayer player, string command, string[] args) => entryRemovePendingPlayer(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.add_given_players")]
        private void cmdEntryAddGivenPlayers(ConsoleSystem.Arg arg) => entryAddGivenPlayers(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.add_given_players")]
        private void chatEntryAddGivenPlayers(BasePlayer player, string command, string[] args) => entryAddGivenPlayers(new CommandArg() { Args = args }, player, true);

        [ConsoleCommand("sgift.entry.remove_given_player")]
        private void cmdEntryRemoveGivenPlayer(ConsoleSystem.Arg arg) => entryRemoveGivenPlayer(ParseCSA(arg), arg.Player());

        [ChatCommand("sgift.entry.remove_given_player")]
        private void chatEntryRemoveGivenPlayer(BasePlayer player, string command, string[] args) => entryRemoveGivenPlayer(new CommandArg() { Args = args }, player, true);

        #endregion > entry

        #endregion Commands

        #region Entry

        private void entryNewItem(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 3)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string itemShortname, itemAmount = "1", distributionType = DistributionTypes.SpecificPlayers.ToString(), playerSteamIDs = "", deleteEntryOnMapWipeS = "false";
            itemShortname = arg.Args[0].ToString();
            itemAmount = arg.Args[1].ToString();
            distributionType = arg.Args[2].ToString();
            if (arg.Args.Length >= 4)
                playerSteamIDs = arg.Args[3].ToString();
            if (arg.Args.Length >= 5)
                deleteEntryOnMapWipeS = arg.Args[4].ToString();

            var isValidItem = ItemManager.itemList.Exists(d => d.shortname == itemShortname);
            if (!isValidItem)
            {
                PrintConsole(Translate("Invalid_item_shortname"), player, isChat);
                return;
            }

            DistributionTypes dt;
            Enum.TryParse(distributionType, out dt);
            Boolean deleteEntryOnMapWipeB = false;
            Boolean.TryParse(deleteEntryOnMapWipeS, out deleteEntryOnMapWipeB);

            List<ulong> uPlayers = new List<ulong>();
            string playersSteamIDsLang = "";

            switch (dt)
            {
                case DistributionTypes.SpecificPlayers:
                    {
                        if (string.IsNullOrEmpty(playerSteamIDs.Trim()))
                        {
                            PrintConsole(Translate("Required_field_players_steam_id"), player, isChat);
                            return;
                        }
                        var uPendingPlayers = playerSteamIDs.Trim().Split(',').Select(StringToUlong).ToList();
                        uPlayers = uPendingPlayers;
                        playersSteamIDsLang = playerSteamIDs.Trim();
                        break;
                    }
                case DistributionTypes.MapPlayers:
                    {
                        foreach (BasePlayer p in BasePlayer.allPlayerList)
                        {
                            uPlayers.Add(p.userID);
                        }
                        playersSteamIDsLang = "All Players In Map";
                        break;
                    }
                case DistributionTypes.ServerPlayers:
                    {
                        foreach (IPlayer p in covalence.Players.All)
                        {
                            uPlayers.Add(StringToUlong(p.Id));
                        }
                        playersSteamIDsLang = "All Players In Server";
                        break;
                    }
                case DistributionTypes.Everyone:
                    {
                        playersSteamIDsLang = "Everyone";
                        break;
                    }
            }

            var entry = new Entry
            {
                ID = NewUID(),
                Gift = new Gift
                {
                    ShortName = itemShortname,
                    Amount = StringToInt(itemAmount),
                },
                DistributionType = dt,
                DeleteEntryOnMapWipe = deleteEntryOnMapWipeB,
                PendingPlayers = uPlayers,
            };
            giftEntriesData.Entries.Add(entry);
            SaveData();

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entry.ID },
                {"shortName", entry.Gift.ShortName },
                {"amount", entry.Gift.Amount.ToString()},
                {"distributionType", dt.ToString()},
                {"playersSteamIDs", playersSteamIDsLang},
                {"deleteEntryOnMapWipe", deleteEntryOnMapWipeB.ToString()},
            };
            var message = Translate("Entry_new_item", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        private void entryList(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            List<string> entryIDs = new List<string>();
            giftEntriesData.Entries.ForEach(e => entryIDs.Add(e.ID));

            var dict = new Dictionary<string, string>
            {
                {"entryIDs", string.Join(", ", entryIDs.ToArray()) },
            };
            var message = Translate("Entry_list", dict);

            PrintConsole(message, player, isChat);
        }

        private void entryView(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 1)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID = arg.Args[0].ToString();

            var entryIndex = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndex == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            var entry = giftEntriesData.Entries[entryIndex];

            var dict = new Dictionary<string, string>
                {
                    {"entryID", entry.ID },
                    {"shortName", entry.Gift.ShortName },
                    {"amount", entry.Gift.Amount.ToString()},
                    {"distributionType", entry.DistributionType.ToString()},
                    {"deleteEntryOnMapWipe", entry.DeleteEntryOnMapWipe.ToString()},
                };
            var message = Translate("Entry_view_item", dict);

            PrintConsole(message, player, isChat);
        }

        private void entryDelete(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 1)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID = arg.Args[0].ToString();

            var entryIndex = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndex == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            try
            {
                giftEntriesData.Entries.RemoveAll(d => d.ID == entryID);
                SaveData();
            }
            catch (Exception e)
            {
                PrintConsole($"{Translate("Error")} \n{e}", player, isChat);
                return;
            }

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entryID} ,
            };
            var message = Translate("Entry_delete", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        private void entryAddPendingPlayers(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 2)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID, playerSteamIDs;
            entryID = arg.Args[0].ToString();
            playerSteamIDs = arg.Args[1].ToString();

            List<ulong> uPlayers = new List<ulong>();
            string playersSteamIDsLang = "";

            if (string.IsNullOrEmpty(playerSteamIDs.Trim()))
            {
                PrintConsole(Translate("Required_field_players_steam_id"), player, isChat);
                return;
            }
            uPlayers = playerSteamIDs.Trim().Split(',').Select(StringToUlong).ToList();
            playersSteamIDsLang = playerSteamIDs.Trim();

            var entryIndexID = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndexID == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            try
            {
                if (giftEntriesData.Entries[entryIndexID].DistributionType == DistributionTypes.Everyone)
                {
                    PrintConsole(Translate("Invalid_distribution_type_cannot_add_pending_players"), player, isChat);
                    return;
                }
                uPlayers.ForEach(d => giftEntriesData.Entries[entryIndexID].PendingPlayers.Add(d));
                SaveData();
            }
            catch (Exception e)
            {
                PrintConsole($"{Translate("Error")} \n{e}", player, isChat);
                return;
            }

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entryID} ,
                {"pendingPlayers", playersSteamIDsLang},
            };
            var message = Translate("Entry_add_pending_players", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        private void entryRemovePendingPlayer(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 2)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID, playerSteamID;
            entryID = arg.Args[0].ToString();
            playerSteamID = arg.Args[1].ToString();

            List<ulong> uPlayers = new List<ulong>();

            var entryIndexID = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndexID == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            try
            {
                giftEntriesData.Entries[entryIndexID].PendingPlayers.RemoveAll(d => d == StringToUlong(playerSteamID));
                SaveData();
            }
            catch (Exception e)
            {
                PrintConsole($"{Translate("Error")} \n{e}", player, isChat);
                return;
            }

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entryID} ,
                {"pendingPlayer", playerSteamID},
            };
            var message = Translate("Entry_remove_pending_player", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        private void entryAddGivenPlayers(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 2)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID, playerSteamIDs;
            entryID = arg.Args[0].ToString();
            playerSteamIDs = arg.Args[1].ToString();

            List<ulong> uPlayers = new List<ulong>();
            string playersSteamIDsLang = "";

            if (string.IsNullOrEmpty(playerSteamIDs.Trim()))
            {
                PrintConsole(Translate("Required_field_players_steam_id"), player, isChat);
                return;
            }
            uPlayers = playerSteamIDs.Trim().Split(',').Select(StringToUlong).ToList();
            playersSteamIDsLang = playerSteamIDs.Trim();

            var entryIndexID = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndexID == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            try
            {
                if (giftEntriesData.Entries[entryIndexID].DistributionType != DistributionTypes.Everyone)
                {
                    PrintConsole(Translate("Invalid_distribution_type_cannot_add_given_players"), player, isChat);
                    return;
                }
                uPlayers.ForEach(d => giftEntriesData.Entries[entryIndexID].GivenPlayers.Add(d));
                SaveData();
            }
            catch (Exception e)
            {
                PrintConsole($"{Translate("Error")} \n{e}", player, isChat);
                return;
            }

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entryID} ,
                {"givenPlayers", playersSteamIDsLang},
            };
            var message = Translate("Entry_add_given_players", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        private void entryRemoveGivenPlayer(CommandArg arg, BasePlayer player = null, bool isChat = false)
        {
            if (player && !HasAccessAdmin(player)) return;

            if (!arg.HasArgs() || arg.Args.Length < 2)
            {
                PrintConsole(Translate("Invalid_syntax"), player, isChat);
                return;
            }

            string entryID, playerSteamID;
            entryID = arg.Args[0].ToString();
            playerSteamID = arg.Args[1].ToString();

            List<ulong> uPlayers = new List<ulong>();

            var entryIndexID = giftEntriesData.Entries.FindIndex(d => d.ID == entryID);
            if (entryIndexID == -1)
            {
                PrintConsole(Translate("Entry_invalid_id"), player, isChat);
                return;
            }

            try
            {
                giftEntriesData.Entries[entryIndexID].GivenPlayers.RemoveAll(d => d == StringToUlong(playerSteamID));
                SaveData();
            }
            catch (Exception e)
            {
                PrintConsole($"{Translate("Error")} \n{e}", player, isChat);
                return;
            }

            RefreshPlayersCache();

            var dict = new Dictionary<string, string>
            {
                {"entryID", entryID} ,
                {"givenPlayer", playerSteamID},
            };
            var message = Translate("Entry_remove_given_player", dict);
            PrintConsoleInfo(message, player, isChat);
            if (_configData.cFileLogging.logCommand)
                FileLog(message, player);
            if (_configData.cDiscordMessages.logCommand)
                DiscordMessage(message, player);
        }

        #endregion Entry

        #region Distributor

        private enum GivePlayerItemStatus
        {
            InvalidShortName,
            CreateItemFailed,
            InventorySlotFull,
            FailedToGiveItem,
            Success,
        }

        private bool DistributeGifts(BasePlayer player, bool isClaim = false)
        {
            var cacheExists = onlinePlayersCache.Exists(d => d == player.userID);
            if (cacheExists)
            {
                giftEntriesData.Entries.ForEach(entry =>
                {
                    switch (entry.DistributionType)
                    {
                        case DistributionTypes.Everyone:
                            {
                                var hasGiven = entry.GivenPlayers.Exists(p => p == player.userID);
                                if (hasGiven) return;
                                break;
                            }
                        case DistributionTypes.MapPlayers:
                        case DistributionTypes.ServerPlayers:
                        case DistributionTypes.SpecificPlayers:
                            {
                                var isPending = entry.PendingPlayers.Exists(p => p == player.userID);
                                if (!isPending) return;
                                break;
                            }
                    }
                    DistributePlayerEntry(player, entry, isClaim);
                });
                return true;
            }
            return false;
        }

        private void DistributePlayerEntry(BasePlayer player, Entry entry, bool isClaim)
        {
            string message = "";
            int totalItems = 0;
            if (!String.IsNullOrEmpty(entry.Gift.ShortName))
                totalItems = 1;

            if (GetPlayerFreeSlots(player) < (totalItems + 1))
            {
                message = Translate("Player_give_gift_failed_inventory_full");
                if (totalItems > 1) message = Translate("Player_give_gifts_failed_inventory_full");
                PrintPlayer(message, player);
                return;
            }

            string playerMessage = "";
            var dict = new Dictionary<string, string>
            {
                {"itemAmount", entry.Gift.Amount.ToString() },
                {"itemName", GetItemDisplayName(entry.Gift.ShortName) },
                {"entryID", entry.ID },
                {"playerID", player.userID.ToString() },
                {"playerName", player.displayName },
            };
            var status = GivePlayerItem(player, entry.Gift.ShortName, entry.Gift.Amount);
            switch (status)
            {
                case GivePlayerItemStatus.CreateItemFailed:
                case GivePlayerItemStatus.FailedToGiveItem:
                case GivePlayerItemStatus.InvalidShortName:
                    {
                        message = Translate("Distributor_failed_give_player_item", dict);
                        break;
                    }

                case GivePlayerItemStatus.InventorySlotFull:
                    {
                        playerMessage = Translate("Player_give_gift_failed_inventory_full");
                        message = Translate("Distributor_failed_give_player_item", dict);
                        break;
                    }

                case GivePlayerItemStatus.Success:
                    {
                        playerMessage = Translate("Player_give_item_success", dict);
                        if (isClaim) playerMessage = Translate("Player_claim_item_success", dict);
                        message = Translate("Distributor_give_player_item", dict);
                        break;
                    }
            }

            if (!string.IsNullOrEmpty(playerMessage)) PrintPlayer(playerMessage, player);
            PrintConsoleInfo(message);
            if (_configData.cFileLogging.logPlayerGiveAttempt)
                FileLog(message);
            if (_configData.cDiscordMessages.logPlayerGiveAttempt)
                DiscordMessage(message);

            if (status == GivePlayerItemStatus.Success)
            {
                RemovePlayerFromCache(player.userID);
                if (entry.DistributionType == DistributionTypes.Everyone)
                {
                    AddPlayerToGiven(player.userID, entry.ID);
                }
                else
                {
                    RemovePlayerFromPending(player.userID, entry.ID);
                }
                SaveData();
            }

            return;
        }

        private GivePlayerItemStatus GivePlayerItem(BasePlayer player, string shortName, int amount, string displayName = null, ulong skinID = 0)
        {
            var iDef = ItemManager.FindItemDefinition(shortName);
            if (iDef == null)
            {
                return GivePlayerItemStatus.InvalidShortName;
            }

            var newItem = ItemManager.Create(iDef, amount, skinID);
            if (newItem == null)
            {
                return GivePlayerItemStatus.CreateItemFailed;
            }

            if (!string.IsNullOrEmpty(displayName)) newItem.name = displayName;

            if (GetPlayerFreeSlots(player) < 1)
            {
                return GivePlayerItemStatus.InventorySlotFull;
            }

            try
            {
                player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
            }
            catch (Exception ex)
            {
                PrintWarning(ex.ToString());
                return GivePlayerItemStatus.FailedToGiveItem;
            }

            return GivePlayerItemStatus.Success;
        }

        private void AddOnlinePlayersToCache()
        {
            foreach (BasePlayer p in BasePlayer.allPlayerList)
            {
                AddPlayerToCache(p.userID);
            }
        }

        private void AddPlayerToCache(ulong playerID)
        {
            giftEntriesData.Entries.ForEach(d =>
            {
                switch (d.DistributionType)
                {
                    case DistributionTypes.SpecificPlayers:
                    case DistributionTypes.MapPlayers:
                    case DistributionTypes.ServerPlayers:
                        {
                            var pendingExists = d.PendingPlayers.Exists(pp => pp == playerID);
                            var cacheExists = onlinePlayersCache.Exists(pc => pc == playerID);
                            if (pendingExists && !cacheExists) onlinePlayersCache.Add(playerID);
                            break;
                        }
                    case DistributionTypes.Everyone:
                        {
                            var givenExists = d.GivenPlayers.Exists(gp => gp == playerID);
                            var cacheExists = onlinePlayersCache.Exists(pc => pc == playerID);
                            if (!givenExists && !cacheExists) onlinePlayersCache.Add(playerID);
                            break;
                        }
                }
            });
        }

        private void RemovePlayerFromCache(ulong playerID)
        {
            onlinePlayersCache.RemoveAll(d => d == playerID);
        }

        private void RefreshPlayersCache()
        {
            onlinePlayersCache = new List<ulong>();
            AddOnlinePlayersToCache();
        }

        private void RemovePlayerFromPending(ulong playerID, string entryID)
        {
            var entryIndex = giftEntriesData.Entries.FindIndex(e => e.ID == entryID);
            if (entryIndex == -1) return;
            giftEntriesData.Entries[entryIndex].PendingPlayers.RemoveAll(p => p == playerID);
        }

        private void AddPlayerToGiven(ulong playerID, string entryID)
        {
            var entryIndex = giftEntriesData.Entries.FindIndex(e => e.ID == entryID);
            if (entryIndex == -1) return;
            giftEntriesData.Entries[entryIndex].GivenPlayers.Add(playerID);
        }

        private void DeleteMapWipeEntries()
        {
            giftEntriesData.Entries.ForEach(e =>
            {
                if (e.DeleteEntryOnMapWipe)
                    giftEntriesData.Entries.RemoveAll(re => re.ID == e.ID);
            });
        }

        #endregion Distributor

        #region Logging

        private void FileLog(string message, BasePlayer player = null)
        {
            if (!_configData.cFileLogging.enabled)
                return;

            var dateTime = DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK");
            var msg = $"[{dateTime}] {message}";
            if (player)
            {
                msg = $"[{dateTime}|{player.userID}] {message}";
            }
            LogToFile("", msg, this);
        }

        private void PrintConsoleInfo(string message, BasePlayer player = null, bool isChat = false)
        {
            var dateTime = DateTime.Now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK");
            if (player)
            {
                if (isChat) SendReply(player, $"[{PrefixAndColor()}] {message}");
                Puts($"[{dateTime}|{player.userID}] {message}");
                PrintToConsole(player, $"[{dateTime}] {message}");
                return;
            }
            Puts($"[{dateTime}] {message}");
        }

        private void PrintConsole(string message, BasePlayer player = null, bool isChat = false)
        {
            if (player)
            {
                if (isChat) SendReply(player, $"[{PrefixAndColor()}] {message}");
                PrintToConsole(message);
                return;
            }
            Puts(message);
        }

        private void PrintPlayer(string message, BasePlayer player)
        {
            SendReply(player, $"[{PrefixAndColor()}] {message}");
        }

        #endregion Logging

        #region Discord

        private void DiscordMessage(string message, BasePlayer player = null)
        {
            if (!_configData.cDiscordMessages.enabled || DiscordMessages == null || !DiscordMessages.IsLoaded || string.IsNullOrEmpty(_configData.cDiscordMessages.webhookURL) || _configData.cDiscordMessages.webhookURL == DEFAULT_DISCORD_WEBHOOK)
            {
                return;
            }

            var msg = $":gift: **[{_configData.prefix}]** {message}";
            if (player)
            {
                msg = $":gift: **[{_configData.prefix}|**{player.displayName}**]** {message}";
            }
            Interface.CallHook("API_SendTextMessage", _configData.cDiscordMessages.webhookURL, msg, false, this);
        }

        #endregion Discord

        #region Language

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "LangVersion", this.Version.ToString() },
                { "Error", "An error occured." },
                { "Invalid_syntax", "Invalid syntax." },
                { "Invalid_item_shortname", "Invalid item shortname." },
                { "Invalid_distribution_type_cannot_add_pending_players", "Cannot add to pending players, Invalid distribution type." },
                { "Invalid_distribution_type_cannot_add_given_players", "Cannot add to given players, Invalid distribution type." },
                { "Required_field_players_steam_id", "Players steamID64 field is required." },
                { "Entry_invalid_id", "Invalid entry ID." },
                { "Entry_new_item", "Created Item Entry (ID: {entryID} - ShortName: {shortName}, Amount: {amount} - DistributionType: {distributionType} - Players: {playersSteamIDs} - DeleteEntryOnMapWipe: {deleteEntryOnMapWipe})" },
                { "Entry_list", "All Entry IDs ({entryIDs})." },
                { "Entry_view_item", "Entry Details (ID: {entryID} - ShortName: {shortName}, Amount: {amount} - DistributionType: {distributionType} - Players: {playersSteamIDs} - DeleteEntryOnMapWipe: {deleteEntryOnMapWipe})" },
                { "Entry_delete", "Deleted Entry {entryID}." },
                { "Entry_add_pending_players", "(Entry {entryID}): Added to Pending Players (\"{pendingPlayers}\")." },
                { "Entry_remove_pending_player", "(Entry {entryID}): Removed Pending Player {pendingPlayer}." },
                { "Entry_add_given_players", "(Entry {entryID}): Added to Given Players (\"{givenPlayers}\")." },
                { "Entry_remove_given_player", "(Entry {entryID}): Removed Given Player {givenPlayer}." },
                { "Player_give_gift_failed_inventory_full", $"Failed to receive gift. Not enough space in your inventory, please clear your inventory and then type <color=#ADD8E6>/{_configData.claimCommand}</color> to manually claim it." },
                { "Player_give_gifts_failed_inventory_full", $"Failed to receive gifts. Not enough space in your inventory, please clear your inventory and then type <color=#ADD8E6>/{_configData.claimCommand}</color> to manually claim them." },
                { "Player_give_item_success", "You have received a gift of {itemAmount} {itemName} in your inventory." },
                { "Player_claim_item_success", "You have claimed a gift of {itemAmount} {itemName} in your inventory." },
                { "Player_claim_no_gifts", "No gifts to claim." },
                { "Distributor_give_player_item", "(Entry {entryID}): Gave {itemAmount} {itemName} to player {playerName} ({playerID})." },
                { "Distributor_failed_give_player_item", "(Entry {entryID}): Failed to give {itemAmount} {itemName} to player {playerName} ({playerID})." },
            }, this, "en");
        }

        private string Translate(string msg, Dictionary<string, string> parameters = null)
        {
            if (string.IsNullOrEmpty(msg))
                return string.Empty;

            msg = lang.GetMessage(msg, this);
            if (parameters != null)
            {
                foreach (var dict in parameters)
                {
                    if (msg.Contains("{" + dict.Key + "}"))
                        msg = msg.Replace("{" + dict.Key + "}", dict.Value);
                }
            }
            return msg;
        }

        #endregion Language

        #region Utils

        private string NewGUID()
        {
            return Guid.NewGuid().ToString();
        }

        private string NewUID()
        {
            return uidPrefix + UnityEngine.Random.Range(1, 999999).ToString();
        }

        private bool HasAccessAdmin(BasePlayer player)
        {
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, _configData.cPermissions.admin);
        }

        private int StringToInt(string text)
        {
            try
            {
                return (int)int.Parse(text);
            }
            catch
            {
                return (int)0;
            }
        }

        private ulong StringToUlong(string text)
        {
            try
            {
                return (ulong)Convert.ToUInt64(text);
            }
            catch
            {
                return (ulong)0;
            }
        }

        private class CommandArg
        {
            public string[] Args = { };

            public bool HasArgs()
            {
                if (Args.Length > 0) return true;
                return false;
            }
        }

        private CommandArg ParseCSA(ConsoleSystem.Arg arg)
        {
            string[] args = { };
            if (arg.HasArgs()) args = arg.Args;
            return new CommandArg() { Args = args };
        }

        private int GetPlayerFreeSlots(BasePlayer player)
        {
            return (player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) +
                   (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count);
        }

        private string PrefixAndColor()
        {
            return $"<color={_configData.prefixColor}>{_configData.prefix}</color>";
        }

        private string GetItemDisplayName(string shortName)
        {
            var iDef = ItemManager.FindItemDefinition(shortName);
            if (iDef == null) return shortName;
            return iDef.displayName.english;
        }

        #endregion Utils
    }
}