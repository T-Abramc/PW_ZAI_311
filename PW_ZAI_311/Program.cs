#region Usings
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PW_ZAI_311.configuration;
using Microsoft.OpenApi.Models;
using PW_ZAI_311;
using PW_ZAI_311.Entieties;
using PW_ZAI_311.Controllers.Domain.IOT.NewCommand.PW_ZAI_311.Application;
#endregion

var builder = WebApplication.CreateBuilder(args);

#region MongoDb
var connectionString = builder.Configuration.GetConnectionString("MongoDb");
var databaseName = builder.Configuration.GetValue<string>("DatabaseName");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(connectionString!, databaseName!));
#endregion

#region JWT
// Klucz dla HmacSha256 musi mieæ minimum 32 bajty (256 bitów)
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "TwojBardzoTajnyKluczKtoryMaMinimum32ZnakiDlugosci!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero // Token wygasa dok³adnie w momencie okreœlonym przez expiresIn
        };

        // Obs³uga tokena z bezpiecznego ciasteczka sesyjnego (tylko jeœli brak nag³ówka Authorization: Bearer)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // To zostaje tak jak mia³eœ - dzia³a œwietnie dla ciasteczek
                if (string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
                    context.Request.Cookies.TryGetValue("jwt_session", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Pobieramy czysty token string z obiektu SecurityToken
                string token = string.Empty;
                if (context.SecurityToken is System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt)
                {
                    token = jwt.RawData;
                }
                else if (context.SecurityToken is Microsoft.IdentityModel.JsonWebTokens.JsonWebToken jws)
                {
                    token = jws.EncodedToken;
                }

                // Sprawdzamy czysty token na czarnej liœcie
                if (TokenBlacklist.IsRevoked(token))
                {
                    context.Fail("Token has been logged out.");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
#endregion

#region Application Services (MediatR, Swagger, Controllers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpaFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Zezwala na dowolny adres
              .AllowAnyHeader()                   
              .AllowAnyMethod()                   
              .AllowCredentials();                
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    // 1. Definicja autoryzacji JWT (Authorization: Bearer <token>)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wklej poni¿ej swój wygenerowany token JWT (bez s³owa Bearer)."
    });

    // 2. Definicja autoryzacji dla czujników (X-API-Key)
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Wklej klucz API czujnika otrzymany przy rejestracji (POST /api/sensors)."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
#endregion

#region App Build
var app = builder.Build();

app.UseHttpsRedirection();

// Nag³ówki bezpieczeñstwa wymagane przez testy automatyczne w grupie A4 (w tym Content-Security-Policy)
app.Use(async (context, next) =>
{
    var db = context.RequestServices.GetService<AppDbContext>();
    if (db != null)
    {
        db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
    }
    context.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'self'; connect-src 'self' http://localhost:* https://localhost:*; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'"); context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseCors("AllowSpaFrontend");

app.UseExceptionHandler();
app.UseStatusCodePages();

// Authentication musi byæ PRZED Authorization
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
#endregion

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

    if (!await db.Users.AnyAsync())
    {
        db.Users.AddRange(
            new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("haslo"),
                Role = "Admin"
            },
            new User
            {
                Username = "superadmin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                Role = "SuperAdmin"
            }
        );
        await db.SaveChangesAsync();
    }
}
await app.RunAsync();