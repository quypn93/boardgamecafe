using Microsoft.AspNetCore.Mvc;
using BoardGameCafeFinder.Services;

namespace BoardGameCafeFinder.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogService _blogService;

        public BlogController(IBlogService blogService)
        {
            _blogService = blogService;
        }

        /// <summary>
        /// Blog listing page
        /// </summary>
        public async Task<IActionResult> Index(string? category = null, string? city = null, int page = 1)
        {
            const int pageSize = 9;

            List<Models.Domain.BlogPost> posts;

            if (!string.IsNullOrEmpty(category))
            {
                posts = await _blogService.GetPostsByCategoryAsync(category, page, pageSize);
                ViewBag.FilterType = "Category";
                ViewBag.FilterValue = category;
            }
            else if (!string.IsNullOrEmpty(city))
            {
                posts = await _blogService.GetPostsByCityAsync(city, page, pageSize);
                ViewBag.FilterType = "City";
                ViewBag.FilterValue = city;
            }
            else
            {
                posts = await _blogService.GetPublishedPostsAsync(page, pageSize);
            }

            var totalPosts = await _blogService.GetTotalPublishedCountAsync();
            var totalPages = (int)Math.Ceiling((double)totalPosts / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = await _blogService.GetAllCategoriesAsync();
            ViewBag.Cities = await _blogService.GetAllCitiesWithPostsAsync();

            return View(posts);
        }

        /// <summary>
        /// Single blog post page
        /// </summary>
        [Route("blog/{slug}")]
        public async Task<IActionResult> Post(string slug)
        {
            var post = await _blogService.GetPostBySlugAsync(slug);

            if (post == null || !post.IsPublished)
            {
                return NotFound();
            }

            // Increment view count
            await _blogService.IncrementViewCountAsync(post.Id);

            // Get related posts (same category or city)
            var relatedPosts = new List<Models.Domain.BlogPost>();
            if (!string.IsNullOrEmpty(post.Category))
            {
                relatedPosts = await _blogService.GetPostsByCategoryAsync(post.Category, 1, 4);
                relatedPosts = relatedPosts.Where(p => p.Id != post.Id).Take(3).ToList();
            }
            else if (!string.IsNullOrEmpty(post.RelatedCity))
            {
                relatedPosts = await _blogService.GetPostsByCityAsync(post.RelatedCity, 1, 4);
                relatedPosts = relatedPosts.Where(p => p.Id != post.Id).Take(3).ToList();
            }

            ViewBag.RelatedPosts = relatedPosts;

            return View(post);
        }
    }
}
