using FarmFresh.Infrastructure.Validation;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FarmFresh.ViewModels.Farmer;

public abstract record FarmerForManipulationDto
{
    [Required]
    [Display(Name = "Please enter information about your farm!")]
    public string FarmDescription { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Please enter the location of your farm!")]
    public string Location { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Please enter your valid phone number!")]
    public string PhoneNumber { get; init; } = string.Empty;

    public byte[] Photo { get; init; } = new byte[0];

    public string? PhotoString { get; init; }

    [DataType(DataType.Upload)]
    public IFormFile? PhotoFile { get; init; }

    [Required]
    [Display(Name = "Please enter your valid egn!")]
    //[EgnValidator]
    public string Egn { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Please enter your date of birth!")]
    public DateTime BirthDate { get; init; }

    [PrivacyAccepted]
    public bool PrivacyAccepted { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public Guid Id { get; init; }
}
