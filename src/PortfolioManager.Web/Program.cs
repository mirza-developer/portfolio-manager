using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using PortfolioManager.Data;
using PortfolioManager.Data.Identity;
using PortfolioManager.Web.Components;
using PortfolioManager.Web.Security;
using PortfolioManager.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Database, repositories and Identity users/roles (SQL Server or SQLite, chosen by configuration).
builder.Services.AddDataLayer(builder.Configuration);
builder.Services.AddIdentityCore<ApplicationUser>().AddSignInManager();

// Authentication (cookie) and authorization (app level fallback + role level policies).
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/account/login";
    o.AccessDeniedPath = "/access-denied";
});
builder.Services.AddAuthorization(AppPolicies.Configure);

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<ILogQueryService, LogQueryService>();
builder.Services.AddScoped<IAssetAdminService, AssetAdminService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ConfirmService>();

var app = builder.Build();

// Migrates (when Database:AutoMigrate is true) and seeds roles, the admin user and the default assets.
await app.Services.InitializeDatabaseAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/account/login", async (
        [FromForm] LoginForm form,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> users) =>
    {
        var user = await users.FindByEmailAsync(form.Email ?? string.Empty);
        if (user is null || !user.IsActive)
            return Results.Redirect("/account/login?error=1");

        var result = await signInManager.PasswordSignInAsync(user, form.Password ?? string.Empty, form.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Results.Redirect("/account/login?error=1");

        var target = !string.IsNullOrEmpty(form.ReturnUrl) && form.ReturnUrl.StartsWith('/') && !form.ReturnUrl.StartsWith("//") ? form.ReturnUrl : "/";
        return Results.LocalRedirect(target);
    }).AllowAnonymous();

app.MapGet("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/account/login");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public sealed class LoginForm
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

// Makes Program visible to the test project.
public partial class Program;
