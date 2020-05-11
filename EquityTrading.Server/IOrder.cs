using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquityTrading.Server
{
    public interface IOrder
    {
        void ParticipantDisconnection(string name);
        void ParticipantReconnection(string name);
        void ParticipantLogin(User user);
        void ParticipantLogout(string name);
        void NotifyUser(string message, MessageType type = MessageType.Info);
        void SendBidDepth(string json);
        void SendAskDepth(string json);
        void ProcessOrder(Order order);
        void BroadcastOrders(IEnumerable<Order> orders);
    }
    public enum OrderType { Buy, Sell }
    public enum OrderStatus { Open, Executed, Cancelled, Partial }
    public enum MessageType { Error, Info, Success, Warning }
}


    
