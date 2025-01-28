using FarmFresh.Data;
using FarmFresh.Extensions;
using FarmFresh.Infrastructure.Extensions;
using FarmFresh.Mapper;
using FarmFresh.Services.Contacts;
using LoggerService.Contacts;
using Microsoft.AspNetCore.Identity;
using NLog;
using static FarmFresh.Commons.GeneralApplicationConstants;

var builder = WebApplication.CreateBuilder(args);

LogManager.LoadConfiguration(string.Concat(Directory.GetCurrentDirectory(), "/nlog.config"));

builder.Services.AddApplicationDbContext(builder.Configuration);
builder.Services.AddApplicationIdentity(builder.Configuration);
builder.Services.ConfigureRepositoryManager();
builder.Services.AddHttpClient();
builder.Services.ConfigureServiceManager();
builder.Services.ConfigureValidator();
builder.Services.ConfigureLoggerService();
builder.Services.ConfigureAccountService();
builder.Services.ConfigureCookieAuthentication();
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureServicesCORS();
builder.Services.ConfigureStriple(builder.Configuration);
builder.Services.AddHostedService<OrderStatusUpdater>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(opt =>
{
    opt.TokenLifespan = TimeSpan.FromMinutes(2);
});

builder.Services.AddControllersWithViews();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(AdminRoleName));
});

builder.Logging.ClearProviders();

builder.Services.AddAutoMapper(typeof(ProductMappingProfile).Assembly);

var app = builder.Build();

app.UseCors("AllowAll");
app.UseSession();

var logger = app.Services.GetRequiredService<ILoggerManager>();
app.ConfigureExceptionHandler(logger);

if (app.Environment.IsProduction())
    app.UseHsts();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.SeedAdministrator(DevelopmentAdminEmail);
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(config =>
{
    config.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
});

app.MapDefaultControllerRoute();
app.MapRazorPages();

//**TESTING PURPOSES**
//testing econt api calls
//using(var scope = app.Services.CreateScope())
//{
//    var serviceManager = scope.ServiceProvider.GetRequiredService<IServiceManager>();
//
//    await DBTransactionHelper.ExecuteTransactionAsync(scope.ServiceProvider, async () =>
//    {
//        var countryService = serviceManager.CountryService;
//        await countryService.UpdateCountriesAsync();
//    });
//    await DBTransactionHelper.ExecuteTransactionAsync(scope.ServiceProvider, async () =>
//    {
//        var cityService = serviceManager.CityService;
//        await cityService.UpdateCitiesAsync();
//    });
//    await DBTransactionHelper.ExecuteTransactionAsync(scope.ServiceProvider, async () =>
//    {
//        var officeService = serviceManager.OfficeService;
//        await officeService.UpdateOfficesAsync();
//    });
//    await DBTransactionHelper.ExecuteTransactionAsync(scope.ServiceProvider, async () =>
//    {
//        var streetService = serviceManager.StreetService;
//        await streetService.UpdateStreetsAsync();
//    });
//    await DBTransactionHelper.ExecuteTransactionAsync(scope.ServiceProvider, async () =>
//    {
//        var quarterService = serviceManager.QuarterService;
//        await quarterService.UpdateQuartersAsync();
//    });
//}

app.Run();