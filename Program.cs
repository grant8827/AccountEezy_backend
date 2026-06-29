using System.Text;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

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
builder.Services.AddHostedService<SubscriptionStatusService>();
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
            ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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
        await EnsureSubscriptionColumns(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while migrating the database: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        startupMigrationError = $"Migration Error: {ex.Message} | Inner: {ex.InnerException?.Message}";
    }

    if (startupMigrationError is null)
    {
        try
        {
            var userManager = startupScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await EnsureConfiguredSuperAdmin(builder.Configuration, userManager);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while configuring the super admin: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
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

static async Task EnsureSubscriptionColumns(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "IsOnLeave" boolean NOT NULL DEFAULT false;

        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "BillingPeriod" character varying(20);
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "PaymentCompletedAt" timestamp with time zone;
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "PaymentStatus" text NOT NULL DEFAULT 'Unpaid';
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "SelectedPlan" character varying(80);
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "StripeCustomerId" character varying(120);
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "StripeSubscriptionId" character varying(120);
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "SubscriptionStatus" text NOT NULL DEFAULT 'Incomplete';
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "SubscriptionStartedAt" timestamp with time zone;
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "NextPaymentDueAt" timestamp with time zone;
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "GracePeriodEndsAt" timestamp with time zone;
        ALTER TABLE "Businesses" ADD COLUMN IF NOT EXISTS "LastPaymentMethod" character varying(40);

        UPDATE "Businesses" SET "PaymentStatus" = 'Unpaid' WHERE "PaymentStatus" IS NULL OR "PaymentStatus" = '';
        UPDATE "Businesses" SET "SubscriptionStatus" = 'Incomplete' WHERE "SubscriptionStatus" IS NULL OR "SubscriptionStatus" = '';
        UPDATE "Businesses" SET "SubscriptionStatus" = 'Active' WHERE lower("SubscriptionStatus") IN ('complete', 'completed', 'active', 'trialing', 'paid');
        UPDATE "Businesses" SET "SubscriptionStatus" = 'Canceled' WHERE lower("SubscriptionStatus") IN ('cancelled', 'canceled');
        UPDATE "Businesses" SET "SubscriptionStatus" = 'Unpaid' WHERE lower("SubscriptionStatus") = 'unpaid';
        UPDATE "Businesses" SET "SubscriptionStatus" = 'PastDue' WHERE lower("SubscriptionStatus") IN ('past_due', 'pastdue', 'paymentfailed', 'payment_failed');
        UPDATE "Businesses" SET "SubscriptionStatus" = 'Incomplete' WHERE "SubscriptionStatus" NOT IN ('Incomplete', 'Active', 'Canceled', 'Unpaid', 'PastDue');

        UPDATE "Businesses" SET "PaymentStatus" = 'Paid' WHERE lower("PaymentStatus") IN ('paid', 'complete', 'completed', 'active');
        UPDATE "Businesses" SET "PaymentStatus" = 'PaymentFailed' WHERE lower("PaymentStatus") IN ('paymentfailed', 'payment_failed', 'failed');
        UPDATE "Businesses" SET "PaymentStatus" = 'Unpaid' WHERE "PaymentStatus" NOT IN ('Unpaid', 'Paid', 'PaymentFailed');

        -- Ensure SubscriptionPackages table and all columns exist regardless of migration state
        CREATE TABLE IF NOT EXISTS "SubscriptionPackages" (
            "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
            "Key" character varying(40) NOT NULL,
            "Name" character varying(80) NOT NULL,
            "MonthlyPriceJmd" bigint NOT NULL DEFAULT 0,
            "DisplayOrder" integer NOT NULL DEFAULT 0,
            "IsCustom" boolean NOT NULL DEFAULT false,
            "DiscountEnabled" boolean NOT NULL DEFAULT false,
            "DiscountPercent" numeric NOT NULL DEFAULT 0,
            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now()
        );
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "YearlyPriceJmd" bigint NULL;
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "MonthlySaleEnabled" boolean NOT NULL DEFAULT false;
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "MonthlySalePriceJmd" bigint NULL;
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "YearlySaleEnabled" boolean NOT NULL DEFAULT false;
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "YearlySalePriceJmd" bigint NULL;
        ALTER TABLE "SubscriptionPackages" ADD COLUMN IF NOT EXISTS "FreeTrialDays" integer NOT NULL DEFAULT 14;
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionPackages_Key" ON "SubscriptionPackages" ("Key");
        INSERT INTO "SubscriptionPackages" ("Key", "Name", "MonthlyPriceJmd", "DisplayOrder", "IsCustom", "DiscountEnabled", "DiscountPercent", "UpdatedAt")
        VALUES ('lite', 'Lite', 3500, 1, false, false, 0, now()),
               ('starter', 'Starter', 6500, 2, false, false, 0, now()),
               ('growth', 'Growth', 12500, 3, false, false, 0, now()),
               ('custom', 'Custom', 15000, 4, true, false, 0, now())
        ON CONFLICT ("Key") DO NOTHING;
        """);
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

static async Task EnsureConfiguredSuperAdmin(IConfiguration configuration, UserManager<AppUser> userManager)
{
    var email = configuration["SuperAdmin:Email"]?.Trim();
    if (string.IsNullOrWhiteSpace(email))
    {
        return;
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user is null)
    {
        var password = configuration["SuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine($"Super admin '{email}' was not found. Set SuperAdmin__Password once to create it.");
            return;
        }

        user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsAdmin = true,
            IsSuperAdmin = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create configured super admin '{email}': {errors}");
        }

        Console.WriteLine($"Configured super admin '{email}' was created.");
        return;
    }

    var changed = false;
    if (!user.IsSuperAdmin)
    {
        user.IsSuperAdmin = true;
        changed = true;
    }
    if (!user.IsAdmin)
    {
        user.IsAdmin = true;
        changed = true;
    }
    if (!user.EmailConfirmed)
    {
        user.EmailConfirmed = true;
        changed = true;
    }

    if (!changed)
    {
        return;
    }

    var updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
        var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
        throw new InvalidOperationException($"Failed to update configured super admin '{email}': {errors}");
    }

    Console.WriteLine($"Configured super admin '{email}' was restored.");
}
