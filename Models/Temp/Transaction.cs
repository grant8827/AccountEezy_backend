using System;
using System.Collections.Generic;

namespace backend.Models.Temp;

public partial class Transaction
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public decimal Amount { get; set; }

    public int Type { get; set; }

    public bool GctApplicable { get; set; }

    public decimal GctAmount { get; set; }

    public string Category { get; set; } = null!;

    public DateTime Date { get; set; }

    public virtual Business Business { get; set; } = null!;
}
