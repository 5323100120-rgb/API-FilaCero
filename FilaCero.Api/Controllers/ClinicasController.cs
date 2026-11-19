using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace FilaCero.Api.Controllers;

// Integración OPCIONAL con Google Places API (New): requiere API key y facturación.
[ApiController]
[Route("api/clinicas")]
public sealed class ClinicasController(IHttpClientFactory factory, IConfiguration config) : ControllerBase
{
    [HttpGet("cercanas")]
    [Authorize(Roles = "usuario")]
    public async Task<IActionResult> Cercanas([FromQuery] double lat, [FromQuery] double lng,
        [FromQuery] double radio = 2000)
    {
        if (!double.IsFinite(lat) || !double.IsFinite(lng) || lat is < -90 or > 90
            || lng is < -180 or > 180 || !double.IsFinite(radio) || radio is < 100 or > 10000)
            return BadRequest(new { mensaje = "Latitud, longitud o radio inválidos." });

        var apiKey = config["GooglePlaces:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(503, new { mensaje = "Google Places no configurado. Agrega GooglePlaces:ApiKey." });

        using var cliente = factory.CreateClient();
        cliente.Timeout = TimeSpan.FromSeconds(12);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchNearby");
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask",
            "places.displayName,places.formattedAddress,places.location,places.googleMapsUri");
        request.Content = JsonContent.Create(new
        {
            includedTypes = new[] { "hospital", "doctor" },
            maxResultCount = 10,
            rankPreference = "DISTANCE",
            locationRestriction = new
            {
                circle = new { center = new { latitude = lat, longitude = lng }, radius = radio }
            }
        });
        try
        {
            using var response = await cliente.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { mensaje = "Google Places no pudo devolver los resultados. Revisa la clave y la API habilitada." });
            return Content(await response.Content.ReadAsStringAsync(), "application/json");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { mensaje = "No fue posible comunicarse con Google Places." });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { mensaje = "Google Places tardó demasiado en responder." });
        }
    }
}
