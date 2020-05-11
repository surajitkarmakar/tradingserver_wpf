using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquityTrading.Server
{
    public class Order
    {
        public OrderType Type { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Symbol { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        public DateTime Placed_Time { get; set; }
        public DateTime Executed_Time { get; set; }
        public int ExecQty { get; set; }
        public int BalQty { get; set; }
        public OrderStatus Status { get; set; }
    }
}
