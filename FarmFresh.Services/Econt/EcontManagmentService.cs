using AutoMapper;
using FarmFresh.Data.Models;
using FarmFresh.Data.Models.Econt.APIInterraction;
using FarmFresh.Data.Models.Econt.DTOs.NumenclatureDTOs;
using FarmFresh.Data.Models.Econt.DTOs.ShipmentDTOs;
using FarmFresh.Data.Models.Enums;
using FarmFresh.Data.Models.Repositories;
using FarmFresh.Services.Contacts.Econt;
using FarmFresh.Services.Econt.APIServices;
using FarmFresh.ViewModels.Order;
using LoggerService.Contacts;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace FarmFresh.Services.Econt;

public class EcontManagmentService : IEcontManagmentService
{
    private readonly IRepositoryManager _repositoryManager;
    private readonly ILoggerManager _loggerManager;
    private readonly IMapper _mapper;

    public EcontManagmentService(IRepositoryManager repositoryManager,
                      ILoggerManager loggerManager,
                      IMapper mapper)
    {
        _repositoryManager = repositoryManager;
        _loggerManager = loggerManager;
        _mapper = mapper;
    }

    public async Task<CreateLabelResponse> CreateLabel(Order order, bool trackChanges)
    {
        var orderDetails = await _repositoryManager.OrderRepository
            .FindOrderByConditionAsync(o => o.Id == order.Id, trackChanges)
            .Include(u => u.User)
            .Include(op => op.OrderProducts)
            .ThenInclude(op => op.Product)
            .ThenInclude(p => p.Farmer.User)
            .FirstOrDefaultAsync();

        var farmerLocation = await _repositoryManager.FarmerLocationRepository
           .FindFarmerLocationsByConditionAsync(fl => fl.Farmer.UserId == orderDetails.OrderProducts.First().Product.Farmer.UserId, trackChanges)
           .FirstOrDefaultAsync();

        var address = await GetAddressByLatAndLongAsync(farmerLocation.Longitude, farmerLocation.Latitude);
        var senderCityName = GetCityNameFromAddress(address);

        var senderAddress = CreateSenderAddress(senderCityName, address);
        var receiverAddress = CreateReceiverAddress(order.City, orderDetails);

        var productCount = orderDetails.OrderProducts.Sum(op => op.Quantity);
        var totalPrice = orderDetails.OrderProducts.Sum(op => op.Price * op.Quantity);

        var shippingLabel = CreateShippingLabel(orderDetails.OrderProducts.First().Product.Farmer.User, orderDetails.User, senderAddress, receiverAddress, productCount, totalPrice);

        return await CreateEcontLabelAsync(shippingLabel);
    }

    private string GetCityNameFromAddress(JObject address)
    {
        var addressComponents = address["results"]?[0]?["address_components"];
        return addressComponents?
            .FirstOrDefault(component => component["types"]?.Any(type => type.ToString() == "locality") ?? false)?
            ["long_name"]?.ToString()?.Trim();
    }

    private AddressDTO CreateSenderAddress(string cityName, JObject address)
    {
        var streetName = address["results"]?[0]?["address_components"]?
            .FirstOrDefault(component => component["types"]?.Any(type => type.ToString() == "route") ?? false)?
            ["long_name"]?.ToString()?.Trim();

        var cityDtoSender = new CityDTO
        {
            Name = cityName,
            Country = new CountryDTO { Code3 = "BGR" }
        };

        return new AddressDTO(cityDtoSender, streetName, "3");
    }

    private AddressDTO CreateReceiverAddress(string cityName, Order currentAdress)
    {
        var cityDtoReceiver = new CityDTO
        {
            Name = cityName,
            Country = new CountryDTO { Code3 = "BGR" }
        };

        return new AddressDTO(cityDtoReceiver, currentAdress.StreetName, currentAdress.StreetNum);
    }

    private ShippingLabelDTO CreateShippingLabel(ApplicationUser senderUser, ApplicationUser currUser, AddressDTO senderAddress, AddressDTO receiverAddress, int productCount, decimal totalPrice)
    {
        return new ShippingLabelDTO(
            new ClientProfileDTO(senderUser.FirstName + " " + senderUser.LastName, new List<string> { "0000000000" }),
            senderAddress,
            new ClientProfileDTO(currUser.FirstName + " " + currUser.LastName, new List<string> { "1111111111" }),
            receiverAddress,
            productCount,
            productCount * 1,
            ShipmentType.Pack,
            "Order shipment",
            (double)totalPrice
        );
    }

