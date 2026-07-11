using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public class LeadProductInterest
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Lead))]
        public int IdLead { get; set; }

        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }

        public decimal Quantity { get; set; } = 1;

        [Column(TypeName = "Money")]
        public decimal UnitPrice { get; set; }

        public decimal DiscountPct { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineTotal { get; set; }

        public int SortOrder { get; set; }

        public virtual Lead? Lead { get; set; }

        public virtual Product? Product { get; set; }
    }

    public class DealProductInterest
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Deal))]
        public int IdDeal { get; set; }

        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }

        public decimal Quantity { get; set; } = 1;

        [Column(TypeName = "Money")]
        public decimal UnitPrice { get; set; }

        public decimal DiscountPct { get; set; }

        [Column(TypeName = "Money")]
        public decimal LineTotal { get; set; }

        public int SortOrder { get; set; }

        public virtual Deal? Deal { get; set; }

        public virtual Product? Product { get; set; }
    }
}
