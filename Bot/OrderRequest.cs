using Discord;
using Discord.WebSocket;
using NHSE.Core;
using System;
using System.Linq;

namespace SysBot.ACNHOrders
{
    public class OrderRequest<T> : IACNHOrderNotifier<T> where T : Item, new()
    {
        public MultiItem ItemOrderData { get; }
        public ulong UserGuid { get; }
        public ulong OrderID { get; }
        public string VillagerName { get; }
        private SocketUser Trader { get; }
        private ISocketMessageChannel CommandSentChannel { get; }
        public Action<CrossBot>? OnFinish { private get; set; }
        public T[] Order { get; } // stupid but I cba to work on this part anymore
        public VillagerRequest? VillagerOrder { get; }
        public bool SkipRequested { get; set; } = false;

        public OrderRequest(MultiItem data, T[] order, ulong user, ulong orderId, SocketUser trader, ISocketMessageChannel commandSentChannel, VillagerRequest? vil)
        {
            ItemOrderData = data;
            UserGuid = user;
            OrderID = orderId;
            Trader = trader;
            CommandSentChannel = commandSentChannel;
            Order = order;
            VillagerName = trader.Username;
            VillagerOrder = vil;
        }

        public void OrderCancelled(CrossBot routine, string msg, bool faulted)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"Ups! Algo ha pasado con tu pedido: {msg}");
            if (!faulted)
                CommandSentChannel.SendMessageAsync($"{Trader.Mention} - Tu pedido ha sido cancelado: {msg}");
        }

        public void OrderInitializing(CrossBot routine, string msg)
        {
            Trader.SendMessageAsync($"Tu pedido se ha iniciado, por favor **asegúrate de que tu inventario está __vacío__**, una vez hecho esto, ve a hablar con Rafa, y quédate esperando en la pantalla del código dodo. Te enviaré el código dodo en un momento. {msg}");
        }

        public void OrderReady(CrossBot routine, string msg, string dodo)
        {
            Trader.SendMessageAsync($"¡Estoy esperándote {Trader.Username}! {msg}. El código dodo es **{dodo}**");
        }

        public void OrderFinished(CrossBot routine, string msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"Tu pedido se ha completado, ¡muchas gracias! {msg}");
        }

        public void SendNotification(CrossBot routine, string msg)
        {
            Trader.SendMessageAsync(msg);
        }
    }
}
