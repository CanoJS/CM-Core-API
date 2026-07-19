# Medical Appointments API

Backend de práctica para los clientes Angular 21 y Flutter 3.41.9. Está construido con
.NET 10, CQRS, Clean Architecture y PostgreSQL administrado por Supabase.

## Requisitos

- .NET SDK 10.0.301 o compatible según `global.json`.
- Docker Desktop para ejecutar Supabase localmente.
- Supabase CLI 2.109.1.

## Inicio rápido

```powershell
dotnet restore MedicalAppointments.slnx --locked-mode
npx supabase@2.109.1 start
dotnet run --project src/MedicalAppointments.Api
```

Configura secretos con variables de entorno o user-secrets. Nunca los agregues a
`appsettings.json`.

```powershell
dotnet user-secrets set "ConnectionStrings:Database" "<connection-string>" --project src/MedicalAppointments.Api
dotnet user-secrets set "Supabase:ProjectUrl" "https://<project-ref>.supabase.co" --project src/MedicalAppointments.Api
```

El alta administrativa de médicos (`POST /api/v1/admin/doctors`) crea el usuario directamente en
Supabase Auth Admin (`POST /auth/v1/admin/users`, ya confirmado con `email_confirm: true`, sin
enviar correo de invitación) y requiere además:

```powershell
dotnet user-secrets set "Supabase:SecretKey" "<secret-key>" --project src/MedicalAppointments.Api
```

`Supabase:SecretKey` es opcional al iniciar la API: sin ella, todos los demás endpoints
funcionan con normalidad y solo el alta de médicos responde `503` hasta que se configure.

El request requiere `temporaryPassword` (mínimo 8 caracteres) — es la contraseña con la que el
médico inicia sesión de inmediato; no se envía ningún correo. Se eligió creación directa en vez
de la invitación por correo de Supabase (`POST /auth/v1/invite`) porque esa ruta está sujeta a
`over_email_send_rate_limit` de Supabase, lo que volvía poco confiable el alta de médicos en
lote/demo para este MVP.

## Disponibilidad de médicos

`GET /api/v1/doctors/{doctorId}/availability?from=YYYY-MM-DD&to=YYYY-MM-DD` devuelve la
disponibilidad de un médico agrupada por fecha local de la clínica.

- Requiere autenticación (`401` sin token); cualquier rol autenticado puede consultarla.
- Zona horaria de la clínica: `America/Mexico_City` (configurable vía `Clinic:TimeZone`).
- Horario: lunes a viernes, 08:00–18:00 hora local, bloques fijos de 30 minutos (20 bloques por
  día laborable). Sábados y domingos no producen bloques ni aparecen en la respuesta.
- `from`/`to` son inclusivos; el rango máximo es 31 fechas inclusivas. `to` no puede ser menor
  que `from`. Fuera de rango o parámetros ausentes/malformados → `400`.
- Médico inexistente o inactivo → `404`.
- `date` es la fecha local (`YYYY-MM-DD`); `startsAt` es el inicio del bloque en UTC (ISO 8601).
  La duración es siempre 30 minutos fijos: el contrato **no incluye `endsAt`**, a propósito,
  porque el modelo ya acordado con Angular (`availability.model.ts`) solo tiene `startsAt` y
  `available`; se documenta aquí en vez de cambiarlo silenciosamente.
- Un bloque tiene `available: false` si ya está ocupado por una cita `SCHEDULED`, o si ya pasó o
  es el instante actual (debe ser estrictamente futuro). Las citas `CANCELLED` no ocupan bloques.
- **La disponibilidad es informativa.** Puede quedar desactualizada de inmediato si otro
  paciente reserva el mismo bloque; la consulta no bloquea filas ni abre transacciones. La
  defensa definitiva contra doble reserva es el índice único parcial
  `ux_appointments_doctor_slot_scheduled` en PostgreSQL, que responde `409` en
  `POST /api/v1/appointments` si el bloque ya fue tomado entre la consulta y la reserva.

Ejemplo de respuesta:

```jsonc
[
  {
    "date": "2026-07-20",
    "slots": [
      { "startsAt": "2026-07-20T14:00:00+00:00", "available": true },
      { "startsAt": "2026-07-20T14:30:00+00:00", "available": false }
    ]
  }
]
```

