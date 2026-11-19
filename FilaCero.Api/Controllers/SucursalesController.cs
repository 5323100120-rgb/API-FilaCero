using FilaCero.Api.Models;
using FilaCero.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FilaCero.Api.Controllers;

[ApiController]
[Route("api/sucursales")]
public sealed class SucursalesController(NpgsqlDataSource dataSource, HorarioService horarios) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SucursalDto>>> Listar()
    {
        var lista = new List<SucursalDto>();
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT id, nombre, ubicacion, descripcion
            FROM sucursales WHERE activa = TRUE ORDER BY nombre
            """, conexion);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            lista.Add(new SucursalDto(r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3)));
        return Ok(lista);
    }

    // GET /api/sucursales/centro/disponibilidad?fecha=2026-11-25
    [HttpGet("{id}/disponibilidad")]
    public async Task<IActionResult> Disponibilidad(string id, [FromQuery] DateOnly fecha)
    {
        if (fecha == default) return BadRequest(new { mensaje = "Indica una fecha válida." });
        var disponibles = await horarios.ObtenerDisponibles(id, fecha);
        return Ok(new { sucursalId = id, fecha, horarios = disponibles });
    }
}
