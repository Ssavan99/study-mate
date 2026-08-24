using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Models;
using StudyMate.Services;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Container hosts assign the port at runtime through PORT rather than ASPNETCORE_URLS.
// Binding to 0.0.0.0 is required for the host's proxy to reach the container at all.
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllersWithViews();

// A relative SQLite path is resolved against the process working directory, which differs
// between `dotnet run` from the repo root and from the project folder — quietly producing
// two different databases. Anchor it to the content root so the location is predictable,
// while still allowing an absolute path from configuration to win (used in the container).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connectionBuilder = new SqliteConnectionStringBuilder(connectionString);
if (!string.IsNullOrWhiteSpace(connectionBuilder.DataSource) && !Path.IsPathRooted(connectionBuilder.DataSource))
{
    connectionBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, connectionBuilder.DataSource);
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionBuilder.ConnectionString));

builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IPasswordHasher<Student>, PasswordHasher<Student>>();

var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // The app only ever sees plain HTTP behind Render's proxy, so the secure flag
        // is set from configuration rather than inferred from the request scheme.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        // The hosting filesystem is ephemeral: the database is rebuilt whenever the
        // service restarts, which leaves previously issued cookies pointing at student
        // rows that no longer exist. Without this the next request would fail on a
        // missing record, so the principal is checked against the database and a stale
        // session is signed out cleanly instead.
        options.Events.OnValidatePrincipal = async context =>
        {
            var studentId = context.Principal.GetStudentId();
            if (studentId == null)
            {
                // Sign out as well as rejecting: rejecting alone leaves the cookie in the
                // browser, so it is re-sent and re-rejected on every subsequent request.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var stillExists = await db.Students.AsNoTracking().AnyAsync(s => s.StudentId == studentId.Value);

            if (!stillExists)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// "Sign in with GitHub" is optional and only wired up when both settings are present,
// so the app runs unchanged without them. The secret is supplied by the host as
// Authentication__GitHub__ClientSecret and is never committed to the repository.
var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
var githubEnabled = !string.IsNullOrWhiteSpace(githubClientId) && !string.IsNullOrWhiteSpace(githubClientSecret);

if (githubEnabled)
{
    authentication.AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.CallbackPath = "/signin-github";

        // GitHub only returns an address when this scope is granted, and the account
        // is keyed on email so the same person lands on the same student record.
        options.Scope.Add("user:email");

        options.Events.OnCreatingTicket = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var hasher = context.HttpContext.RequestServices.GetRequiredService<IPasswordHasher<Student>>();

            var login = context.Identity.FindFirst(ClaimTypes.Name)?.Value ?? "github-user";
            var name = context.Identity.FindFirst("urn:github:name")?.Value ?? login;

            // Accounts with no public address still need a stable unique key.
            var email = (context.Identity.FindFirst(ClaimTypes.Email)?.Value
                         ?? $"{login}@users.noreply.github.com").ToLowerInvariant();

            var student = await db.Students.FirstOrDefaultAsync(s => s.Email == email);
            if (student == null)
            {
                student = new Student
                {
                    Name = name,
                    Email = email,
                    Major = string.Empty,
                    University = string.Empty,
                    Year = 1,
                    IsDemo = false
                };
                student.PasswordHash = hasher.HashPassword(student, Guid.NewGuid().ToString());

                db.Students.Add(student);
                await db.SaveChangesAsync();
            }

            // The identity arrives carrying GitHub's own user id. It has to be replaced
            // with the local student id, because everything downstream reads
            // NameIdentifier as a StudyMate primary key.
            foreach (var stale in context.Identity.FindAll(ClaimTypes.NameIdentifier).ToList())
            {
                context.Identity.RemoveClaim(stale);
            }

            context.Identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, student.StudentId.ToString()));
        };
    });
}

builder.Services.AddSingleton(new AuthenticationOptionsView(githubEnabled));

// Repeated password guesses against a known address are otherwise unlimited.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5)
        }));
});

// Render terminates TLS at its load balancer and forwards plain HTTP to the container.
// Without this the app builds http:// URLs for redirects, which breaks OAuth callbacks.
// Render's proxy address is not known ahead of time, so the known-proxy lists are cleared.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// The hosting filesystem is ephemeral, so the schema is applied and the demonstration
// data rebuilt on every start. Seeding is a no-op if students already exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Student>>();

    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db, hasher);
}

app.Run();
