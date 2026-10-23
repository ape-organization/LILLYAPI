using PharmacyAPI.Data;
using System;
using System.Collections.Generic;

namespace PharmacyAPI.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int ClientId { get; set; }

        public Client Client { get; set; } = null!;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
        public string? Note { get; set; }

        public ICollection<OrderItem> Items { get; set; }
            = new List<OrderItem>();
    }
}
