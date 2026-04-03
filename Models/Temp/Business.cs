using System;
using System.Collections.Generic;

namespace backend.Models.Temp;

public partial class Business
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = null!;

    public string Trn { get; set; } = null!;

    public string Sector { get; set; } = null!;

    public virtual ICollection<AspNetUser> AspNetUsers { get; set; } = new List<AspNetUser>();

    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();

    public virtual ICollection<TaxRecord> TaxRecords { get; set; } = new List<TaxRecord>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
