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