    private async Task<CreateLabelResponse> CreateEcontLabelAsync(ShippingLabelDTO shippingLabel)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var httpClient = new HttpClient();
        var _econtLabelService = new EcontLabelService(configuration, httpClient);
        var LabelRequest = new CreateLabelRequest(shippingLabel);

        return await _econtLabelService.CreateLabelAsync(LabelRequest);
    }

    //public async Task<JObject> GetAddressByLatAndLongAsync(double latitude, double longitude)
    //{
    //    //var apiKey = "AIzaSyDn87dIETpeaJSov9jznA0k9YUAs7Fs5QA";
    //    //var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={longitude},{latitude}&key={apiKey}";
    //   
    //    using (var httpClient = new HttpClient())
    //    {
    //        // var response = await httpClient.GetAsync(requestUrl);
    //        var jsonResponse = "\"{ \\\"plus_code\\\" : { \\\"compound_code\\\" : \\\"FFWG+M4V Burgas, Bulgaria\\\", \\\"global_code\\\" : \\\"8GJ9FFWG+M4V\\\" }, \\\"results\\\" : [ { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"15А\\\", \\\"short_name\\\" : \\\"15А\\\", \\\"types\\\" : [ \\\"street_number\\\" ] }, { \\\"long_name\\\" : \\\"ulitsa \\\\\\\"Trayko Kitanchev\\\\\\\"\\\", \\\"short_name\\\" : \\\"ul. \\\\\\\"Trayko Kitanchev\\\\\\\"\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, ul. \\\\\\\"Trayko Kitanchev\\\\\\\" 15А, 8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.49665760000001, \\\"lng\\\" : 27.4753017 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4965349, \\\"lng\\\" : 27.4750941 } }, \\\"location\\\" : { \\\"lat\\\" : 42.4966023, \\\"lng\\\" : 27.4751954 }, \\\"location_type\\\" : \\\"ROOFTOP\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.49794523029151, \\\"lng\\\" : 27.4765468802915 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.49524726970851, \\\"lng\\\" : 27.47384891970849 } } }, \\\"navigation_points\\\" : [ { \\\"location\\\" : { \\\"latitude\\\" : 42.4965463, \\\"longitude\\\" : 27.4751075 } } ], \\\"place_id\\\" : \\\"ChIJv3fFQcCUpkARf6wGZHgUX34\\\", \\\"types\\\" : [ \\\"premise\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"25\\\", \\\"short_name\\\" : \\\"25\\\", \\\"types\\\" : [ \\\"street_number\\\" ] }, { \\\"long_name\\\" : \\\"ulitsa \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"short_name\\\" : \\\"ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\" 25, 8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"location\\\" : { \\\"lat\\\" : 42.4969147, \\\"lng\\\" : 27.4751714 }, \\\"location_type\\\" : \\\"ROOFTOP\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.4982636802915, \\\"lng\\\" : 27.4765203802915 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4955657197085, \\\"lng\\\" : 27.4738224197085 } } }, \\\"navigation_points\\\" : [ { \\\"location\\\" : { \\\"latitude\\\" : 42.4969385, \\\"longitude\\\" : 27.4751493 } } ], \\\"place_id\\\" : \\\"ChIJtdP5FMCUpkARywe3conHSpQ\\\", \\\"plus_code\\\" : { \\\"compound_code\\\" : \\\"FFWG+Q3 Burgas, Bulgaria\\\", \\\"global_code\\\" : \\\"8GJ9FFWG+Q3\\\" }, \\\"types\\\" : [ \\\"street_address\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"23\\\", \\\"short_name\\\" : \\\"23\\\", \\\"types\\\" : [ \\\"street_number\\\" ] }, { \\\"long_name\\\" : \\\"ulitsa \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"short_name\\\" : \\\"ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\" 23, 8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"location\\\" : { \\\"lat\\\" : 42.4968794, \\\"lng\\\" : 27.4750663 }, \\\"location_type\\\" : \\\"ROOFTOP\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.4982283802915, \\\"lng\\\" : 27.4764152802915 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4955304197085, \\\"lng\\\" : 27.4737173197085 } } }, \\\"navigation_points\\\" : [ { \\\"location\\\" : { \\\"latitude\\\" : 42.4968911, \\\"longitude\\\" : 27.4750553 } } ], \\\"place_id\\\" : \\\"ChIJw9JLFMCUpkARcN9RcYq9df4\\\", \\\"types\\\" : [ \\\"subpremise\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"25\\\", \\\"short_name\\\" : \\\"25\\\", \\\"types\\\" : [ \\\"street_number\\\" ] }, { \\\"long_name\\\" : \\\"ulitsa \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"short_name\\\" : \\\"ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\" 25, 8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"location\\\" : { \\\"lat\\\" : 42.4969905, \\\"lng\\\" : 27.4751011 }, \\\"location_type\\\" : \\\"RANGE_INTERPOLATED\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.4983394802915, \\\"lng\\\" : 27.47645008029151 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4956415197085, \\\"lng\\\" : 27.4737521197085 } } }, \\\"place_id\\\" : \\\"EkpCdXJnYXMgQ2VudGVyLCB1bC4gIlN2ZXRpIFN2ZXRpIEtpcmlsIEkgTWV0b2RpeSIgMjUsIDgwMDAgQnVyZ2FzLCBCdWxnYXJpYSIaEhgKFAoSCQ0fbhTAlKZAEXTfJ5HMRLO-EBk\\\", \\\"types\\\" : [ \\\"street_address\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"FFWG+M4\\\", \\\"short_name\\\" : \\\"FFWG+M4\\\", \\\"types\\\" : [ \\\"plus_code\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"FFWG+M4 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.49675, \\\"lng\\\" : 27.475375 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.49662499999999, \\\"lng\\\" : 27.47525 } }, \\\"location\\\" : { \\\"lat\\\" : 42.4967486, \\\"lng\\\" : 27.475294 }, \\\"location_type\\\" : \\\"GEOMETRIC_CENTER\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.4980364802915, \\\"lng\\\" : 27.4766614802915 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4953385197085, \\\"lng\\\" : 27.47396351970849 } } }, \\\"place_id\\\" : \\\"GhIJuapHdZU_RUARKvwZ3qx5O0A\\\", \\\"plus_code\\\" : { \\\"compound_code\\\" : \\\"FFWG+M4 Burgas, Bulgaria\\\", \\\"global_code\\\" : \\\"8GJ9FFWG+M4\\\" }, \\\"types\\\" : [ \\\"plus_code\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"21-29\\\", \\\"short_name\\\" : \\\"21-29\\\", \\\"types\\\" : [ \\\"street_number\\\" ] }, { \\\"long_name\\\" : \\\"ulitsa \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"short_name\\\" : \\\"ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\"\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, ul. \\\\\\\"Sveti Sveti Kiril I Metodiy\\\\\\\" 21-29, 8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.4972, \\\"lng\\\" : 27.4755166 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4967919, \\\"lng\\\" : 27.4747072 } }, \\\"location\\\" : { \\\"lat\\\" : 42.496996, \\\"lng\\\" : 27.4751119 }, \\\"location_type\\\" : \\\"GEOMETRIC_CENTER\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.49834493029149, \\\"lng\\\" : 27.4764608802915 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4956469697085, \\\"lng\\\" : 27.47376291970849 } } }, \\\"place_id\\\" : \\\"ChIJDR9uFMCUpkARdN8nkcxEs74\\\", \\\"types\\\" : [ \\\"route\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"Burgas Center\\\", \\\"short_name\\\" : \\\"Burgas Center\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Center, Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.5037678, \\\"lng\\\" : 27.4810436 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4886806, \\\"lng\\\" : 27.4671984 } }, \\\"location\\\" : { \\\"lat\\\" : 42.4967486, \\\"lng\\\" : 27.4752939 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.5037678, \\\"lng\\\" : 27.4810436 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4886806, \\\"lng\\\" : 27.4671984 } } }, \\\"place_id\\\" : \\\"ChIJk99rZMCUpkARprDSxmBbDrQ\\\", \\\"types\\\" : [ \\\"neighborhood\\\", \\\"political\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"8000\\\", \\\"short_name\\\" : \\\"8000\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"8000 Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.5521614, \\\"lng\\\" : 27.4936135 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4767887, \\\"lng\\\" : 27.4564921 } }, \\\"location\\\" : { \\\"lat\\\" : 42.5187534, \\\"lng\\\" : 27.4772799 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.5521614, \\\"lng\\\" : 27.4936135 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4767887, \\\"lng\\\" : 27.4564921 } } }, \\\"place_id\\\" : \\\"ChIJSQXyNIaUpkAR3kFItrFPSCE\\\", \\\"types\\\" : [ \\\"postal_code\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.6139216, \\\"lng\\\" : 27.5458556 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4391223, \\\"lng\\\" : 27.3580761 } }, \\\"location\\\" : { \\\"lat\\\" : 42.50479259999999, \\\"lng\\\" : 27.4626361 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.6139216, \\\"lng\\\" : 27.5458556 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.4391223, \\\"lng\\\" : 27.3580761 } } }, \\\"place_id\\\" : \\\"ChIJkZ38-WaSpkAR8E2_aRKgAAQ\\\", \\\"types\\\" : [ \\\"locality\\\", \\\"political\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"Burgas Municipality\\\", \\\"short_name\\\" : \\\"Burgas Municipality\\\", \\\"types\\\" : [ \\\"administrative_area_level_2\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas Municipality, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.7126812, \\\"lng\\\" : 27.5700188 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.3713306, \\\"lng\\\" : 27.210996 } }, \\\"location\\\" : { \\\"lat\\\" : 42.50480200000001, \\\"lng\\\" : 27.4626269 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.7126812, \\\"lng\\\" : 27.5700188 }, \\\"southwest\\\" : { \\\"lat\\\" : 42.3713306, \\\"lng\\\" : 27.210996 } } }, \\\"place_id\\\" : \\\"ChIJ75xq2WOSpkARsn-MwekSb7c\\\", \\\"types\\\" : [ \\\"administrative_area_level_2\\\", \\\"political\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"Burgas\\\", \\\"short_name\\\" : \\\"Burgas\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"Burgas, Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.9848614, \\\"lng\\\" : 28.035258 }, \\\"southwest\\\" : { \\\"lat\\\" : 41.904492, \\\"lng\\\" : 26.5786548 } }, \\\"location\\\" : { \\\"lat\\\" : 42.5048, \\\"lng\\\" : 27.4626079 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 42.9848614, \\\"lng\\\" : 28.035258 }, \\\"southwest\\\" : { \\\"lat\\\" : 41.904492, \\\"lng\\\" : 26.5786548 } } }, \\\"place_id\\\" : \\\"ChIJXzuWWd_tpkARIEy_aRKgAAM\\\", \\\"types\\\" : [ \\\"administrative_area_level_1\\\", \\\"political\\\" ] }, { \\\"address_components\\\" : [ { \\\"long_name\\\" : \\\"Bulgaria\\\", \\\"short_name\\\" : \\\"BG\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"formatted_address\\\" : \\\"Bulgaria\\\", \\\"geometry\\\" : { \\\"bounds\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 44.2154806, \\\"lng\\\" : 28.7292001 }, \\\"southwest\\\" : { \\\"lat\\\" : 41.2353414, \\\"lng\\\" : 22.3571566 } }, \\\"location\\\" : { \\\"lat\\\" : 42.733883, \\\"lng\\\" : 25.48583 }, \\\"location_type\\\" : \\\"APPROXIMATE\\\", \\\"viewport\\\" : { \\\"northeast\\\" : { \\\"lat\\\" : 44.2154806, \\\"lng\\\" : 28.7292001 }, \\\"southwest\\\" : { \\\"lat\\\" : 41.2353414, \\\"lng\\\" : 22.3571566 } } }, \\\"place_id\\\" : \\\"ChIJifBbyMH-qEAREEy_aRKgAAA\\\", \\\"types\\\" : [ \\\"country\\\", \\\"political\\\" ] } ], \\\"status\\\" : \\\"OK\\\" }\"";
    //        //if (!response.IsSuccessStatusCode)
    //        //{
    //        //    throw new Exception("Failed to fetch geocoding data.");
    //        //}
    //
    //        //var jsonResponse = await response.Content.ReadAsStringAsync();
    //        Console.WriteLine($"Google Maps Response: {jsonResponse}");
    //        return JObject.Parse(jsonResponse);
    //    }
    //}

    public async Task<JObject> GetAddressByLatAndLongAsync(double latitude, double longitude)
    {
        //var apiKey = "AIzaSyDn87dIETpeaJSov9jznA0k9YUAs7Fs5QA";
        //var requestUrl = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={longitude},{latitude}&key={apiKey}";

        //using (var httpClient = new HttpClient())
        //{
        //    var response = await httpClient.GetAsync(requestUrl);
        //    if (!response.IsSuccessStatusCode)
        //    {
        //        throw new Exception("Failed to fetch geocoding data.");
        //    }

        var jsonResponse = "{\r\n   \"plus_code\" :\r\n   {\r\n      \"compound_code\" : \"FFWG+M4V Burgas, Bulgaria\",\r\n      \"global_code\" : \"8GJ9FFWG+M4V\"\r\n   },\r\n   \"results\" :\r\n   [\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"15?\",\r\n               \"short_name\" : \"15?\",\r\n               \"types\" :\r\n               [\r\n                  \"street_number\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"ulitsa \\\"Trayko Kitanchev\\\"\",\r\n               \"short_name\" : \"ul. \\\"Trayko Kitanchev\\\"\",\r\n               \"types\" :\r\n               [\r\n                  \"route\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, ul. \\\"Trayko Kitanchev\\\" 15?, 8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.49665760000001,\r\n                  \"lng\" : 27.4753017\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4965349,\r\n                  \"lng\" : 27.4750941\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4966023,\r\n               \"lng\" : 27.4751954\r\n            },\r\n            \"location_type\" : \"ROOFTOP\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.49794523029151,\r\n                  \"lng\" : 27.4765468802915\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.49524726970851,\r\n                  \"lng\" : 27.47384891970849\r\n               }\r\n            }\r\n         },\r\n         \"navigation_points\" :\r\n         [\r\n            {\r\n               \"location\" :\r\n               {\r\n                  \"latitude\" : 42.4965463,\r\n                  \"longitude\" : 27.4751075\r\n               }\r\n            }\r\n         ],\r\n         \"place_id\" : \"ChIJv3fFQcCUpkARf6wGZHgUX34\",\r\n         \"types\" :\r\n         [\r\n            \"premise\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"25\",\r\n               \"short_name\" : \"25\",\r\n               \"types\" :\r\n               [\r\n                  \"street_number\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"ulitsa \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"short_name\" : \"ul. \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"types\" :\r\n               [\r\n                  \"route\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, ul. \\\"Sveti Sveti Kiril I Metodiy\\\" 25, 8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4969147,\r\n               \"lng\" : 27.4751714\r\n            },\r\n            \"location_type\" : \"ROOFTOP\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.4982636802915,\r\n                  \"lng\" : 27.4765203802915\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4955657197085,\r\n                  \"lng\" : 27.4738224197085\r\n               }\r\n            }\r\n         },\r\n         \"navigation_points\" :\r\n         [\r\n            {\r\n               \"location\" :\r\n               {\r\n                  \"latitude\" : 42.4969385,\r\n                  \"longitude\" : 27.4751493\r\n               }\r\n            }\r\n         ],\r\n         \"place_id\" : \"ChIJtdP5FMCUpkARywe3conHSpQ\",\r\n         \"plus_code\" :\r\n         {\r\n            \"compound_code\" : \"FFWG+Q3 Burgas, Bulgaria\",\r\n            \"global_code\" : \"8GJ9FFWG+Q3\"\r\n         },\r\n         \"types\" :\r\n         [\r\n            \"street_address\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"23\",\r\n               \"short_name\" : \"23\",\r\n               \"types\" :\r\n               [\r\n                  \"street_number\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"ulitsa \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"short_name\" : \"ul. \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"types\" :\r\n               [\r\n                  \"route\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, ul. \\\"Sveti Sveti Kiril I Metodiy\\\" 23, 8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4968794,\r\n               \"lng\" : 27.4750663\r\n            },\r\n            \"location_type\" : \"ROOFTOP\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.4982283802915,\r\n                  \"lng\" : 27.4764152802915\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4955304197085,\r\n                  \"lng\" : 27.4737173197085\r\n               }\r\n            }\r\n         },\r\n         \"navigation_points\" :\r\n         [\r\n            {\r\n               \"location\" :\r\n               {\r\n                  \"latitude\" : 42.4968911,\r\n                  \"longitude\" : 27.4750553\r\n               }\r\n            }\r\n         ],\r\n         \"place_id\" : \"ChIJw9JLFMCUpkARcN9RcYq9df4\",\r\n         \"types\" :\r\n         [\r\n            \"subpremise\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"25\",\r\n               \"short_name\" : \"25\",\r\n               \"types\" :\r\n               [\r\n                  \"street_number\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"ulitsa \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"short_name\" : \"ul. \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"types\" :\r\n               [\r\n                  \"route\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, ul. \\\"Sveti Sveti Kiril I Metodiy\\\" 25, 8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4969905,\r\n               \"lng\" : 27.4751011\r\n            },\r\n            \"location_type\" : \"RANGE_INTERPOLATED\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.4983394802915,\r\n                  \"lng\" : 27.47645008029151\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4956415197085,\r\n                  \"lng\" : 27.4737521197085\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"EkpCdXJnYXMgQ2VudGVyLCB1bC4gIlN2ZXRpIFN2ZXRpIEtpcmlsIEkgTWV0b2RpeSIgMjUsIDgwMDAgQnVyZ2FzLCBCdWxnYXJpYSIaEhgKFAoSCQ0fbhTAlKZAEXTfJ5HMRLO-EBk\",\r\n         \"types\" :\r\n         [\r\n            \"street_address\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"FFWG+M4\",\r\n               \"short_name\" : \"FFWG+M4\",\r\n               \"types\" :\r\n               [\r\n                  \"plus_code\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"FFWG+M4 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.49675,\r\n                  \"lng\" : 27.475375\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.49662499999999,\r\n                  \"lng\" : 27.47525\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4967486,\r\n               \"lng\" : 27.475294\r\n            },\r\n            \"location_type\" : \"GEOMETRIC_CENTER\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.4980364802915,\r\n                  \"lng\" : 27.4766614802915\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4953385197085,\r\n                  \"lng\" : 27.47396351970849\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"GhIJuapHdZU_RUARKvwZ3qx5O0A\",\r\n         \"plus_code\" :\r\n         {\r\n            \"compound_code\" : \"FFWG+M4 Burgas, Bulgaria\",\r\n            \"global_code\" : \"8GJ9FFWG+M4\"\r\n         },\r\n         \"types\" :\r\n         [\r\n            \"plus_code\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"21-29\",\r\n               \"short_name\" : \"21-29\",\r\n               \"types\" :\r\n               [\r\n                  \"street_number\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"ulitsa \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"short_name\" : \"ul. \\\"Sveti Sveti Kiril I Metodiy\\\"\",\r\n               \"types\" :\r\n               [\r\n                  \"route\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, ul. \\\"Sveti Sveti Kiril I Metodiy\\\" 21-29, 8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.4972,\r\n                  \"lng\" : 27.4755166\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4967919,\r\n                  \"lng\" : 27.4747072\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.496996,\r\n               \"lng\" : 27.4751119\r\n            },\r\n            \"location_type\" : \"GEOMETRIC_CENTER\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.49834493029149,\r\n                  \"lng\" : 27.4764608802915\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4956469697085,\r\n                  \"lng\" : 27.47376291970849\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJDR9uFMCUpkARdN8nkcxEs74\",\r\n         \"types\" :\r\n         [\r\n            \"route\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"Burgas Center\",\r\n               \"short_name\" : \"Burgas Center\",\r\n               \"types\" :\r\n               [\r\n                  \"neighborhood\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Center, Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.5037678,\r\n                  \"lng\" : 27.4810436\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4886806,\r\n                  \"lng\" : 27.4671984\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.4967486,\r\n               \"lng\" : 27.4752939\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.5037678,\r\n                  \"lng\" : 27.4810436\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4886806,\r\n                  \"lng\" : 27.4671984\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJk99rZMCUpkARprDSxmBbDrQ\",\r\n         \"types\" :\r\n         [\r\n            \"neighborhood\",\r\n            \"political\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"8000\",\r\n               \"short_name\" : \"8000\",\r\n               \"types\" :\r\n               [\r\n                  \"postal_code\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"8000 Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.5521614,\r\n                  \"lng\" : 27.4936135\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4767887,\r\n                  \"lng\" : 27.4564921\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.5187534,\r\n               \"lng\" : 27.4772799\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.5521614,\r\n                  \"lng\" : 27.4936135\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4767887,\r\n                  \"lng\" : 27.4564921\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJSQXyNIaUpkAR3kFItrFPSCE\",\r\n         \"types\" :\r\n         [\r\n            \"postal_code\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"locality\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.6139216,\r\n                  \"lng\" : 27.5458556\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4391223,\r\n                  \"lng\" : 27.3580761\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.50479259999999,\r\n               \"lng\" : 27.4626361\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.6139216,\r\n                  \"lng\" : 27.5458556\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.4391223,\r\n                  \"lng\" : 27.3580761\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJkZ38-WaSpkAR8E2_aRKgAAQ\",\r\n         \"types\" :\r\n         [\r\n            \"locality\",\r\n            \"political\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"Burgas Municipality\",\r\n               \"short_name\" : \"Burgas Municipality\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_2\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas Municipality, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.7126812,\r\n                  \"lng\" : 27.5700188\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.3713306,\r\n                  \"lng\" : 27.210996\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.50480200000001,\r\n               \"lng\" : 27.4626269\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.7126812,\r\n                  \"lng\" : 27.5700188\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 42.3713306,\r\n                  \"lng\" : 27.210996\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJ75xq2WOSpkARsn-MwekSb7c\",\r\n         \"types\" :\r\n         [\r\n            \"administrative_area_level_2\",\r\n            \"political\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"Burgas\",\r\n               \"short_name\" : \"Burgas\",\r\n               \"types\" :\r\n               [\r\n                  \"administrative_area_level_1\",\r\n                  \"political\"\r\n               ]\r\n            },\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Burgas, Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.9848614,\r\n                  \"lng\" : 28.035258\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 41.904492,\r\n                  \"lng\" : 26.5786548\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.5048,\r\n               \"lng\" : 27.4626079\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 42.9848614,\r\n                  \"lng\" : 28.035258\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 41.904492,\r\n                  \"lng\" : 26.5786548\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJXzuWWd_tpkARIEy_aRKgAAM\",\r\n         \"types\" :\r\n         [\r\n            \"administrative_area_level_1\",\r\n            \"political\"\r\n         ]\r\n      },\r\n      {\r\n         \"address_components\" :\r\n         [\r\n            {\r\n               \"long_name\" : \"Bulgaria\",\r\n               \"short_name\" : \"BG\",\r\n               \"types\" :\r\n               [\r\n                  \"country\",\r\n                  \"political\"\r\n               ]\r\n            }\r\n         ],\r\n         \"formatted_address\" : \"Bulgaria\",\r\n         \"geometry\" :\r\n         {\r\n            \"bounds\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 44.2154806,\r\n                  \"lng\" : 28.7292001\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 41.2353414,\r\n                  \"lng\" : 22.3571566\r\n               }\r\n            },\r\n            \"location\" :\r\n            {\r\n               \"lat\" : 42.733883,\r\n               \"lng\" : 25.48583\r\n            },\r\n            \"location_type\" : \"APPROXIMATE\",\r\n            \"viewport\" :\r\n            {\r\n               \"northeast\" :\r\n               {\r\n                  \"lat\" : 44.2154806,\r\n                  \"lng\" : 28.7292001\r\n               },\r\n               \"southwest\" :\r\n               {\r\n                  \"lat\" : 41.2353414,\r\n                  \"lng\" : 22.3571566\r\n               }\r\n            }\r\n         },\r\n         \"place_id\" : \"ChIJifBbyMH-qEAREEy_aRKgAAA\",\r\n         \"types\" :\r\n         [\r\n            \"country\",\r\n            \"political\"\r\n         ]\r\n      }\r\n   ],\r\n   \"status\" : \"OK\"\r\n}"; /*await response.Content.ReadAsStringAsync();*/
        Console.WriteLine($"Google Maps Response: {jsonResponse}");
        return JObject.Parse(jsonResponse);
        //}
    }

    public async Task<IEnumerable<string>> GetCitiesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<string>();

        var cities = await _repositoryManager.CityRepository
            .FindCitiesByCondition(c => c.Name.StartsWith(searchTerm), trackChanges: true)
             .Select(c => c.Name)
            .Take(10)
            .ToListAsync();

        return cities;
    }

    public async Task<IEnumerable<string>> GetEcontOfficesAsync(string cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
            return Enumerable.Empty<string>();

        var offices = await _repositoryManager.OfficeRepository
           .FindOfficesByCondition(o =>
             o.Address != null &&
             o.Address.City != null &&
             o.Address.City.Name.ToLower() == cityName.ToLower(),
             trackChanges: true)
           .Select(o => o.Address.FullAddress)
           .ToListAsync();

        return offices;
    }

    public async Task<decimal> CalculatePrice(Order order, bool trackChanges)
    {
        var cartItems = await _repositoryManager.CartItemRepository
            .FindCartItemsByConditionAsync(c => c.UserId == order.UserId, trackChanges)
            .Include(p => p.Product)
            .ThenInclude(p => p.Farmer.User)
            .Include(ph => ph.Product.ProductPhotos)
            .ToListAsync();

        var farmerLocations = await _repositoryManager.FarmerLocationRepository
            .FindFarmerLocationsByConditionAsync(
            f => cartItems.Select(ci => ci.Product.Farmer.UserId).Contains(f.Farmer.UserId), trackChanges)
            .Include(f => f.Farmer)
            .ToListAsync();

        var cartItemViewModels = _mapper.Map<IEnumerable<CartItemViewModel>>(cartItems);
        decimal totalSum = 0m;

        try
        {
            foreach(var cartItem in cartItemViewModels)
            {
                var product = cartItems.First(ci => ci.ProductId == cartItem.ProductId).Product;
                var farmer = product.Farmer;
                var senderUser = farmer.User;
                var farmerLocation = farmerLocations.FirstOrDefault(fl => fl.Farmer.UserId == senderUser.Id);

                var address = await GetAddressByLatAndLongAsync(farmerLocation.Longitude, farmerLocation.Latitude);

                var senderCityName = ExtractCityNameFromAddress(address);
                var streetName = ExtractStreetNameFromAddress(address);

                var shipmentRequest = BuildShipmentRequest(cartItem, order, senderUser, senderCityName, streetName);
                var shipmentPrice = await CalculateShipmentPrice(shipmentRequest);
                totalSum += shipmentPrice;
            }
        }
        catch (Exception ex)
        {
            _loggerManager.LogError($"An unexpected error occurred: {ex.Message}");
            throw;
        }

        return totalSum;
    }

    private string ExtractCityNameFromAddress(JObject address)
    {
        var addressComponents = address["results"]?[0]?["address_components"];
        return addressComponents?
            .FirstOrDefault(component => component["types"]?.Any(type => type.ToString() == "locality") ?? false)?
            ["long_name"]?.ToString()?.Trim();
    }

    private string ExtractStreetNameFromAddress(JObject address)
    {
        var addressComponents = address["results"]?[0]?["address_components"];
        return addressComponents?
            .FirstOrDefault(component => component["types"]?.Any(type => type.ToString() == "route") ?? false)?
            ["long_name"]?.ToString()?.Trim();
    }

    private CalculateShipmentPriceRequest BuildShipmentRequest(CartItemViewModel cartItem, Order order,
                                                               ApplicationUser senderUser, string senderCityName,
                                                               string streetName)
    {
        var senderCity = new CityDTO
        {
            Name = senderCityName,
            Country = new CountryDTO { Code3 = "BGR" }
        };

        var receiverCity = new CityDTO
        {
            Name = order.City,
            Country = new CountryDTO { Code3 = "BGR" }
        };

        return new CalculateShipmentPriceRequest(
       new ShippingLabelDTO(
           senderClient: new ClientProfileDTO($"{senderUser.FirstName} {senderUser.LastName}", new List<string> { senderUser.PhoneNumber }),
           senderAddress: new AddressDTO(
               city: senderCity,
               streetName: streetName,
               streetNum: "3"
           ),
           receiverClient: new ClientProfileDTO($"{order.FirstName} {order.LastName}", new List<string> { order.PhoneNumber }),
           receiverAddress: new AddressDTO(
               city: receiverCity,
               streetName: order.StreetName,
               streetNum: order.StreetNum
           ),
           packCount: cartItem.Quantity,
           weigth: 1 * order.OrderProducts.Count(),
           shipmentType: ShipmentType.Pack,
           shipmentDescription: "Order shipment",
           orderPrice: (double)cartItem.TotalPrice
       )
   );
    }

    private async Task<decimal> CalculateShipmentPrice(CalculateShipmentPriceRequest request)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var httpClient = new HttpClient();
        var econtLabelService = new EcontLabelService(configuration, httpClient);
        var shipmentPrice = await econtLabelService.CalculateShipmentAsync(request);
        return shipmentPrice.HasValue ? (decimal)shipmentPrice.Value : 0m;
    }
}
