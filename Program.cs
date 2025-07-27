using AutoCAC;
using AutoCAC.Components;
using AutoCAC.Services;
using AutoCAC.Utilities;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
      .AddInteractiveServerComponents().AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
    options.Name = "AutoCACTheme";
    options.Duration = TimeSpan.FromDays(365);
});
builder.Services.AddHttpClient();

builder.Services.AddHttpClient("WindowsAuthClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseDefaultCredentials = true
    });


builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddScoped<RPMSService>();
builder.Services.AddScoped<FtpUploadService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connString = builder.Configuration.GetConnectionString("mainConnection");
builder.Services.AddDbContextFactory<AutoCAC.Models.mainContext>(options =>
{
    options.UseSqlServer(connString);
});
builder.Services.AddScoped<LoadDataGridService>();
builder.Services.AddScoped<UserContextService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapControllers();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.MapPost("/export-excel", async (HttpContext http, [FromBody] List<Dictionary<string, object>> data) =>
{
    if (data == null || data.Count == 0)
    {
        http.Response.StatusCode = 400;
        await http.Response.WriteAsync("No data received.");
        return;
    }

    var fileBytes = ExcelExporter.ExportDynamicToExcel(data);

    http.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    http.Response.Headers.ContentDisposition = "attachment; filename=\"AutoCAC_Export.xlsx\"";

    await http.Response.Body.WriteAsync(fileBytes);
});

SqlDependency.Start(connString);

app.Lifetime.ApplicationStopping.Register(() =>
{
    SqlDependency.Stop(connString);
});


app.Run();