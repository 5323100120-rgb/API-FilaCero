using Npgsql;

namespace FilaCero.Api.Services;

public sealed class HorarioService(NpgsqlDataSource dataSource)
{
    // En bd: 1=lunes, 2=martes, ..., 7=domingo.
    public async Task<List<TimeOnly>> ObtenerDisponibles(string sucursalId, DateOnly fecha)
    {
        await using var conexion = await dataSource.OpenConnectionAsync();
        await using var horarioCmd = new NpgsqlCommand("""
            SELECT h.hora_apertura, h.hora_cierre, h.minutos_por_cita
            FROM horarios_sucursal h
            JOIN sucursales s ON s.id = h.sucursal_id
            WHERE h.sucursal_id = @sucursalId AND s.activa = TRUE AND h.dia_semana = @dia
            """, conexion);
        horarioCmd.Parameters.AddWithValue("sucursalId", sucursalId);
        horarioCmd.Parameters.AddWithValue("dia", fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek);

        TimeOnly apertura, cierre;
        int minutos;
        await using (var r = await horarioCmd.ExecuteReaderAsync())
        {
            if (!await r.ReadAsync()) return [];
            apertura = r.GetFieldValue<TimeOnly>(0);
            cierre = r.GetFieldValue<TimeOnly>(1);
            minutos = r.GetInt32(2);
        }
        if (minutos <= 0 || apertura >= cierre) return [];

        var ocupados = new HashSet<TimeOnly>();
        await using var ocupadoCmd = new NpgsqlCommand("""
            SELECT hora FROM citas
            WHERE sucursal_id = @sucursalId AND fecha = @fecha AND estado_registro <> 'Cancelada'
            """, conexion);
        ocupadoCmd.Parameters.AddWithValue("sucursalId", sucursalId);
        ocupadoCmd.Parameters.AddWithValue("fecha", fecha);
        await using (var r = await ocupadoCmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) ocupados.Add(r.GetFieldValue<TimeOnly>(0));
        }

        var ahora = DateTimeOffset.UtcNow;
        // Zona horaria configurable (por defecto Sonora); IANA reconocida en .NET moderno.
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Hermosillo");
        var local = TimeZoneInfo.ConvertTime(ahora, zona).DateTime;
        var hoy = DateOnly.FromDateTime(local);
        if (fecha < hoy) return [];

        var disponibles = new List<TimeOnly>();
        for (var hora = apertura; hora.AddMinutes(minutos) <= cierre; hora = hora.AddMinutes(minutos))
        {
            if (fecha == hoy && hora <= TimeOnly.FromDateTime(local)) continue;
            if (!ocupados.Contains(hora)) disponibles.Add(hora);
        }
        return disponibles;
    }
}
