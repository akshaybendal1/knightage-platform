using System.Text;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Infrastructure.Data;
using Knightage.Platform.Infrastructure.ExternalServices;
using Knightage.Platform.Infrastructure.Repositories;
using Knightage.Platform.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "wwwroot/browser"
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token issued by knightage-identity."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ITenantServiceDatabaseRepository, TenantServiceDatabaseRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<ITenantMigrationRepository, TenantMigrationRepository>();
builder.Services.AddScoped<ITenantDataService, TenantDataService>();
builder.Services.AddScoped<IProvisioningService, ProvisioningService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var crmBaseUrl = builder.Configuration["Services:CrmBaseUrl"]
    ?? throw new InvalidOperationException("Services:CrmBaseUrl is not configured.");
builder.Services.AddHttpClient("Crm", client =>
{
    client.BaseAddress = new Uri(crmBaseUrl);
    // Best-effort call during a migration, not something every request waits on -- fail
    // fast rather than let a slow/unreachable CRM stall the migration itself.
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<ICrmCacheInvalidator, CrmCacheInvalidator>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Angular client-side routes (e.g. /tenants) aren't real server routes -- fall back to
// index.html so the Angular router can handle them. Must come after MapControllers so API
// routes still resolve normally.
app.MapFallbackToFile("index.html");

app.Run();
