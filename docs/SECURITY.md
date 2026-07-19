# Seguridad

- Nunca exponer connection strings, secret keys ni `service_role` a Angular o Flutter.
- Usar llaves asimétricas de Supabase y validación mediante JWKS.
- `user_metadata` se permite para datos de presentación, nunca para permisos.
- El esquema `medical` no está incluido en los esquemas expuestos por PostgREST.
- RLS permanece habilitado como defensa adicional y `anon`/`authenticated` no tienen grants.
- La nota médica no debe aparecer en respuestas de paciente, recepción o dashboard.
- Los cambios de rol requieren renovar el token para actualizar `user_role`.
- Todo registro debe enviar `first_name` y `last_name` en `raw_user_meta_data`. Se usan
  solo para presentación; el trigger crea el perfil con rol fijo `PATIENT`.
- `first_name`, `last_name` o cualquier otro valor de `user_metadata` nunca decide permisos.
- Toda búsqueda o lectura de notas médicas deberá generar auditoría.
- `Supabase:SecretKey` (Auth Admin, usado para invitar médicos) solo se configura mediante
  user-secrets o variable de entorno; nunca en `appsettings.json`, frontend, logs, mensajes de
  error ni pruebas automatizadas. No es obligatoria al iniciar la API: si falta, el alta de
  médicos responde `503` sin revelar detalles y el resto de la API sigue funcionando.
- Las pruebas automatizadas nunca crean usuarios reales en Supabase Auth; usan un fake de
  `IAuthAdminService`.
- Un PATIENT con `medical.user_profiles.active = false` no puede crear (`POST /api/v1/appointments`)
  ni cancelar (`PATCH /api/v1/appointments/{id}/cancel`) citas; responde `403`. Un DOCTOR inactivo
  no puede atender citas (`PATCH /api/v1/appointments/{id}/attend`); responde `403`. La
  verificación se hace contra el perfil actual en base de datos en cada request, no contra el
  claim `user_role` del JWT — ese claim solo se recalcula al refrescar el token
  (`custom_access_token_hook`), así que una cuenta desactivada queda bloqueada de inmediato sin
  esperar la expiración de su access token vigente.

Después de desplegar una migración, ejecutar los Database Advisors de Supabase y resolver
hallazgos antes de publicar el ambiente.
