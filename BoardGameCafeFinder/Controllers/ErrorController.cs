using Microsoft.AspNetCore.Mvc;

namespace BoardGameCafeFinder.Controllers;

public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        switch (statusCode)
        {
            case 404:
                ViewBag.ErrorMessage = "The page you're looking for doesn't exist.";
                ViewBag.ErrorTitle = "Page Not Found";
                return View("NotFound");

            case 403:
                ViewBag.ErrorMessage = "You don't have permission to access this page.";
                ViewBag.ErrorTitle = "Access Denied";
                return View("Error");

            case 500:
                ViewBag.ErrorMessage = "Something went wrong on our end. Please try again later.";
                ViewBag.ErrorTitle = "Server Error";
                return View("Error");

            default:
                ViewBag.ErrorMessage = "An unexpected error occurred.";
                ViewBag.ErrorTitle = $"Error {statusCode}";
                return View("Error");
        }
    }

    [Route("Error")]
    public IActionResult Error()
    {
        ViewBag.ErrorTitle = "Error";
        ViewBag.ErrorMessage = "An unexpected error occurred. Please try again later.";
        return View();
    }
}
