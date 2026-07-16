# Arquitectura obligatoria

## Regla de dependencias

```text
Domain <- Application <- Infrastructure
              ^               ^
              +------ API ----+
```

- `Domain` contiene entidades, value objects, reglas e invariantes. No conoce EF Core,
  ASP.NET, Supabase ni DTO HTTP.
- `Application` contiene commands, queries, handlers, contratos de persistencia y casos de
  uso. Solo referencia `Domain`.
- `Infrastructure` implementa persistencia, reloj, agenda y conexiones externas.
- `Api` transforma HTTP en commands/queries, configura autenticación y compone dependencias.
- Las pruebas de arquitectura fallan si una referencia viola estas fronteras.

## CQRS

- Toda escritura es un `ICommand<TResponse>` con su handler.
- Toda lectura es un `IQuery<TResponse>` con su handler.
- Un endpoint no accede directamente al `DbContext`.
- Commands no devuelven entidades de EF; devuelven contratos estables.
- Queries pueden producir DTO enriquecidos para evitar múltiples llamadas del frontend.

Cada caso de uso se organiza como vertical slice:

```text
Application/Appointments/CreateAppointment/
  CreateAppointmentCommand.cs
  CreateAppointmentCommandHandler.cs
```

## Persistencia

- Las tablas de negocio viven en el esquema privado `medical`.
- Web y Flutter no acceden directamente a esas tablas; consumen la API.
- Las migraciones de `supabase/migrations` son la fuente de verdad del esquema.
- UUID es el tipo de identificador público en PostgreSQL, .NET, TypeScript y Dart.
- Fechas se almacenan en UTC como `timestamptz`.
- Las restricciones críticas deben existir en PostgreSQL además de C#.

## Autenticación y autorización

- Supabase Auth emite los access tokens.
- ASP.NET Core valida firma, emisor, audiencia y expiración mediante OIDC/JWKS.
- Los roles de aplicación son `PATIENT`, `DOCTOR` y `ADMIN`.
- El claim `user_role` se obtiene mediante el Custom Access Token Hook desde
  `medical.user_profiles`; nunca se confía en `user_metadata` para autorización.
- Toda ruta requiere autenticación salvo que declare explícitamente `AllowAnonymous`.
- La autorización del caso de uso vuelve a comprobar el rol aunque el frontend o endpoint
  oculten la acción.

## Calidad obligatoria

ESLint no analiza C#. Para el backend se usan:

- `.editorconfig` y analizadores Roslyn.
- Warnings tratados como errores.
- `dotnet format --verify-no-changes`.
- Pruebas unitarias, de integración y de arquitectura.
- Versiones de NuGet centralizadas y lockfiles comprometidos.

No se acepta código que rompa el pipeline definido en `.github/workflows/backend-ci.yml`.
