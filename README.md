# FilaCero-Api — .NET 11 + PostgreSQL (FilaCeroDB)

**No se modificaron los nombres ni la estructura de los proyectos.** La API usa las tablas
`roles`, `usuarios`, `sucursales`, `horarios_sucursal`, `citas` y `triajes` de tu SQL.
Además de los endpoints existentes, `GET /api/citas/mias` devuelve el triaje de cada
cita, para que la PWA pueda recuperarlo después de reiniciar.

## Paso 1 — abrir solución y configurar secretos

Requiere SDK .NET 11, PostgreSQL y Visual Studio actualizado. Abre `FilaCero.sln`.

**Por seguridad, este ZIP no incluye la contraseña de PostgreSQL ni el secreto JWT**
que estaban escritos en `FilaCero.Api/appsettings.json`. Configúralos localmente con
`dotnet user-secrets` desde la carpeta `FilaCero.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=FilaCeroDB;Username=postgres;Password=TU_PASSWORD_POSTGRES"
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$jwtSecret = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet user-secrets set "Jwt:Key" $jwtSecret
```

Mantén `Jwt:Issuer` y `Jwt:Audience` en `appsettings.json` (ya están definidos).
Si ya tenías secrets configurados con el mismo UserSecretsId, no tienes que rehacerlos.
En despliegue, usa variables de entorno `ConnectionStrings__DefaultConnection` y `Jwt__Key`.
No subas la contraseña o clave JWT a Git.

## Paso 2 — revisar la base ya creada

En pgAdmin verifica que existe `FilaCeroDB` y que tiene las tablas de tu SQL. **No
vuelvas a ejecutar CREATE TABLE si ya existen.** `bd/02_ajustes_sobre_bd_existente.sql`
es opcional y cambia el índice de reservas para considerar también estados no cancelados.
Antes de usarlo, haz respaldo; si tu base ya funciona y no necesitas ese ajuste, no lo ejecutes.

Si no hay sucursales y horarios y quieres una demo ficticia, ejecuta `bd/03_sucursales_ejemplo.sql`.
La API toma sus IDs y horas de la BD (1=lunes, 7=domingo).

## Paso 3 — ejecutar la API en el puerto 5138

En la carpeta `FilaCero.Api`:

```powershell
dotnet restore
dotnet run --launch-profile http
```

O selecciona el perfil **http** en Visual Studio y F5. Para evitar problemas de certificado
local, la PWA usa el perfil **http** en `http://localhost:5138`.

Comprueba estos enlaces antes de abrir la PWA:

- `http://localhost:5138/api/salud`
- `http://localhost:5138/api/salud/bd` (debe incluir `FilaCeroDB`)
- `http://localhost:5138/api/sucursales`
- `http://localhost:5138/swagger` (solo Development)

## Paso 4 — iniciar la PWA

Abre el otro proyecto `FilaCero`, deja `API_BASE_URL=http://localhost:5138` en su
archivo `.env` y ejecuta `node server/index.mjs` o `INICIAR_WINDOWS.bat`.
Ingresa a `http://localhost:3000` y prueba registro, login, citas y triaje.

## Roles y citas

Solo se puede registrar el rol `usuario` desde el navegador. Para hacer admin una cuenta
registrada, entra como operador autorizado a pgAdmin y ejecuta:

```sql
UPDATE usuarios
SET rol_id = (SELECT id FROM roles WHERE nombre = 'admin')
WHERE correo = 'correo-del-administrador@ejemplo.com';
```

Cierra sesión y vuelve a entrar para actualizar el token. Admin solo puede consultar
citas y detalles, no modificar. `Agendada`, `EnProceso`, `Finalizada`, `Cancelada` son
los estados almacenados; esta API no simula la atención ni determina el nivel médico
automáticamente. Una cancelación solo es válida antes de la fecha/hora de la cita.

La API autentica con JWT y protege endpoints por rol. Para publicar en producción hace
falta configurar HTTPS, políticas de acceso, auditoría, revocación y protección de datos;
no se debe usar con datos clínicos reales sin las garantías correspondientes.
