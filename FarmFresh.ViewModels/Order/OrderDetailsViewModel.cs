using FarmFresh.Data.Models.Enums;
using FarmFresh.ViewModels.Product;

namespace FarmFresh.ViewModels.Order;

public record OrderDetailsViewModel(
    Guid Id,
    Guid OrderId,
    int Quantity,
    decimal Price,
    decimal TotalPrice,
    string FirstName,
    string LastName,
    string Adress,
    string PhoneNumber,
    string Email,
    string ProductName,
    DateTime CreatedDate,
    DeliveryOption DeliveryOption,
    OrderStatus OrderStatus,
    PaymentOption PaymentOption,
    string ProductDescription,
    string Origin,
    string FarmerName,
    decimal ProductPrice,
    bool IsPaymentSuccess,
    string ShipmentNumber,
    Seasons Seasons,
    DateTime HarvestDate,
    DateTime ExpirationDate,
    IEnumerable<ProductPhotosDto> Photos);