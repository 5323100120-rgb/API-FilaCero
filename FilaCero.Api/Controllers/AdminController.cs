using FilaCero.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace FilaCero.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin")]
public sealed class AdminController(NpgsqlDataSource dataSource) : ControllerBase
{
    // El administrador es de SOLO LECTURA para citas y triajes.
    [HttpGet("citas")]
    public async Task<IActionResult> ListarCitas(
        [FromQuery] DateOnly? fecha = null, [FromQuery] string? sucursalId = null,
        [FromQuery] string? estado = null)
    {
        var lista = new List<CitaAdminDto>();
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT c.id, u.nombre_completo, u.correo, s.nombre,
                   c.fecha, c.hora, c.estado_registro, (t.cita_id IS NOT NULL)
            FROM citas c
            JOIN usuarios u ON u.id = c.usuario_id
            JOIN sucursales s ON s.id = c.sucursal_id
            LEFT JOIN triajes t ON t.cita_id = c.id
            WHERE (@fecha::date IS NULL OR c.fecha = @fecha)
              AND (@sucursal::text IS NULL OR c.sucursal_id = @sucursal)
              AND (@estado::text IS NULL OR c.estado_registro = @estado)
            ORDER BY c.fecha DESC, c.hora DESC
            LIMIT 200
            """, conexion);
        cmd.Parameters.Add("fecha", NpgsqlDbType.Date).Value = (object?)fecha ?? DBNull.Value;
        cmd.Parameters.Add("sucursal", NpgsqlDbType.Text).Value = (object?)sucursalId ?? DBNull.Value;
        cmd.Parameters.Add("estado", NpgsqlDbType.Text).Value = (object?)estado ?? DBNull.Value;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new CitaAdminDto
            {
                Id = r.GetGuid(0), Paciente = r.GetString(1), Correo = r.GetString(2),
                Sucursal = r.GetString(3), Fecha = r.GetFieldValue<DateOnly>(4),
                Hora = r.GetFieldValue<TimeOnly>(5), Estado = r.GetString(6),
                TieneTriaje = r.GetBoolean(7)
            });
        return Ok(lista);
    }

    [HttpGet("citas/{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT c.id, u.nombre_completo, u.correo, u.telefono,
                   s.nombre, s.ubicacion, c.fecha, c.hora, c.estado_registro,
                   t.dolor_pecho, t.dificultad_respiratoria, t.inconsciente,
                   t.fiebre, t.dolor_intenso, t.mareo, t.presion_sistolica,
                   t.presion_diastolica, t.descripcion, t.nivel, t.color,
                   t.titulo, t.mensaje, t.revisado
            FROM citas c
            JOIN usuarios u ON u.id = c.usuario_id
            JOIN sucursales s ON s.id = c.sucursal_id
            LEFT JOIN triajes t ON t.cita_id = c.id
            WHERE c.id = @id
            """, conexion);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return NotFound(new { mensaje = "Cita no encontrada." });

        object? triaje = r.IsDBNull(9) ? null : new
        {
            dolorPecho = r.GetBoolean(9), dificultadRespiratoria = r.GetBoolean(10),
            inconsciente = r.GetBoolean(11), fiebre = r.GetBoolean(12),
            dolorIntenso = r.GetBoolean(13), mareo = r.GetBoolean(14),
            presionSistolica = r.IsDBNull(15) ? (int?)null : r.GetInt32(15),
            presionDiastolica = r.IsDBNull(16) ? (int?)null : r.GetInt32(16),
            descripcion = r.IsDBNull(17) ? null : r.GetString(17),
            nivel = r.IsDBNull(18) ? null : r.GetString(18),
            color = r.IsDBNull(19) ? null : r.GetString(19),
            titulo = r.IsDBNull(20) ? null : r.GetString(20),
            mensaje = r.IsDBNull(21) ? null : r.GetString(21),
            revisado = !r.IsDBNull(22) && r.GetBoolean(22)
        };
        return Ok(new
        {
            id = r.GetGuid(0), paciente = r.GetString(1), correo = r.GetString(2),
            telefono = r.IsDBNull(3) ? null : r.GetString(3),
            sucursal = r.GetString(4), ubicacion = r.GetString(5),
            fecha = r.GetFieldValue<DateOnly>(6), hora = r.GetFieldValue<TimeOnly>(7),
            estado = r.GetString(8), triaje
        });
    }
}
