using System.Security.Claims;
using FilaCero.Api.Models;
using FilaCero.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace FilaCero.Api.Controllers;

[ApiController]
[Authorize(Roles = "usuario")]
[Route("api/citas")]
public sealed class CitasController(NpgsqlDataSource dataSource, HorarioService horarios) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("mias")]
    public async Task<ActionResult<List<CitaDto>>> MisCitas()
    {
        var lista = new List<CitaDto>();
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT c.id, c.sucursal_id, s.nombre, c.fecha, c.hora, c.estado_registro, c.creada_en,
                   t.cita_id, t.dolor_pecho, t.dificultad_respiratoria, t.inconsciente,
                   t.fiebre, t.dolor_intenso, t.mareo, t.presion_sistolica,
                   t.presion_diastolica, t.descripcion, t.nivel, t.color,
                   t.titulo, t.mensaje, t.revisado
            FROM citas c JOIN sucursales s ON s.id = c.sucursal_id
            LEFT JOIN triajes t ON t.cita_id = c.id
            WHERE c.usuario_id = @usuario ORDER BY c.fecha DESC, c.hora DESC
            """, conexion);
        cmd.Parameters.AddWithValue("usuario", UsuarioId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            TriajeDto? triaje = r.IsDBNull(7) ? null : new TriajeDto(
                (!r.IsDBNull(8) && r.GetBoolean(8)), (!r.IsDBNull(9) && r.GetBoolean(9)), (!r.IsDBNull(10) && r.GetBoolean(10)),
                (!r.IsDBNull(11) && r.GetBoolean(11)), (!r.IsDBNull(12) && r.GetBoolean(12)), (!r.IsDBNull(13) && r.GetBoolean(13)),
                r.IsDBNull(14) ? null : r.GetInt32(14),
                r.IsDBNull(15) ? null : r.GetInt32(15),
                r.IsDBNull(16) ? null : r.GetString(16),
                r.IsDBNull(17) ? null : r.GetString(17),
                r.IsDBNull(18) ? null : r.GetString(18),
                r.IsDBNull(19) ? null : r.GetString(19),
                r.IsDBNull(20) ? null : r.GetString(20),
                (!r.IsDBNull(21) && r.GetBoolean(21)));
            lista.Add(new CitaDto(r.GetGuid(0), r.GetString(1), r.GetString(2),
                r.GetFieldValue<DateOnly>(3), r.GetFieldValue<TimeOnly>(4), r.GetString(5),
                r.GetFieldValue<DateTimeOffset>(6), triaje));
        }
        return Ok(lista);
    }

    [HttpPost]
    public async Task<IActionResult> Agendar([FromBody] AgendarCitaRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.SucursalId) || input.Fecha == default)
            return BadRequest(new { mensaje = "Sucursal y fecha son obligatorias." });
        var libres = await horarios.ObtenerDisponibles(input.SucursalId, input.Fecha);
        if (!libres.Contains(input.Hora))
            return Conflict(new { mensaje = "El horario no está disponible. Consulta los horarios vigentes." });
        try
        {
            await using var conexion = await dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO citas(usuario_id, sucursal_id, fecha, hora, estado_registro)
                VALUES (@usuario, @sucursal, @fecha, @hora, 'Agendada')
                RETURNING id
                """, conexion);
            cmd.Parameters.AddWithValue("usuario", UsuarioId);
            cmd.Parameters.AddWithValue("sucursal", input.SucursalId);
            cmd.Parameters.AddWithValue("fecha", input.Fecha);
            cmd.Parameters.AddWithValue("hora", input.Hora);
            var id = (Guid)(await cmd.ExecuteScalarAsync())!;
            return Created($"/api/citas/mias", new { id, mensaje = "Cita agendada correctamente." });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new { mensaje = "Alguien más reservó este horario. Elige otro." });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return BadRequest(new { mensaje = "La sucursal o usuario no existe." });
        }
    }

    [HttpPatch("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            UPDATE citas SET estado_registro = 'Cancelada', cancelada_en = NOW()
            WHERE id = @id AND usuario_id = @usuario AND estado_registro = 'Agendada'
              AND (fecha::timestamp + hora) > (NOW() AT TIME ZONE 'America/Hermosillo')
            RETURNING id
            """, conexion);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("usuario", UsuarioId);
        var actualizado = await cmd.ExecuteScalarAsync();
        if (actualizado is null)
            return NotFound(new { mensaje = "No existe una cita agendada tuya con ese ID." });
        return Ok(new { id, estado = "Cancelada", mensaje = "Horario liberado." });
    }

    [HttpPut("{id:guid}/triaje")]
    public async Task<IActionResult> GuardarTriaje(Guid id, [FromBody] TriajeRequest input)
    {
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO triajes (
                cita_id, dolor_pecho, dificultad_respiratoria, inconsciente,
                fiebre, dolor_intenso, mareo, presion_sistolica,
                presion_diastolica, descripcion
            )
            SELECT c.id, @dolor, @respira, @inconsciente, @fiebre, @intenso,
                   @mareo, @sistolica, @diastolica, @descripcion
            FROM citas c
            WHERE c.id = @cita AND c.usuario_id = @usuario AND c.estado_registro = 'Agendada'
            ON CONFLICT (cita_id) DO UPDATE SET
                dolor_pecho = EXCLUDED.dolor_pecho,
                dificultad_respiratoria = EXCLUDED.dificultad_respiratoria,
                inconsciente = EXCLUDED.inconsciente,
                fiebre = EXCLUDED.fiebre,
                dolor_intenso = EXCLUDED.dolor_intenso,
                mareo = EXCLUDED.mareo,
                presion_sistolica = EXCLUDED.presion_sistolica,
                presion_diastolica = EXCLUDED.presion_diastolica,
                descripcion = EXCLUDED.descripcion
            RETURNING cita_id
            """, conexion);
        cmd.Parameters.AddWithValue("cita", id);
        cmd.Parameters.AddWithValue("usuario", UsuarioId);
        cmd.Parameters.AddWithValue("dolor", input.DolorPecho);
        cmd.Parameters.AddWithValue("respira", input.DificultadRespiratoria);
        cmd.Parameters.AddWithValue("inconsciente", input.Inconsciente);
        cmd.Parameters.AddWithValue("fiebre", input.Fiebre);
        cmd.Parameters.AddWithValue("intenso", input.DolorIntenso);
        cmd.Parameters.AddWithValue("mareo", input.Mareo);
        cmd.Parameters.Add("sistolica", NpgsqlDbType.Integer).Value = (object?)input.PresionSistolica ?? DBNull.Value;
        cmd.Parameters.Add("diastolica", NpgsqlDbType.Integer).Value = (object?)input.PresionDiastolica ?? DBNull.Value;
        cmd.Parameters.Add("descripcion", NpgsqlDbType.Varchar).Value = (object?)input.Descripcion ?? DBNull.Value;
        var guardado = await cmd.ExecuteScalarAsync();
        if (guardado is null) return NotFound(new { mensaje = "Cita propia no encontrada o no está agendada." });
        return Ok(new { mensaje = "Triaje registrado (pendiente de revisión médica)." });
    }
}
