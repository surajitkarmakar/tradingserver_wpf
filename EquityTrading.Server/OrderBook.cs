using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EquityTrading.Server
{
    public class OrderBook : Hub<IOrder>
    {
        private static ConcurrentDictionary<string, User> _tradeUsers = new ConcurrentDictionary<string, User>();
        private static ConcurrentDictionary<OrderType, ConcurrentDictionary<string, HashSet<Order>>> _tradeOrders = new ConcurrentDictionary<OrderType, ConcurrentDictionary<string, HashSet<Order>>>();
        public override Task OnDisconnected(bool stopCalled)
        {
            var userName = _tradeUsers.SingleOrDefault((c) => c.Value.ID == Context.ConnectionId).Key;
            if (userName != null)
            {
                Clients.Others.ParticipantDisconnection(userName);
                Console.WriteLine($"<> {userName} disconnected");
            }
            return base.OnDisconnected(stopCalled);
        }
        public override Task OnReconnected()
        {
            var userName = _tradeUsers.SingleOrDefault((c) => c.Value.ID == Context.ConnectionId).Key;
            if (userName != null)
            {
                Clients.Others.ParticipantReconnection(userName);
                Console.WriteLine($"== {userName} reconnected");
            }
            return base.OnReconnected();
        }
        public List<User> Login(string name)
        {
            if (!_tradeUsers.ContainsKey(name))
            {
                Console.WriteLine($"++ {name} logged in");
                List<User> users = new List<User>(_tradeUsers.Values);
                User newUser = new User { Name = name, ID = Context.ConnectionId };
                var added = _tradeUsers.TryAdd(name, newUser);
                if (!added) return null;
                Clients.CallerState.UserName = name;
                Clients.Others.ParticipantLogin(newUser);
                return users;
            }
            return null;
        }
        public void Logout()
        {
            var name = Clients.CallerState.UserName;
            if (!string.IsNullOrEmpty(name))
            {
                User client = new User();
                _tradeUsers.TryRemove(name, out client);
                Clients.Others.ParticipantLogout(name);
                Console.WriteLine($"-- {name} logged out");
            }
        }
        public void GenerateMarketDepth(string symbol)
        {
            if (_tradeOrders.ContainsKey(OrderType.Buy) && _tradeOrders[OrderType.Buy].ContainsKey(symbol))
            {
                var bidData = _tradeOrders[OrderType.Buy][symbol]
                    .Where(x => x.Status != OrderStatus.Executed && x.Status != OrderStatus.Cancelled)
                    .GroupBy(x => x.Price)
                    .Select(g => new
                    {
                        Price = g.Key,
                        Quantity = g.Sum(n => n.BalQty)
                    }).OrderBy(m => m.Price).TakeLast(5);
                var bidJson =JsonConvert.SerializeObject(bidData);
                foreach (var tradeUser in _tradeUsers)
                {
                    Clients.Client(tradeUser.Value.ID).SendBidDepth(bidJson);
                }
            }
            if (_tradeOrders.ContainsKey(OrderType.Sell) && _tradeOrders[OrderType.Sell].ContainsKey(symbol))
            {
                var askData = _tradeOrders[OrderType.Sell][symbol]
                    .Where(x => x.Status != OrderStatus.Executed && x.Status != OrderStatus.Cancelled)
                    .GroupBy(x => x.Price)
                    .Select(g => new
                    {
                        Price = g.Key,
                        Quantity = g.Sum(n => n.BalQty)
                    }).OrderBy(m => m.Price).Take(5);
                var askJson= JsonConvert.SerializeObject(askData);
                foreach (var tradeUser in _tradeUsers)
                {
                    Clients.Client(tradeUser.Value.ID).SendAskDepth(askJson);
                }
            }
        }
        public ConcurrentDictionary<OrderType, ConcurrentDictionary<string, HashSet<Order>>> ProcessOrder(Order order)
        {
            Monitor.Enter(_tradeOrders);
            try
            {
                _tradeUsers.TryGetValue(order.UserName, out User user);
                order.UserId = user?.ID;
                order.BalQty = order.Quantity;
                order.Status = OrderStatus.Open;
                order.Placed_Time = DateTime.Now;
                if (!_tradeOrders.ContainsKey(order.Type))
                    _tradeOrders.TryAdd(order.Type,new ConcurrentDictionary<string, HashSet<Order>>());
                if (!_tradeOrders[order.Type].ContainsKey(order.Symbol))
                    _tradeOrders[order.Type].TryAdd(order.Symbol, new HashSet<Order>());

                _tradeOrders[order.Type][order.Symbol]?.Add(order);
                foreach (var tradeUser in _tradeUsers)
                {
                    IEnumerable<Order> bidTrade=null, askTrade=null;
                    try
                    {
                        bidTrade = _tradeOrders[OrderType.Buy][order.Symbol].Where(x => x.UserId == tradeUser.Value.ID);
                        askTrade = _tradeOrders[OrderType.Sell][order.Symbol]
                            .Where(x => x.UserId == tradeUser.Value.ID);
                    }
                    catch
                    {
                        // ignored
                    }
                    finally
                    {
                        var result = bidTrade != null ? askTrade != null ? bidTrade.Concat(askTrade) : bidTrade :
                            askTrade != null ? askTrade : null;

                        if (result != null)
                        {
                            Clients.Client(tradeUser.Value.ID).BroadcastOrders(result);
                        }
                    }
                }
                Task.Run(() => GenerateMarketDepth(order.Symbol));
                Task.Run(() => ExecuteOrders(order));
            }
            finally
            {
                Monitor.Exit(_tradeOrders);
            }
            return _tradeOrders;
        }
        public void ExecuteOrders(Order order)
        {
            var executionType = NegateOrderType(order.Type);
            if (!_tradeOrders.ContainsKey(executionType) || !_tradeOrders[executionType].ContainsKey(order.Symbol) || 
                !_tradeOrders[executionType][order.Symbol].Any(x => x.Status!=OrderStatus.Executed && x.Status!= OrderStatus.Cancelled)) return;

            var executionOrders = _tradeOrders[executionType][order.Symbol].OrderBy((x) => x.Placed_Time);
            var quantity = order.BalQty;
            var count = executionOrders.Count();
            for ( int i=0;i< count; i++) 
            {
                var executionOrder = executionOrders.ElementAt(i);
                if (executionOrder.Status == OrderStatus.Cancelled ||
                    executionOrder.Status == OrderStatus.Executed) continue;
                if (order.Type == OrderType.Sell && order.Price > executionOrder.Price) continue;
                if (order.Type == OrderType.Buy && order.Price < executionOrder.Price) continue;
                ExecuteSingleOrder(ref order, ref executionOrder);
                if (order.BalQty == 0 || i==count-1)
                {
                    Clients.Client(order.UserId).NotifyUser($"Your order for {order.Symbol} of {order.Quantity-order.BalQty} is executed at Rs. {order.Price}", MessageType.Success);
                    break;
                }
            }
            foreach (var tradeUser in _tradeUsers)
            {
                IEnumerable<Order> bidTrade = null, askTrade = null;
                try
                {
                    bidTrade = _tradeOrders[OrderType.Buy][order.Symbol].Where(x => x.UserId == tradeUser.Value.ID);
                    askTrade = _tradeOrders[OrderType.Sell][order.Symbol]
                        .Where(x => x.UserId == tradeUser.Value.ID);
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    var result = bidTrade != null ? askTrade != null ? bidTrade.Concat(askTrade) : bidTrade :
                        askTrade != null ? askTrade : null;

                    if (result != null)
                    {
                        Clients.Client(tradeUser.Value.ID).BroadcastOrders(result);
                    }
                }
            }
            Task.Run(() => GenerateMarketDepth(order.Symbol));
        }
        public void ExecuteSingleOrder(ref Order seller,ref Order buyer)
        {

            Monitor.Enter(seller);
            Monitor.Enter(buyer);
            try
            {
                var sellerBalQty = seller.BalQty >= buyer.BalQty ? seller.BalQty - buyer.BalQty : 0;
                var buyerBalQty = buyer.BalQty > seller.BalQty ? buyer.BalQty - seller.BalQty : 0 ;
                seller.BalQty = sellerBalQty;
                buyer.BalQty = buyerBalQty;
                seller.Executed_Time = buyer.Executed_Time = DateTime.Now;
                buyer.Status = buyer.BalQty > 0 ? OrderStatus.Partial: OrderStatus.Executed;
                seller.Status = seller.BalQty > 0 ? OrderStatus.Partial : OrderStatus.Executed ;
                //BroadcastOrders(_tradeOrders);
                
                Clients.Client(buyer.UserId).NotifyUser($"Your order for {buyer.Symbol} of {buyer.Quantity - buyer.BalQty} is executed at Rs. {buyer.Price}", MessageType.Success);
            }
            finally
            {
                Monitor.Exit(buyer);
                Monitor.Exit(seller);
            }
        }
        public OrderType NegateOrderType(OrderType type) => type == OrderType.Buy ? OrderType.Sell :OrderType.Buy;
    }
}
