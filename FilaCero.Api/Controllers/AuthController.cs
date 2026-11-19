using System.Security.Claims;
using FilaCero.Api.Models;
using FilaCero.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace FilaCero.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(NpgsqlDataSource dataSource, TokenService tokens) : ControllerBase
{
    [HttpPost("registro")]
    [AllowAnonymous]
    public async Task<IActionResult> Registrar([FromBody] RegistroRequest input)
    {
        var correo = input.Correo.Trim().ToLowerInvariant();
        var nombre = input.NombreCompleto.Trim();
        if (nombre.Length < 3) return BadRequest(new { mensaje = "Nombre inválido." });
        var hash = BCrypt.Net.BCrypt.HashPassword(input.Contrasena);
        try
        {
            await using var conexion = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO usuarios(nombre_completo, correo, telefono, contrasena_hash, rol_id)
                SELECT @nombre, @correo, @telefono, @hash, r.id
                FROM roles r WHERE r.nombre = 'usuario'
                RETURNING id
                """, conexion);
            cmd.Parameters.AddWithValue("nombre", nombre);
            cmd.Parameters.AddWithValue("correo", correo);
            cmd.Parameters.Add("telefono", NpgsqlDbType.Varchar).Value = (object?)input.Telefono ?? DBNull.Value;
            cmd.Parameters.AddWithValue("hash", hash);
            var resultado = await cmd.ExecuteScalarAsync();
            if (resultado is not Guid id)
                return Problem("Falta el rol 'usuario' en la base de datos. Ejecuta el script SQL.");
            return Created("/api/auth/yo", new UsuarioPublico(id, nombre, correo, "usuario"));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { mensaje = "El correo ya está registrado." });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest input)
    {
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT u.id, u.nombre_completo, u.correo, u.contrasena_hash, r.nombre
            FROM usuarios u JOIN roles r ON r.id = u.rol_id
            WHERE lower(u.correo) = @correo AND u.activo = TRUE
            """, conexion);
        cmd.Parameters.AddWithValue("correo", input.Correo.Trim().ToLowerInvariant());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return Unauthorized(new { mensaje = "Credenciales inválidas." });

        var id = r.GetGuid(0);
        var nombre = r.GetString(1);
        var correo = r.GetString(2);
        var hash = r.GetString(3);
        var rol = r.GetString(4);
        if (!BCrypt.Net.BCrypt.Verify(input.Contrasena, hash))
            return Unauthorized(new { mensaje = "Credenciales inválidas." });
        return Ok(tokens.CrearToken(new UsuarioPublico(id, nombre, correo, rol)));
    }

    [HttpGet("yo")]
    [Authorize]
    public IActionResult Yo() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        nombre = User.FindFirstValue(ClaimTypes.Name),
        correo = User.FindFirstValue(ClaimTypes.Email),
        rol = User.FindFirstValue(ClaimTypes.Role)
    });
}
