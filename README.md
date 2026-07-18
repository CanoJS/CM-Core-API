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

El alta administrativa de médicos (`POST /api/v1/admin/doctors`) invita usuarios mediante
Supabase Auth Admin y requiere además:

```powershell
dotnet user-secrets set "Supabase:SecretKey" "<secret-key>" --project src/MedicalAppointments.Api
dotnet user-secrets set "Supabase:DoctorInviteRedirectUrl" "<url-de-redireccion>" --project src/MedicalAppointments.Api
```

`Supabase:SecretKey` es opcional al iniciar la API: sin ella, todos los demás endpoints
funcionan con normalidad y solo el alta de médicos responde `503` hasta que se configure.
`Supabase:DoctorInviteRedirectUrl` es opcional; si falta, Supabase usa la Site URL configurada
en el proyecto.

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

Consulta [ARCHITECTURE.md](ARCHITECTURE.md) antes de agregar funcionalidades.
