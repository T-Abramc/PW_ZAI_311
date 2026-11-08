#region Usings
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver.Core.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using PW_ZAI_311.configuration;
using Microsoft.OpenApi.Models;
using PW_ZAI_311;

#endregion

var builder = WebApplication.CreateBuilder(args);


#region MongoDb
var connectionString = builder.Configuration.GetConnectionString("MongoDb");
var databaseName = builder.Configuration.GetValue<string>("DatabaseName");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMongoDB(connectionString, databaseName));
#endregion

#region JWT
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "TwojBardzoTajnyKluczKtoryMaMinimum16Znakow!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();
#endregion

#region Application Services (MediatR, Swagger, Controllers)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
#endregion
#region Application Services (MediatR, Swagger, Controllers)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    // 1. Definicja autoryzacji (pojawia siê przycisk Authorize)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wklej poni¿ej swój wygenerowany token JWT (bez s³owa Bearer)."
    });

    // 2. Wymaganie tokena dla chronionych endpointów (pojawiaj¹ siê k³ódki przy endpointach)
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
            Array.Empty<string>() // Brak specyficznych ról na poziomie definicji Swaggera, obs³uguje to sam kod
        }
    });
});

builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});
#endregion
#region App Build
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Authentication musi byæ PRZED Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
#endregion

#region Authentication Endpoints (Minimal API)
app.MapPost("/login", (LoginDto request) =>
{
    if (request.Password != "123456") return Results.Unauthorized();

    string role = request.Username switch
    {
        "admin" => "Admin",
        "superadmin" => "SuperAdmin",
        _ => null
    };

    if (role == null) return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, role)
        }),
        Expires = DateTime.UtcNow.AddHours(2),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };

    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Results.Ok(new { Token = tokenHandler.WriteToken(token) });
});

// Zabezpieczone endpointy testowe Minimal API
app.MapGet("/only-admin", () => "Witaj Adminie!")
   .RequireAuthorization(policy => policy.RequireRole("Admin", "SuperAdmin"));

app.MapGet("/only-superadmin", () => "Witaj Super Adminie!")
   .RequireAuthorization(policy => policy.RequireRole("SuperAdmin"));
#endregion


app.Run();


