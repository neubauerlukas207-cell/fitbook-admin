using System.ComponentModel.DataAnnotations;

namespace FitBook.Admin.Domain;

public enum PlanKind
{
    /// <summary>Neomezené vstupy po dobu platnosti (měsíční, roční).</summary>
    Unlimited,
    /// <summary>Permanentka na daný počet vstupů.</summary>
    Entries,
}

public class MembershipPlan
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Název")]
    public string Name { get; set; } = "";

    [Display(Name = "Typ")]
    public PlanKind Kind { get; set; }

    [Range(1, 730)]
    [Display(Name = "Platnost (dní)")]
    public int ValidityDays { get; set; }

    [Range(1, 100)]
    [Display(Name = "Počet vstupů")]
    public int? Entries { get; set; }

    [Range(0, 100_000)]
    [Display(Name = "Cena (Kč)")]
    public decimal Price { get; set; }

    [Display(Name = "V prodeji")]
    public bool IsActive { get; set; } = true;
}

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [Phone, StringLength(20)]
    public string? Phone { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<Membership> Memberships { get; set; } = [];
    public List<CheckIn> CheckIns { get; set; } = [];
}

public class Membership
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int PlanId { get; set; }
    public MembershipPlan Plan { get; set; } = null!;

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    /// <summary>Zbývající vstupy; null = neomezeně.</summary>
    public int? RemainingEntries { get; set; }

    public decimal PricePaid { get; set; }
    public DateTime SoldAtUtc { get; set; }

    public bool IsValidOn(DateOnly day) =>
        day >= ValidFrom && day <= ValidTo && (RemainingEntries is null || RemainingEntries > 0);
}

public class CheckIn
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int MembershipId { get; set; }
    public Membership Membership { get; set; } = null!;
    public DateTime AtUtc { get; set; }
}
