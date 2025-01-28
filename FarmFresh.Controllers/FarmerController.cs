using FarmFresh.Commons.RequestFeatures;
using FarmFresh.Infrastructure.Extensions;
using FarmFresh.Services.Contacts;
using FarmFresh.ViewModels.Farmer;
using Microsoft.AspNetCore.Mvc;
using static FarmFresh.Commons.MessagesConstants.Farmers;
using static FarmFresh.Commons.MessagesConstants;
using static FarmFresh.Commons.NotificationMessagesConstants;

namespace FarmFresh.Controllers;

[Route("api/farmers")]
public class FarmerController : BaseController
{
    private readonly IServiceManager _serviceManager;

    public FarmerController(IServiceManager serviceManager)
    {
        _serviceManager = serviceManager;
    }

    [HttpGet("become")]
    public async Task<IActionResult> Become()
    {
        if(User.GetId() is null)
        {
            TempData[ErrorMessage] = UserIsNotLogin;
            return RedirectToAction("UnauthorizedError", "Error");
        }
        else if(await _serviceManager.FarmerService.DoesFarmerExistsByuserId(User.GetId(), trackChanges: false) == true)
        {
            TempData[ErrorMessage] = "You are already registered as a farmer.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

       return View();
    }

    [HttpPost("become")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Become(FarmerForCreationDto model)
    {
        var userId = User.GetId();

        if (await _serviceManager.FarmerService.DoesFarmerExistsByuserId(userId, trackChanges: false) == true)
        {
            TempData[ErrorMessage] = "You cannot register as a farmer more than once.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        //if (!ModelState.IsValid)
        //{
        //    return View(model);
        //}

        await _serviceManager.FarmerService.CreateFarmerAsync(model, userId, trackChanges: true);
        TempData[SuccessMessage] = SuccessfullyBecomeAFarmer;

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet("allFarmers")]
    public async Task<IActionResult> AllFarmers([FromQuery] FarmerParameters farmerParameters)
    {
        var (farmers, metaData) = await _serviceManager.FarmerService.GetAllFarmersAsync(farmerParameters, trackChanges: false);

        var model = await _serviceManager.FarmerService.CreateFarmersListViewModelAsync(farmers, metaData, farmerParameters.SearchTerm);

        return View(model);
    }

    [HttpGet("edit/{id}")]

    public async Task<IActionResult> Edit(Guid id)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorMessage] = UserIsNotLogin;
            return RedirectToAction("UnauthorizedError", "Error");
        }

        var editView =  await _serviceManager.FarmerService.GetFarmerForEditAsync(id, trackChanges: false);
        return View(editView);
    }

    [HttpPatch("edit/{farmerId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid farmerId, FarmerForUpdatingDto model)
    {
        if (ModelState.IsValid)
        {
            await _serviceManager.FarmerService.EditFarmerAsync(model, farmerId, trackChanges: true);
        }

        return Ok(new { success = true });
    }

    [HttpGet("profile/{id}")]
    public async Task<IActionResult> Profile(Guid id)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorMessage] = UserIsNotLogin;
            return RedirectToAction("UnauthorizedError", "Error");
        }
        var farmerProfile = await _serviceManager.FarmerService.GetFarmerProfileAsync(id.ToString());
        return View(farmerProfile);
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> Delete([FromBody] FarmerProfileViewModel model)
    {
        if (model.Id == Guid.Empty)
        {
            TempData[ErrorMessage] = UserIsNotLogin;
            return RedirectToAction("UnauthorizedError", "Error");
        }
        await _serviceManager.FarmerService.DeleteFarmerAsync(model.Id, trackChanges: true);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet("details/{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        if (id == Guid.Empty)
        {
            TempData[ErrorMessage] = UserIsNotLogin;
            return RedirectToAction("UnauthorizedError", "Error");
        }
        var model = await _serviceManager.FarmerService.GetFarmersDetailsByIdAsync(id, trackChanges: false);
        return View(model);
    }
}