`startsAt` es UTC pero se serializa con offset numérico (`+00:00`), no con sufijo `Z` como en el
ejemplo de `docs/API-CONVENTIONS.md` — es el comportamiento por defecto de `DateTimeOffset` en
System.Text.Json (verificado, no un supuesto) y ya aplica igual a `startsAt`/`endsAt` en
`POST /api/v1/appointments`. Ambos representan el mismo instante UTC; `Date.parse` (JS/Dart) lo
interpreta igual. Es una discrepancia preexistente entre la documentación y el runtime, no
introducida en esta fase; no se corrigió aquí para no tocar el formato de endpoints ya
existentes fuera de esta fase.

## Ciclo de vida de citas

Base: `/api/v1/appointments`. Todos los endpoints requieren JWT (`401` sin token).

| Endpoint | Rol | Regla de acceso |
|---|---|---|
| `POST /` | PATIENT | `patientId` sale del JWT; nunca se acepta en el body. |
| `GET /` | PATIENT, DOCTOR, ADMIN | PATIENT: solo propias. DOCTOR: solo asignadas. ADMIN: todas. |
| `GET /{id}` | PATIENT, DOCTOR, ADMIN | Cita ajena o no asignada → `404` (no revela existencia). |
| `PATCH /{id}/cancel` | PATIENT, ADMIN | PATIENT: solo propia y con más de 24h de anticipación (exactamente 24h o menos se rechaza). ADMIN: cualquiera `SCHEDULED`, incluso dentro de 24h. DOCTOR no cancela en este MVP. |
| `PATCH /{id}/reschedule` | ADMIN | Solo ADMIN en este MVP. |
| `PATCH /{id}/attend` | DOCTOR | Solo el médico asignado; no antes de `startsAt`. |

Reglas compartidas de horario (mismas que disponibilidad): lunes a viernes, 08:00–18:00 hora
`America/Mexico_City`, bloques exactos de 30 minutos, segundos y fracciones de segundo
rechazados. `IClock` decide "ahora" en todos los casos — nunca `DateTimeOffset.UtcNow` directo.

`version` viaja como string opaco (token de `xmin`) en request y response de `cancel`,
`reschedule` y `attend` — igual que en Specialties y Doctors. Token malformado → `400`; token
obsoleto → `409`.

Un `PATCH` que reagenda al mismo médico y horario todavía ejecuta un `UPDATE` real (fuerza
`DoctorId`/`StartsAt` como modificados) para que un token `xmin` obsoleto siga detectándose —
igual mecanismo que el resto de las mutaciones con concurrencia optimista de este proyecto.

`GET /` y `GET /{id}` devuelven un DTO enriquecido con nombres de paciente/médico y especialidad
(vía `JOIN`, sin duplicar esos datos en `appointments`):

```jsonc
[
  {
    "id": "b5e1...",
    "patientId": "5e2b...",
    "patientFirstName": "Jesús",
    "patientLastName": "Cano Méndez",
    "patientName": "Jesús Cano Méndez",
    "doctorId": "9a3f...",
    "doctorFirstName": "Ana",
    "doctorLastName": "López",
    "doctorName": "Ana López",
    "specialtyId": "1c4d...",
    "specialtyName": "Cardiología",
    "startsAt": "2026-07-20T15:00:00+00:00",
    "endsAt": "2026-07-20T15:30:00+00:00",
    "status": "SCHEDULED",
    "reason": "Consulta general",
    "medicalNote": null,
    "createdAt": "2026-07-18T18:00:00+00:00",
    "updatedAt": "2026-07-18T18:00:00+00:00",
    "version": "1"
  }
]
```

`doctorName`/`patientName` son `"{firstName} {lastName}"`, sin prefijo `Dr./Dra.` — el dominio no
modela género/título, y adivinarlo a partir del nombre no es correcto ni apropiado. Si el
frontend necesita el prefijo, debe agregarlo en la capa de presentación.

`medicalNote` nunca aparece en respuestas de PATIENT (`docs/SECURITY.md`: "La nota médica no
debe aparecer en respuestas de paciente..."; este sistema no tiene rol de recepción/dashboard
separado, así que la regla se reduce a "PATIENT nunca la ve"). DOCTOR (su propio paciente) y
ADMIN sí la ven. Cada lectura de una nota no nula queda registrada (`ILogger`, solo
`appointmentId`/`viewerUserId`, nunca el contenido) — `docs/SECURITY.md`: "Toda búsqueda o
lectura de notas médicas deberá generar auditoría".

`GET /` acepta filtros opcionales `status` (`SCHEDULED`/`ATTENDED`/`CANCELLED`, inválido → `400`),
`from`/`to` (`YYYY-MM-DD`, interpretados en `America/Mexico_City`; `to < from` o rango mayor a
366 fechas inclusivas → `400`).

### Idempotencia en la creación de citas

