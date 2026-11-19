using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FilaCero.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace FilaCero.Api.Services;

public sealed class TokenService(IConfiguration config)
{
    public AuthResponse CrearToken(UsuarioPublico usuario)
    {
        var duracion = config.GetValue<int>("Jwt:DuracionMinutos", 120);
        var expira = DateTimeOffset.UtcNow.AddMinutes(duracion);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreCompleto),
            new Claim(ClaimTypes.Email, usuario.Correo),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };
        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expira.UtcDateTime,
            signingCredentials: new SigningCredentials(llave, SecurityAlgorithms.HmacSha256));

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expira, usuario);
    }
}
