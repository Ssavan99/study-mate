using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Models.ViewModels;
using StudyMate.Services;

namespace StudyMate.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<Student> _passwordHasher;

        // Fixed decoy so a login attempt for an unknown email still performs a real
        // hash verification. Computed once; the value it encodes is never a valid login.
        private static readonly Student TimingDecoy = new() { StudentId = 0, Email = "decoy@invalid" };
        private static readonly string TimingDecoyHash =
            new PasswordHasher<Student>().HashPassword(TimingDecoy, Guid.NewGuid().ToString());

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
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var student = await _db.Students.FirstOrDefaultAsync(s => s.Email == email);

            PasswordVerificationResult verification;
            if (student == null)
            {
                // Hash against a throwaway account so an unknown email costs the same
                // work as a known one. A short-circuit here would answer in about a
                // millisecond and let an unknown address be told apart by timing alone.
                _passwordHasher.VerifyHashedPassword(TimingDecoy, TimingDecoyHash, model.Password);
                verification = PasswordVerificationResult.Failed;
            }
            else
            {
                // SuccessRehashNeeded also signs in; it cannot occur with the current
                // hasher settings, but treating it as a failure would lock people out
                // the moment those settings change.
                verification = _passwordHasher.VerifyHashedPassword(student, student.PasswordHash, model.Password);
            }

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
                UniversityId = null,
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

        /// <summary>
        /// Starts an external sign-in. The provider handler completes at its own callback
        /// path, creates or finds the matching student, and signs into the same cookie the
        /// local login uses — so the rest of the app cannot tell the two apart.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(nameof(Login));
            }

            // A brand-new GitHub-linked account starts with no university set, and
            // University hard-filters matching — left blank, the account would never
            // see or appear in anyone's deck. Route straight to the profile instead of
            // dropping a silently broken account onto the matches page.
            var studentId = User.GetStudentId();
            var universityId = studentId == null
                ? (int?)null
                : await _db.Students
                    .Where(s => s.StudentId == studentId.Value)
                    .Select(s => s.UniversityId)
                    .FirstOrDefaultAsync();

            if (!universityId.HasValue)
            {
                return RedirectToAction("Edit", "Profile");
            }

            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        [HttpGet]
        public IActionResult ExternalLoginFailed()
        {
            TempData["ExternalLoginError"] = "We could not verify this account as belonging to a recognised university. Try a university account or use a demo profile.";
            return RedirectToAction(nameof(Login));
        }

        private async Task SignInAsync(Student student)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, student.StudentId.ToString()),
                new(ClaimTypes.Name, student.Name),
                new(ClaimTypes.Email, student.Email)
            };
            if (student.IsAdmin) claims.Add(new Claim("is_admin", "true"));

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
