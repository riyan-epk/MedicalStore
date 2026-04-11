namespace MedicalStore.Common.Models
{
    public class LedgerEntry
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = ""; // Sale, Return, Payment, Replacement
        public string Reference { get; set; } = ""; // Invoice No or ID
        public decimal Debit { get; set; } // Customer owes (Sale, Replacement Extra)
        public decimal Credit { get; set; } // Customer pays or returns (Payment, Return, Replacement Refund)
        public decimal BalanceAfter { get; set; }
        public string? Notes { get; set; }
    }
}
