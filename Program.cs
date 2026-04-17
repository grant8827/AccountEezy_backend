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

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(
            "http://localhost:4200",
            "https://accounteezyfrontend-production.up.railway.app")
            .AllowAnyHeader()
            .AllowAnyMethod());
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

// ── Apply pending migrations on every startup ─────────────────────────────
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
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

app.UseCors("frontend");
app.UseStaticFiles();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "account-eezy-api" }));

app.Run();

static async Task SeedTestAccount(AppDbContext context, UserManager<AppUser> userManager)
{
    const string testEmail = "test@accounteezy.com";
    const string testPassword = "Test1234";

    // Check if test user already exists
    var existingUser = await userManager.FindByEmailAsync(testEmail);
    if (existingUser != null)
    {
        Console.WriteLine("✓ Test account already exists");
        return;
    }

    // Create test business
    var testBusiness = new Business
    {
        CompanyName = "Test Company Ltd",
        TRN = "TEST123456",
        Sector = "Technology"
    };

    context.Businesses.Add(testBusiness);
    await context.SaveChangesAsync();

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
