using Data;
using Data.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --------------------------
// Logging
// --------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --------------------------
// Controllers + JSON (Enums as strings)
// --------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --------------------------
// Swagger
// --------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

    // JWT Bearer في Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your token.\nExample: \"Bearer eyJhbGciOiJI...\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // عرض Enum RoleKey كقيم نصية واضحة (Dropdown)
    c.MapType<Domain.DTOs.RoleKey>(() => new OpenApiSchema
    {
        Type = "string",
        Enum = new List<IOpenApiAny>
        {
            new OpenApiString("Admin"),
            new OpenApiString("Employee"),
            new OpenApiString("Member")
        }
    });
});

// --------------------------
// DbContext
// --------------------------
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --------------------------
// JWT Service
// --------------------------
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var secret = config["Jwt:Secret"]!;
    var issuer = config["Jwt:Issuer"]!;
    var audience = config["Jwt:Audience"]!;
    return new JwtService(secret, issuer, audience);
});

// --------------------------
// Application Services
// --------------------------
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<BlacklistService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<BookService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<BorrowRecordService>();
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaFilter<BookCreateExampleSchemaFilter>();
});

// --------------------------
// CORS
// --------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// --------------------------
// JWT Authentication
// --------------------------
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var key = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;                       // مفيد لو حابب تحتاج التوكن لاحقًا
    options.RequireHttpsMetadata = false;           // خليها true في الإنتاج خلف HTTPS
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),

        // مهم لمواءمة الـ claims مع ما نُصدره في JwtService.GenerateToken
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role,

        ClockSkew = TimeSpan.Zero // لا تمدّد صلاحية التوكن تلقائيًا
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            // تحقق من الـ blacklist
            var blacklistService = context.HttpContext.RequestServices.GetRequiredService<BlacklistService>();
            var authHeader = context.Request.Headers.Authorization.ToString();
            var accessToken = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(accessToken))
            {
                var isRevoked = await blacklistService.IsTokenRevokedAsync(accessToken);
                if (isRevoked)
                    context.Fail("This token has been revoked.");
            }
        },
        OnChallenge = async context =>
        {
            // نكتب JSON موحّد للـ 401
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Unauthorized: Please login first." });
        },
        OnForbidden = async context =>
        {
            // نكتب JSON موحّد للـ 403
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Forbidden: You don’t have permission." });
        }
    };
});

// --------------------------
// Build app
// --------------------------
var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Global Exception Handler (اختياري)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "An unexpected error occurred",
            details = ex.Message
        });
    }
});

app.MapControllers();

// --------------------------
// Ensure DB created / Seed Admin
// --------------------------
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<BankDbContext>();
    var userService = scope.ServiceProvider.GetRequiredService<UserService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        ctx.Database.Migrate();

        if (!ctx.Users.Any(u => u.RoleId == 1))
        {
            await userService.AddUserAsync("admin", "admin@example.com", "123456", 1);
            logger.LogInformation("Admin account created: admin@example.com / 123456");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DB init error");
    }
}

app.Run();
