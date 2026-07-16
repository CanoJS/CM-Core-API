# Seguridad

- Nunca exponer connection strings, secret keys ni `service_role` a Angular o Flutter.
- Usar llaves asimétricas de Supabase y validación mediante JWKS.
- `user_metadata` se permite para datos de presentación, nunca para permisos.
- El esquema `medical` no está incluido en los esquemas expuestos por PostgREST.
- RLS permanece habilitado como defensa adicional y `anon`/`authenticated` no tienen grants.
- La nota médica no debe aparecer en respuestas de paciente, recepción o dashboard.
- Los cambios de rol requieren renovar el token para actualizar `user_role`.
- Toda búsqueda o lectura de notas médicas deberá generar auditoría.

Después de desplegar una migración, ejecutar los Database Advisors de Supabase y resolver
hallazgos antes de publicar el ambiente.
