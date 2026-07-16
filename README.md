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

## Validación

```powershell
dotnet format MedicalAppointments.slnx --verify-no-changes --no-restore
dotnet build MedicalAppointments.slnx --no-restore
dotnet test MedicalAppointments.slnx --no-build --no-restore
```

Consulta [ARCHITECTURE.md](ARCHITECTURE.md) antes de agregar funcionalidades.
