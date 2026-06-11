using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SampleShop.Auth;
using SampleShop.Data;
using SampleShop.Services;

// Application entry point: builds the host, registers services into the
// dependency-injection container, and configures the HTTP request pipeline.
var builder = WebApplication.CreateBuilder(args);

// Register the EF Core database context.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Dependency injection registrations for application services.
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddControllers();

var app = builder.Build();

// Configure the middleware request pipeline (order matters here).
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
