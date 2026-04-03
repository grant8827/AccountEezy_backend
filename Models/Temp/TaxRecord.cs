using System;
using System.Collections.Generic;

namespace backend.Models.Temp;

public partial class TaxRecord
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public decimal TotalRemittance { get; set; }

    public int Status { get; set; }

    public virtual Business Business { get; set; } = null!;
}
