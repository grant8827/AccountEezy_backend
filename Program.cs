using System.Text;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager<SignInManager<AppUser>>()
    .AddDefaultTokenProviders();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
          ?? throw new InvalidOperationException("JWT configuration is missing.");

if (string.IsNullOrWhiteSpace(jwt.Key))
    throw new InvalidOperationException("JWT Key is not configured. Set the Jwt__Key environment variable.");
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
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(
            "http://localhost:4200",
            "https://hrbooks360.com",
            "https://www.hrbooks360.com",
            "https://hrbooks360frontend-production.up.railway.app",
            "https://accounteezyfrontend-production.up.railway.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

string? startupMigrationError = null;

// ── Apply pending migrations on every startup ─────────────────────────────
using (var startupScope = app.Services.CreateScope())
{
    try
    {
        var db = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        startupMigrationError = $"Migration Error: {ex.Message} | Inner: {ex.InnerException?.Message}";
    }
}

// ── Intercept all requests if migration fails to show the EXACT error ──────
if (startupMigrationError != null)
{
    var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http://localhost:4200",
        "https://hrbooks360.com",
        "https://www.hrbooks360.com",
        "https://hrbooks360frontend-production.up.railway.app",
        "https://accounteezyfrontend-production.up.railway.app"
    };

    app.Run(async context =>
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin) && allowedOrigins.Contains(origin))
        {
            context.Response.Headers.AccessControlAllowOrigin = origin;
            context.Response.Headers.AccessControlAllowCredentials = "true";
            context.Response.Headers.AccessControlAllowMethods = "GET,POST,PUT,PATCH,DELETE,OPTIONS";
            context.Response.Headers.AccessControlAllowHeaders = "authorization,content-type";
            context.Response.Headers.Vary = "Origin";
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "The database failed to migrate on startup. Please check the error below.",
            error = startupMigrationError
        });
    });
}

// Seed test data in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<AppUser>>();

    await SeedTestAccount(context, userManager);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            message = "An unexpected server error occurred.",
            traceId = context.TraceIdentifier
        });
    });
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "hrbooks360-api" }));

app.Run();

static async Task SeedTestAccount(AppDbContext context, UserManager<AppUser> userManager)
{
    const string testEmail = "test@hrbooks360.com";
    const string testPassword = "Test1234";

    // Check if test user already exists
    var existingUser = await userManager.FindByEmailAsync(testEmail);
    if (existingUser != null)
    {
        Console.WriteLine("✓ Test account already exists");
        return;
    }

    // Check if test business already exists
    var existingBusiness = await context.Businesses
        .FirstOrDefaultAsync(b => b.TRN == "TEST123456");

    Business testBusiness;
    if (existingBusiness != null)
    {
        testBusiness = existingBusiness;
    }
    else
    {
        // Create test business
        testBusiness = new Business
        {
            CompanyName = "Test Company Ltd",
            TRN = "TEST123456",
            Sector = "Technology"
        };
        context.Businesses.Add(testBusiness);
        await context.SaveChangesAsync();
    }

    // Create test user
    var testUser = new AppUser
    {
        UserName = testEmail,
        Email = testEmail,
        EmailConfirmed = true,
        BusinessId = testBusiness.Id,
        IsAdmin = true
    };

    var result = await userManager.CreateAsync(testUser, testPassword);

    if (result.Succeeded)
    {
        Console.WriteLine("✓ Test account created successfully!");
        Console.WriteLine($"  Email: {testEmail}");
        Console.WriteLine($"  Password: {testPassword}");
        Console.WriteLine($"  Company: {testBusiness.CompanyName}");
    }
    else
    {
        Console.WriteLine("✗ Failed to create test account:");
        foreach (var error in result.Errors)
        {
            Console.WriteLine($"  - {error.Description}");
        }
    }
}
