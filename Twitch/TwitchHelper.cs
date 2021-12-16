using NHSE.Core;
using NHSE.Villagers;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SysBot.ACNHOrders.Twitch
{
    public static class TwitchHelper
    {
        // Helper functions for commands
        public static bool AddToWaitingList(string orderString, string display, string username, ulong id, bool sub, bool cat, out string msg)
        {
            if (!IsQueueable(orderString, id, out var msge))
            {
                msg = $"@{username} - {msge}";
                return false;
            }

            try
            {
                var cfg = Globals.Bot.Config;
                VillagerRequest? vr = null;

                // try get villager
                var result = VillagerOrderParser.ExtractVillagerName(orderString, out var res, out var san);
                if (result == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
                {
                    msg = $"@{username} - {res} Pedido no aceptado.";
                    return false;
                }

                if (result == VillagerOrderParser.VillagerRequestResult.Success)
                {
                    if (!cfg.AllowVillagerInjection)
                    {
                        msg = $"@{username} - La inyeción de aldeanos está deshabilitada.";
                        return false;
                    }

                    orderString = san;
                    var replace = VillagerResources.GetVillager(res);
                    vr = new VillagerRequest(username, replace, 0, GameInfo.Strings.GetVillager(res));
                }

                var items = string.IsNullOrWhiteSpace(orderString) ? new Item[1] { new Item(Item.NONE) } : ItemParser.GetItemsFromUserInput(orderString, cfg.DropConfig, ItemDestination.FieldItemDropped);

                return InsertToQueue(items, vr, display, username, id, sub, cat, out msg);
            }
            catch (Exception e) 
            { 
                LogUtil.LogError($"{username}@{orderString}: {e.Message}", nameof(TwitchHelper)); 
                msg = $"@{username} {e.Message}";
                return false;
            }
        }

        public static bool AddToWaitingListPreset(string presetName, string display, string username, ulong id, bool sub, out string msg)
        {
            if (!IsQueueable(presetName, id, out var msge))
            {
                msg = $"@{username} - {msge}";
                return false;
            }

            try
            {
                var cfg = Globals.Bot.Config;
                VillagerRequest? vr = null;

                // try get villager
                var result = VillagerOrderParser.ExtractVillagerName(presetName, out var res, out var san);
                if (result == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
                {
                    msg = $"@{username} - {res} El pedido no ha sido aceptado.";
                    return false;
                }

                if (result == VillagerOrderParser.VillagerRequestResult.Success)
                {
                    if (!cfg.AllowVillagerInjection)
                    {
                        msg = $"@{username} - La inyección de aldeanos está deshabilitada.";
                        return false;
                    }

                    presetName = san;
                    var replace = VillagerResources.GetVillager(res);
                    vr = new VillagerRequest(username, replace, 0, GameInfo.Strings.GetVillager(res));
                }

                presetName = presetName.Trim();
                var preset = PresetLoader.GetPreset(cfg.OrderConfig, presetName);
                if (preset == null)
                {
                    msg = $"{username} - {presetName} no es un preset válido.";
                    return false;
                }

                return InsertToQueue(preset, vr, display, username, id, sub, true, out msg);
            }
            catch (Exception e)
            {
                LogUtil.LogError($"{username}@Preset:{presetName}: {e.Message}", nameof(TwitchHelper));
                msg = $"@{username} {e.Message}";
                return false;
            }
        }

        public static string ClearTrade(ulong userID)
        {
            QueueExtensions.GetPosition(userID, out var order);
            if (order == null)
                return "Lo siento, no estás en la cola o tu pedido se está llevando a cabo ahora.";

            order.SkipRequested = true;
            return "Tu pedido ha sido eliminado. Ten en cuenta que no podrás unirte a la cola durante un rato.";
        }

        public static string ClearTrade(string userID)
        {
            if (!ulong.TryParse(userID, out var usrID))
                return $"{userID} is not a valid u64.";

            return ClearTrade(userID);
        }

        public static string GetPosition(ulong userID)
        {
            var position = QueueExtensions.GetPosition(userID, out var order);
            if (order == null)
                return "Lo siento, no estás en la cola o tu pedido se está llevando a cabo ahora.";

            var message = $"Estás en la cola. Posición: {position}.";
            if (position > 1)
                message += $" Tiempo restante aproximado: {QueueExtensions.GetETA(position)}.";

            return message;
        }

        public static string GetPresets(char prefix)
        {
            var presets = PresetLoader.GetPresets(Globals.Bot.Config.OrderConfig);

            if (presets.Length < 1)
                return "No hay presets disponibles.";
            else
                return $"Los presets disponibles son los siguientes: {string.Join(", ", presets)}. Introduce {prefix}preset [nombre del preset] para pedir uno!";
        }

        public static string Clean(ulong id, string username, TwitchConfig tcfg)
        {
            if (!tcfg.AllowDropViaTwitchChat)
            {
                LogUtil.LogInfo($"{username} está intentando limpiar objetos, aunque la configuración de Twitch no permite comandos de drop.", nameof(TwitchCrossBot));
                return string.Empty;
            }

            if (!GetDropAvailability(id, username, tcfg, out var error))
                return error;

            if (!Globals.Bot.Config.AllowClean)
                return "La función de limpieza está actualmente desactivada.";
            
            Globals.Bot.CleanRequested = true;
            return "Una petición de limpieza será ejecutada en unos momentos.";
        }

        public static string Drop(string message, ulong id, string username, TwitchConfig tcfg)
        {
            if (!tcfg.AllowDropViaTwitchChat)
            {
                LogUtil.LogInfo($"{username} está intentando dropear objetos, aunque la configuración de Twitch no permite comandos de drop.", nameof(TwitchCrossBot));
                return string.Empty;
            }
            if (!GetDropAvailability(id, username, tcfg, out var error))
                return error;

            var cfg = Globals.Bot.Config;
            var items = ItemParser.GetItemsFromUserInput(message, cfg.DropConfig, cfg.DropConfig.UseLegacyDrop ? ItemDestination.PlayerDropped : ItemDestination.HeldItem);
            MultiItem.StackToMax(items);

            if (!InternalItemTool.CurrentInstance.IsSane(items, cfg.DropConfig))
                return $"Estás intentando dropear objetos que pueden dañar tu partida. Petición denegada.";

            var MaxRequestCount = cfg.DropConfig.MaxDropCount;
            var ret = string.Empty;
            if (items.Count > MaxRequestCount)
            {
                ret += $"Los usuarios están limitados a {MaxRequestCount} objetos por comando. Por favor, usa este bot responsablemente. ";
                items = items.Take(MaxRequestCount).ToArray();
            }

            var requestInfo = new ItemRequest(username, items);
            Globals.Bot.Injections.Enqueue(requestInfo);

            ret += $"La petición de drop del objeto {(requestInfo.Item.Count > 1 ? "s" : string.Empty)} será ejecutada momentáneamente.";
            return ret;
        }

        private static bool IsQueueable(string orderString, ulong id, out string msg)
        {
            if (!TwitchCrossBot.Bot.Config.AcceptingCommands || TwitchCrossBot.Bot.Config.SkipConsoleBotCreation)
            {
                msg = "Lo siento, no estoy aceptando peticiones de drop en este momento.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(orderString))
            {
                msg = "No valid order text.";
                return false;
            }

            if (GlobalBan.IsBanned(id.ToString()))
            {
                msg = "Has sido baneado por abusar. Tu pedido se ha denegado.";
                return false;
            }

            msg = string.Empty;
            return true;
        }

        private static bool InsertToQueue(IReadOnlyCollection<Item> items, VillagerRequest? vr, string display, string username, ulong id, bool sub, bool cat, out string msg)
        {
            if (!InternalItemTool.CurrentInstance.IsSane(items, Globals.Bot.Config.DropConfig))
            {
                msg = $"@{username} - Estás intentando dropear objetos que pueden dañar tu partida. Petición denegada.";
                return false;
            }

            var multiOrder = new MultiItem(items.ToArray(), cat, true, true);

            var tq = new TwitchQueue(multiOrder.ItemArray.Items, vr, display, id, sub);
            TwitchCrossBot.QueuePool.Add(tq);
            msg = $"@{username} - He anotado tu pedido, ahora envíame por mensaje privado cualquier número de 3 dígitos. Escribe /w @{TwitchCrossBot.BotName.ToLower()} [número de 3 dígitos] en este canal. ¡Tu pedido no se añadirá a la cola hasta que me lo envíes!";
            return true;
        }

        private static bool GetDropAvailability(ulong callerId, string callerName, TwitchConfig tcfg, out string error)
        {
            error = string.Empty;
            var cfg = Globals.Bot.Config;

            if (tcfg.IsSudo(callerName))
                return true;

            if (Globals.Bot.CurrentUserId == callerId)
                return true;

            if (!cfg.AllowDrop)
            {
                error = $"AllowDrop está en false en la configuración.";
                return false;
            }
            else if (!cfg.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                error = $"Sólo puedes ejecutar este comando mientras estés en la isla durante tu pedido, y sólo si te has olvidado de algo.";
                return false;
            }

            return true;
        }
    }
}