`POST /` acepta un header opcional `Idempotency-Key` (máx. 200 caracteres). Con la misma clave y
el mismo payload (mismo `doctorId`+`startsAt`+`reason`), devuelve la respuesta original sin crear
una segunda cita. Con la misma clave y payload distinto → `409`. Sin header, comportamiento normal
(no hay cambio si el cliente no la envía).

El alcance es por `(usuario, operación, clave)`, respaldado por la restricción única existente
`ux_idempotency_user_operation_key` en `medical.idempotency_requests` — la misma clave usada por
usuarios distintos nunca colisiona. Dos solicitudes concurrentes con la misma clave: la cita y su
registro de idempotencia se insertan en una sola transacción explícita; si otra solicitud gana la
carrera por esa clave, la transacción perdedora se revierte (incluida la cita) y se reintenta una
lectura para devolver la respuesta de la ganadora en vez de un error genérico.

```powershell
curl -X POST https://localhost:.../api/v1/appointments `
  -H "Authorization: Bearer <token>" `
  -H "Idempotency-Key: <uuid-del-cliente>" `
  -H "Content-Type: application/json" `
  -d '{"doctorId":"...","startsAt":"2026-07-20T15:00:00Z","reason":"Consulta general"}'
```

## Validación

```powershell
dotnet format MedicalAppointments.slnx --verify-no-changes --no-restore
dotnet build MedicalAppointments.slnx --no-restore
dotnet test MedicalAppointments.slnx --no-build --no-restore
```

Las pruebas que golpean PostgreSQL real (`[RealDatabaseFact]`) se omiten (`Skipped`) salvo que
se active explícitamente:

```powershell
$env:RUN_REAL_DB_TESTS = "true"
dotnet test MedicalAppointments.slnx --no-build --no-restore
```

Requieren `ConnectionStrings:Database` configurada localmente; se ejecutan dentro de
transacciones revertidas y nunca crean usuarios reales de Supabase Auth.

## Deploy en AWS App Runner

Supabase sigue siendo la base de datos y el proveedor de autenticación. AWS App Runner solo
hospeda el contenedor de esta API — nada de Postgres, Auth ni RLS se mueve a AWS.

### Imagen Docker

`src/MedicalAppointments.Api/Dockerfile` es multi-stage (SDK para build/publish, `aspnet`
runtime para ejecutar) y ya trae `.dockerignore` en la raíz del repo. El **build context debe
ser la raíz del repositorio** (el Dockerfile hace `dotnet restore` sobre todo el `.slnx`, no solo
sobre el proyecto `Api`):

```powershell
docker build -f src/MedicalAppointments.Api/Dockerfile -t medical-appointments-api .
docker run --rm -p 8080:8080 --env-file .env.local medical-appointments-api
```

El contenedor escucha en `0.0.0.0:8080` (`ENV ASPNETCORE_URLS=http://+:8080` en el Dockerfile) y
corre como el usuario no-root `app` que trae la imagen base — no como root.

### Publicar la imagen (Amazon ECR)

```powershell
aws ecr create-repository --repository-name medical-appointments-api
aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin <account-id>.dkr.ecr.<region>.amazonaws.com
docker tag medical-appointments-api:latest <account-id>.dkr.ecr.<region>.amazonaws.com/medical-appointments-api:latest
docker push <account-id>.dkr.ecr.<region>.amazonaws.com/medical-appointments-api:latest
```

### Configuración del servicio App Runner

- **Puerto del contenedor:** `8080` (debe coincidir con `ASPNETCORE_URLS` del Dockerfile).
- **Health check:** ruta `/health/live`, método HTTP, sin autenticación (`AllowAnonymous`). No
  se agregó un endpoint `/health` nuevo porque `/health/live` ya cumple el mismo propósito desde
  una fase anterior; usar esa ruta al configurar el health check de App Runner.
- **CPU/memoria:** mínimo recomendado para un backend .NET simple (0.25 vCPU / 0.5 GB) suele
  alcanzar para este MVP; ajustar según carga real observada.

### Variables de entorno

**Aviso sobre nombres:** una preparación de despliegue anterior sugirió los nombres
`ConnectionStrings__MedicalAppointments`, `Supabase__ServiceRoleKey`, `Supabase__JwtIssuer` y
`Supabase__JwtAudience`. Ninguno de esos cuatro coincide con una clave de configuración que el
código realmente lea — documentarlos tal cual habría sido inventar un contrato que no existe. La
tabla de abajo usa los nombres **reales**, ya usados en `Program.cs`/`DependencyInjection.cs`
desde fases anteriores:

