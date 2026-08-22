using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Models.ViewModels;

namespace StudyMate.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Student> _passwordHasher;

        public AccountController(AppDbContext db, IPasswordHasher<Student> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Matches");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var student = await _db.Students.FirstOrDefaultAsync(s => s.Email == email);

            // Verify even when no account matched, so a wrong email and a wrong password
            // take a comparable amount of time and cannot be told apart from the outside.
            var verification = student == null
                ? PasswordVerificationResult.Failed
                : _passwordHasher.VerifyHashedPassword(student, student.PasswordHash, model.Password);

            if (verification == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "That email and password do not match an account.");
                return View(model);
            }

            await SignInAsync(student);
            return RedirectToLocal(model.ReturnUrl);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Matches");
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var emailTaken = await _db.Students.AnyAsync(s => s.Email == email);
            if (emailTaken)
            {
                ModelState.AddModelError(nameof(model.Email), "An account already uses that email.");
                return View(model);
            }

            var student = new Student
            {
                Name = model.Name.Trim(),
                Email = email,
                Major = model.Major.Trim(),
                Year = model.Year,
                IsDemo = false
            };
            student.PasswordHash = _passwordHasher.HashPassword(student, model.Password);

            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            await SignInAsync(student);
            return RedirectToAction("Edit", "Profile");
        }

        /// <summary>
        /// Signs in as one of the seeded demo personas. Restricted to accounts flagged
        /// IsDemo so a registered account can never be entered through this route.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemoSignIn(int id)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == id && s.IsDemo);
            if (student == null)
            {
                return NotFound();
            }

            await SignInAsync(student);
            return RedirectToAction("Index", "Matches");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        private async Task SignInAsync(Student student)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, student.StudentId.ToString()),
                new(ClaimTypes.Name, student.Name),
                new(ClaimTypes.Email, student.Email)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true });
        }

        /// <summary>Only ever redirects within this application, so returnUrl cannot be used to bounce elsewhere.</summary>
        private IActionResult RedirectToLocal(string returnUrl) =>
            Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Matches");
    }
}
