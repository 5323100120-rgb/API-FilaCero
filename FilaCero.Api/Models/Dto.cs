using System.ComponentModel.DataAnnotations;

namespace FilaCero.Api.Models;

public sealed class RegistroRequest
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string NombreCompleto { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string Correo { get; set; } = "";

    [RegularExpression(@"^\d{10}$", ErrorMessage = "El teléfono debe tener 10 dígitos.")]
    public string? Telefono { get; set; }

    [Required, MinLength(8), MaxLength(72)]
    public string Contrasena { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Correo { get; set; } = "";
    [Required] public string Contrasena { get; set; } = "";
}

public sealed record UsuarioPublico(Guid Id, string NombreCompleto, string Correo, string Rol);
public sealed record AuthResponse(string Token, DateTimeOffset ExpiraEn, UsuarioPublico Usuario);

public sealed record SucursalDto(string Id, string Nombre, string Ubicacion, string? Descripcion);
public sealed record HorarioDisponible(DateOnly Fecha, TimeOnly Hora);

public sealed class AgendarCitaRequest
{
    [Required] public string SucursalId { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public TimeOnly Hora { get; set; }
}

public sealed record CitaDto(Guid Id, string SucursalId, string SucursalNombre,
    DateOnly Fecha, TimeOnly Hora, string EstadoRegistro, DateTimeOffset CreadaEn, TriajeDto? Triaje);

public sealed record TriajeDto(
    bool DolorPecho, bool DificultadRespiratoria, bool Inconsciente, bool Fiebre,
    bool DolorIntenso, bool Mareo, int? PresionSistolica, int? PresionDiastolica,
    string? Descripcion, string? Nivel, string? Color, string? Titulo,
    string? Mensaje, bool Revisado);

public sealed class TriajeRequest
{
    public bool DolorPecho { get; set; }
    public bool DificultadRespiratoria { get; set; }
    public bool Inconsciente { get; set; }
    public bool Fiebre { get; set; }
    public bool DolorIntenso { get; set; }
    public bool Mareo { get; set; }
    [Range(40, 300)] public int? PresionSistolica { get; set; }
    [Range(20, 200)] public int? PresionDiastolica { get; set; }
    [StringLength(400)] public string? Descripcion { get; set; }
}

public sealed class CitaAdminDto
{
    public Guid Id { get; init; }
    public string Paciente { get; init; } = "";
    public string Correo { get; init; } = "";
    public string Sucursal { get; init; } = "";
    public DateOnly Fecha { get; init; }
    public TimeOnly Hora { get; init; }
    public string Estado { get; init; } = "";
    public bool TieneTriaje { get; init; }
}
