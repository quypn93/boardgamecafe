using EscapeRoomFinder.Models;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoomFinder.Controllers
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
                    403 => "You don't have permission to access this resource.",
                    500 => "Something went wrong on our end. Please try again later.",
                    _ => "An error occurred."
                }
            };

            return View("Error", model);
        }
    }
}
