using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace BoardGameCafeFinder.Middleware
{
    /// <summary>
    /// Middleware to handle culture from URL prefix for SEO-friendly URLs.
    /// Supports URLs like /vi/cafe/123, /en/blog/post-slug, etc.
    /// Also detects language from browser Accept-Language header when no URL prefix.
    /// </summary>
    public class CultureMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CultureCookieName = "UserLanguagePreference";

        private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
        {
            "en", "vi", "ja", "ko", "zh", "th", "es", "de"
        };

        // Map country/language codes to supported cultures
        private static readonly Dictionary<string, string> LanguageMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            // Vietnamese
            { "vi", "vi" }, { "vi-VN", "vi" },
            // Japanese
            { "ja", "ja" }, { "ja-JP", "ja" },
            // Korean
            { "ko", "ko" }, { "ko-KR", "ko" },
            // Chinese (Simplified and Traditional)
            { "zh", "zh" }, { "zh-CN", "zh" }, { "zh-TW", "zh" }, { "zh-HK", "zh" },
            // Thai
            { "th", "th" }, { "th-TH", "th" },
            // Spanish
            { "es", "es" }, { "es-ES", "es" }, { "es-MX", "es" }, { "es-AR", "es" },
            // German
            { "de", "de" }, { "de-DE", "de" }, { "de-AT", "de" }, { "de-CH", "de" },
            // English variants
            { "en", "en" }, { "en-US", "en" }, { "en-GB", "en" }, { "en-AU", "en" }, { "en-CA", "en" }
        };

        public CultureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string culture;

            if (segments.Length > 0 && SupportedCultures.Contains(segments[0]))
            {
                // Priority 1: Culture prefix in URL
                culture = segments[0].ToLowerInvariant();

                // 301 redirect to strip the culture prefix from the URL.
                // Attribute-routed controllers (cafe, blog, etc.) don't include the culture
                // prefix so these URLs return 404. Redirect to the English canonical URL and
                // save the language preference in a cookie instead.
                var remainingPath = "/" + string.Join("/", segments.Skip(1).Select(s => s.ToLowerInvariant()));
                var queryString = context.Request.QueryString.Value ?? "";
                var redirectUrl = remainingPath + queryString;

                // Save language preference in cookie (non-English only)
                if (culture != "en")
                {
                    context.Response.Cookies.Append(CultureCookieName, culture, new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax
                    });
                }

                context.Response.StatusCode = 301;
                context.Response.Headers["Location"] = redirectUrl;
                return;
            }
            else
            {
                // No culture prefix in URL - detect from cookie or browser
                culture = DetectCultureFromRequest(context);
            }

            var cultureInfo = new CultureInfo(culture);

            // Set culture for this request
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            // Also set via feature for consistency
            context.Features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(new RequestCulture(cultureInfo), null));

            // Store culture in items for views to access
            context.Items["Culture"] = culture;
            context.Items["CultureInfo"] = cultureInfo;

            await _next(context);
        }

        /// <summary>
        /// Detect culture from cookie or Accept-Language header
        /// </summary>
        private string DetectCultureFromRequest(HttpContext context)
        {
            // Priority 2: Check for saved language preference cookie
            if (context.Request.Cookies.TryGetValue(CultureCookieName, out var cookieCulture)
                && !string.IsNullOrEmpty(cookieCulture)
                && SupportedCultures.Contains(cookieCulture))
            {
                return cookieCulture.ToLowerInvariant();
            }

            // Priority 3: Detect from Accept-Language header
            var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();
            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                // Parse Accept-Language header (e.g., "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7")
                var languages = acceptLanguage
                    .Split(',')
                    .Select(lang =>
                    {
                        var parts = lang.Trim().Split(';');
                        var langCode = parts[0].Trim();
                        var quality = 1.0;
                        if (parts.Length > 1 && parts[1].StartsWith("q="))
                        {
                            double.TryParse(parts[1].Substring(2), out quality);
                        }
                        return (Language: langCode, Quality: quality);
                    })
                    .OrderByDescending(x => x.Quality)
                    .Select(x => x.Language)
                    .ToList();

                // Try to find a supported culture
                foreach (var lang in languages)
                {
                    // Try exact match first (e.g., "vi-VN")
                    if (LanguageMapping.TryGetValue(lang, out var mappedCulture))
                    {
                        return mappedCulture;
                    }

                    // Try base language (e.g., "vi" from "vi-VN")
                    var baseLang = lang.Split('-')[0];
                    if (LanguageMapping.TryGetValue(baseLang, out mappedCulture))
                    {
                        return mappedCulture;
                    }
                }
            }

            // Priority 4: Default to English
            return "en";
        }
    }

    public static class CultureMiddlewareExtensions
    {
        public static IApplicationBuilder UseCultureFromUrl(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CultureMiddleware>();
        }
    }
}
