using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoomFinder.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogService _blogService;
        private readonly ILogger<BlogController> _logger;

        public BlogController(IBlogService blogService, ILogger<BlogController> logger)
        {
            _blogService = blogService;
            _logger = logger;
        }

        [Route("blog")]
        public async Task<IActionResult> Index(int page = 1)
        {
            var posts = await _blogService.GetPublishedPostsAsync(page, 10);
            ViewBag.Page = page;
            ViewBag.TotalPosts = await _blogService.GetTotalPostCountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling((double)ViewBag.TotalPosts / 10);

            return View(posts);
        }

        [Route("blog/{slug}")]
        public async Task<IActionResult> Post(string slug)
        {
            var post = await _blogService.GetPostBySlugAsync(slug);

            if (post == null)
            {
                return NotFound();
            }

            // Increment view count
            await _blogService.IncrementViewCountAsync(post.Id);

            // Get related posts
            if (!string.IsNullOrEmpty(post.RelatedCity))
            {
                ViewBag.RelatedPosts = await _blogService.GetPostsByCityAsync(post.RelatedCity, 3);
            }
            else if (!string.IsNullOrEmpty(post.Category))
            {
                ViewBag.RelatedPosts = await _blogService.GetPostsByCategoryAsync(post.Category, 3);
            }

            return View(post);
        }

        [Route("blog/category/{category}")]
        public async Task<IActionResult> Category(string category, int page = 1)
        {
            var posts = await _blogService.GetPostsByCategoryAsync(category, (page - 1) * 10 + 10);
            ViewBag.Category = category;
            ViewBag.Page = page;

            return View("Index", posts.Skip((page - 1) * 10).Take(10).ToList());
        }

        [Route("blog/city/{city}")]
        public async Task<IActionResult> City(string city, int page = 1)
        {
            var posts = await _blogService.GetPostsByCityAsync(city, (page - 1) * 10 + 10);
            ViewBag.City = city;
            ViewBag.Page = page;

            return View("Index", posts.Skip((page - 1) * 10).Take(10).ToList());
        }
    }
}
