using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StudyMate.Data;
using StudyMate.Services;

var builder = WebApplication.CreateBuilder(args);

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// The hosting filesystem is ephemeral, so the schema is applied on every start.
// Seed data is added in a later phase; until then the database starts empty.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
