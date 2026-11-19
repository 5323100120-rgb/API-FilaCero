
using System.Text;
using FilaCero.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// 1. CONEXIÓN A POSTGRESQL
// =============================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta configurar ConnectionStrings:DefaultConnection.");
}

// =============================================
// 2. CONFIGURACIÓN JWT
// =============================================

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) ||
    Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Falta Jwt:Key o tiene menos de 32 bytes.");
}

// =============================================
// 3. REGISTRAR SERVICIOS
// =============================================

builder.Services.AddSingleton(
    NpgsqlDataSource.Create(connectionString)
);

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<HorarioService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// =============================================
// 4. CONFIGURACIÓN CORS
// PERMITIR CONEXIÓN DESDE LA PWA
// =============================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontLocal", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://127.0.0.1:3000",
            "http://localhost:4200",
            "https://localhost:4200",
            "http://localhost:5173",
            "https://localhost:5173"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// =============================================
// 5. AUTENTICACIÓN JWT
// =============================================

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,

                ValidIssuer =
                    builder.Configuration["Jwt:Issuer"],

                ValidAudience =
                    builder.Configuration["Jwt:Audience"],

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

// =============================================
// 6. AUTORIZACIÓN
// =============================================

builder.Services.AddAuthorization();

// =============================================
// 7. CREAR APLICACIÓN
// =============================================

var app = builder.Build();

// =============================================
// 8. SWAGGER
// =============================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// =============================================
// 9. MIDDLEWARE
// =============================================

app.UseHttpsRedirection();

app.UseCors("FrontLocal");

app.UseAuthentication();

app.UseAuthorization();

// =============================================
// 10. CONTROLADORES REST
// =============================================

app.MapControllers();

// =============================================
// 11. COMPROBAR ESTADO DE LA API
// =============================================

app.MapGet("/api/salud", () =>
{
    return Results.Ok(new
    {
        mensaje = "FilaCero API en funcionamiento",
        estado = "Activo",
        aplicacion = "FilaCero",
        version = "1.0"
    });
});

// =============================================
// 12. COMPROBAR POSTGRESQL
// =============================================

app.MapGet("/api/salud/bd",
    async (NpgsqlDataSource db) =>
    {
        await using var conexion =
            await db.OpenConnectionAsync();

        await using var cmd =
            new NpgsqlCommand(
                "SELECT current_database()",
                conexion
            );

        var nombre =
            (string?)await cmd.ExecuteScalarAsync();

        return Results.Ok(new
        {
            mensaje = "Conexión a PostgreSQL correcta",
            baseDatos = nombre
        });
    });

// =============================================
// 13. INICIAR API
// =============================================

app.Run();
