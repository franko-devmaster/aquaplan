using System.Text;
using System.Text.Json.Serialization;
using AquaPlan.Api.Middleware;
using AquaPlan.Application.Extensions;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Data.Seeds;
using AquaPlan.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/aquaplan-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
});

// Infrastructure + Application services
builder.Services.WithInfrastructure(builder.Configuration);
builder.Services.WithApplication();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? "AquaPlan-Dev-Secret-Key-Min-32-Chars!!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
    };
});

// OIDC authentication scheme (gated by config)
var oidcEnabled = builder.Configuration.GetValue<bool>("Oidc:Enabled");
if (oidcEnabled)
{
    var oidcSettings = builder.Configuration.GetSection("Oidc");
    builder.Services.AddAuthentication()
        .AddOpenIdConnect("oidc", options =>
        {
            options.Authority = oidcSettings["Authority"];
            options.ClientId = oidcSettings["ClientId"];
            options.ClientSecret = oidcSettings["ClientSecret"];
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.CallbackPath = oidcSettings["CallbackPath"] ?? "/api/auth/oidc-callback";
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
        });
}

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// NSwag / OpenAPI
builder.Services.AddOpenApiDocument(configure =>
{
    configure.Title = "AquaPlan API";
    configure.Version = "v1";
    configure.Description = "AquaPlan Water Quality Management API";
});

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("AquaPlan.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<BusinessExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply pending EF Core migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
    await db.Database.MigrateAsync();
}

// Seed roles, permissions, and default admin user
await RoleAndPermissionSeeder.SeedAsync(app.Services);

// Seed analysis catalog (containers)
await AnalysisCatalogSeeder.SeedAsync(app.Services);

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
