using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Error() => View();
}
