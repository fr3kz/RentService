using Microsoft.AspNetCore.Mvc;

namespace RentService.Controllers.VehicleModule;

[Area("VehicleModule")]
public class VehicleController : Controller
{
    private readonly ILogger<VehicleController> _logger;

    public VehicleController(ILogger<VehicleController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    } 
}