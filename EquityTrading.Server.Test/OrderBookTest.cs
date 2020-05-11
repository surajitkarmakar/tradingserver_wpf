using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace EquityTrading.Server.Test
{
    [TestClass]
    public class OrderBookTest
    {
        [TestMethod]
        public void TestBuyOrderProcessing()
        {
            string symbol = "HDFCBANK";
            string userId = "surajit1234";
            string userName = "Surajit";
            Mock<User> mockUser = new Mock<User>();
            mockUser.SetupGet(x => x.ID).Returns(userId);
            mockUser.SetupGet(x => x.Name).Returns(userName);

            OrderBook orderBook = new OrderBook();
            Order order = new Order() { UserId = mockUser.Object.ID, UserName = mockUser.Object.Name, Type = OrderType.Buy, Price = 150, Quantity = 50, Placed_Time = DateTime.Now,Symbol = "HDFCBANK",Status = OrderStatus.Open};
            var result = orderBook.ProcessOrder(order);
            Assert.AreEqual(result[OrderType.Buy][symbol].First(x=>x.UserName==userName),order);
        }

        [TestMethod]
        public void TestSellOrderProcessing()
        {
            string symbol = "HDFCBANK";
            string userId = "surajit1234";
            string userName = "Surajit";
            Mock<User> mockUser = new Mock<User>();
            mockUser.SetupGet(x => x.ID).Returns(userId);
            mockUser.SetupGet(x => x.Name).Returns(userName);

            OrderBook orderBook = new OrderBook();
            Order order = new Order() { UserId = mockUser.Object.ID, UserName = mockUser.Object.Name, Type = OrderType.Sell, Price = 150, Quantity = 50, Placed_Time = DateTime.Now, Symbol = "HDFCBANK", Status = OrderStatus.Open };
            var result = orderBook.ProcessOrder(order);
            Assert.AreEqual(result[OrderType.Sell][symbol].First(x => x.UserName == userName), order);
        }
    }
}
