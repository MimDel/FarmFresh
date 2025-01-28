using System.ComponentModel.DataAnnotations;
using static FarmFresh.Commons.EntityValidationConstants.User;
using static FarmFresh.Commons.EntityValidationConstants.Orders;
using static FarmFresh.Commons.EntityValidationConstants.Farmers;
using FarmFresh.Data.Models.Enums;

namespace FarmFresh.Data.Models;

public class Order : Entity_1<Guid>
{

    [Required]
    [MaxLength(UserFirstNameMaxLength)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(UserLastNameMaxLength)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(OrderAdressMaxLength)]
    public string Adress { get; set; } = string.Empty;
    [Required]
    public string  StreetName { get; set; } = string.Empty;
    [Required]
    public string StreetNum { get; set; } = string.Empty;
    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(FarmerPhoneNumberMaxLength)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public DateTime CreateOrderdDate { get; set; }

    public DateTime? ShippedDate { get; set; }

    [Required]
    public DeliveryOption DeliveryOption { get; set; }

    public OrderStatus? OrderStatus { get; set; }

    public PaymentOption? PaymentOption { get; set; }

    public bool IsPaymentSuccsess { get; set; }

    public decimal TotalPrice { get; set; }

    public bool IsTaken { get; set; }

    public Guid UserId { get; set; }

    public Order(
        Guid id,
        string firstName,
        string lastName,
        string adress,
        string phoneNumber,
        string email,
        DateTime createorderddate,
        DeliveryOption deliveryOption,
        OrderStatus orderStatus,
        bool istaken,
        Guid userId)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Adress = adress;
        PhoneNumber = phoneNumber;
        Email = email;
        CreateOrderdDate = createorderddate;
        DeliveryOption = deliveryOption;
        OrderStatus = orderStatus;
        IsTaken = istaken;
        UserId = userId;
    }

    public Order()
    {
        Id = Guid.NewGuid();
        OrderProducts = new HashSet<OrderProduct>();
        ProductPhotos = new HashSet<ProductPhoto>();
    }

    public virtual ApplicationUser User { get; set; } = null!;

    public virtual ICollection<OrderProduct> OrderProducts { get; set; }

    public virtual ICollection<ProductPhoto> ProductPhotos { get; set; }

    public string? ShipmentNumber { get; set; }
}