using Microsoft.AspNetCore.Mvc;

namespace VRArcadeFinder.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "Sorry, the page you requested could not be found.";
                    return View("NotFound");
                case 403:
                    ViewBag.ErrorMessage = "You don't have permission to access this resource.";
                    return View("Forbidden");
                default:
                    ViewBag.ErrorMessage = "An error occurred while processing your request.";
                    return View("Error");
            }
        }
    }
}
