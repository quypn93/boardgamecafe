using CookingClassFinder.Models;
using Microsoft.AspNetCore.Mvc;

namespace CookingClassFinder.Controllers
{
    public class ErrorController : Controller
    {
        [Route("error/{statusCode}")]
        public IActionResult Index(int statusCode)
        {
            var model = new ErrorViewModel
            {
                StatusCode = statusCode,
                Message = statusCode switch
                {
                    404 => "The page you're looking for doesn't exist.",
                    500 => "Something went wrong on our end. Please try again later.",
                    _ => "An error occurred."
                }
            };

            return View(model);
        }
    }
}
