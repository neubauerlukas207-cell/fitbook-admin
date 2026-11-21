using System.ComponentModel.DataAnnotations;
using FitBook.Admin.Domain;
using FitBook.Admin.Services;

namespace FitBook.Admin.Models;

public record DashboardViewModel(DashboardStats Stats, IReadOnlyList<LessonDto>? Lessons, DateOnly Today);

public record CustomerRow(int Id, string Name, string Email, string? ActivePlan, DateOnly? ValidTo, int? RemainingEntries);

public record CustomerListViewModel(IReadOnlyList<CustomerRow> Customers, string? Query);

public record CustomerDetailViewModel(Customer Customer, IReadOnlyList<MembershipPlan> Plans, DateOnly Today);

public record CheckInViewModel(string? Query, IReadOnlyList<CustomerRow> Results, IReadOnlyList<CheckIn> Recent);

public class CustomerForm
{
    [Required(ErrorMessage = "Vyplňte jméno")]
    [StringLength(100)]
    [Display(Name = "Jméno a příjmení")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Vyplňte e-mail")]
    [EmailAddress(ErrorMessage = "Neplatný e-mail")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Phone(ErrorMessage = "Neplatné telefonní číslo")]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }
}
