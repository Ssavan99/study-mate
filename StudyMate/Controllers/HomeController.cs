using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;

namespace StudyMate.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;

        public HomeController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Matches");
            }

            var personas = await _db.Students
                .AsNoTracking()
                .Where(s => s.IsDemo)
                .Include(s => s.Enrollments)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View(personas);
        }

        public IActionResult Privacy() => View();

        /// <summary>
        /// Re-executed by UseStatusCodePagesWithReExecute (see Program.cs) whenever a
        /// request ends in a bare status code with no body — a 404 from routing, or a
        /// NotFound() from a controller — so it gets the same designed page as any
        /// other error instead of the host's blank default response.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCode(int code)
        {
            Response.StatusCode = code;

            if (code == 404)
            {
                return View("NotFound");
            }

            return View("Error", new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