| Variable (formato .NET `__` para env vars) | Obligatoria | Notas |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Recomendada | `Production` en App Runner. |
| `ASPNETCORE_URLS` | No | Ya fijada en el Dockerfile (`http://+:8080`); solo si necesitas otro puerto. |
| `ConnectionStrings__Database` | **Sí** | Cadena de conexión a Postgres de Supabase. Incluir `SSL Mode=Require` (Supabase exige TLS fuera de la red interna de Docker local). |
| `Supabase__ProjectUrl` | **Sí** | `https://<project-ref>.supabase.co`. También se usa para derivar el `issuer` del JWT (`{ProjectUrl}/auth/v1`) — no hay una variable `JwtIssuer` separada. La audiencia (`authenticated`) es una constante fija de Supabase, tampoco configurable. |
| `Supabase__SecretKey` | No | Habilita `POST /api/v1/admin/doctors` (alta directa de médicos, sin correo). Sin ella, ese único endpoint responde `503`; el resto de la API funciona. Acepta clave moderna `sb_secret_...` o `service_role` legacy. |
| `Clinic__TimeZone` | No | Por defecto `America/Mexico_City`. |
| `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ... | **Sí** (si hay frontend), salvo que uses `Cors__AllowAnyOrigin=true` | Orígenes exactos de Angular/Flutter-web en producción. Sin esto, CORS bloquea todo origen (lista vacía por defecto). Ejemplo: `Cors__AllowedOrigins__0=https://front.example.com`. |
| `Cors__AllowAnyOrigin` | No | **Solo para demo/MVP** — ver advertencia abajo. Por defecto `false`, usa `Cors__AllowedOrigins`. |
| `OpenApi__Enabled` | No | Ver sección siguiente. |

Ninguna de estas variables tiene un valor por defecto que funcione en producción salvo
`Clinic__TimeZone` y `ASPNETCORE_URLS` — configúralas en la consola de App Runner (o vía
Secrets Manager para `ConnectionStrings__Database` y `Supabase__SecretKey`), nunca en
`appsettings.json` ni en el Dockerfile.

### CORS abierto temporal (demo)

Para integrar rápido con Angular/Flutter durante una demo, sin mantener aún un allowlist de
orígenes, existe un flag angosto:

```powershell
$env:Cors__AllowAnyOrigin = "true"
```

Con `Cors__AllowAnyOrigin=true` la política CORS usa `AllowAnyOrigin()` + `AllowAnyHeader()` +
`AllowAnyMethod()` y **ignora** `Cors__AllowedOrigins`. Nunca se combina con
`AllowCredentials()` (el navegador lo rechazaría de todos modos) — esta API no depende de
cookies para autenticación, solo de bearer tokens, así que no hay nada que proteger con esa
combinación.

**Esto es configuración temporal de demo/MVP, no apta para producción real.** CORS abierto solo
permite que cualquier origen en el navegador llame la API; no reemplaza autenticación ni
autorización — JWT, RLS y las reglas de rol siguen aplicando igual. Aun así, en producción real
usa orígenes específicos:

```powershell
# Producción real: false (o ausente) y orígenes explícitos
$env:Cors__AllowAnyOrigin = "false"
$env:Cors__AllowedOrigins__0 = "https://front.example.com"
```

### Habilitar OpenAPI temporalmente (demo)

`GET /openapi/v1.json` (documento generado por `Microsoft.AspNetCore.OpenApi`) y la UI visual de
Swagger en `GET /swagger` (paquete `Swashbuckle.AspNetCore.SwaggerUI`, solo la UI — apunta al
mismo documento en vez de generar uno propio) solo se exponen automáticamente cuando
`ASPNETCORE_ENVIRONMENT=Development`. Para una demo en App Runner sin cambiar el ambiente
completo (que también activaría páginas de error detalladas), se agregó un flag angosto:

```powershell
# En la consola de App Runner, o localmente:
$env:OpenApi__Enabled = "true"
```

Con el flag activo, `/swagger` (redirige a `/swagger/index.html`) abre la UI sin autenticación —
igual que `/openapi/v1.json`, es middleware anterior a `UseAuthentication`/`UseAuthorization`, no
un endpoint sujeto a la política de autenticación por defecto. El resto de la API (`/api/v1/*`)
no se ve afectado; su autenticación/autorización no cambia.

Quitar la variable (o ponerla en `false`) para volver a ocultar el documento después de la demo.

Consulta [ARCHITECTURE.md](ARCHITECTURE.md) antes de agregar funcionalidades.
