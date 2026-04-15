Lee el archivo `claude/skills/crear-cached-repository/SKILL.md` y sigue sus instrucciones para crear el repositorio con caché solicitado.

Repositorio con caché a crear: $ARGUMENTS

Pasos obligatorios según el skill:
1. Crear la interfaz del repositorio con caché
2. Implementar el repositorio heredando de `EFRepository<TContext>`
3. Inyectar `IMemoryCache` y definir la clave y duración de caché
4. Invalidar el caché en operaciones de escritura (Crear, Actualizar, Eliminar)
5. Verificar que `AddMemoryCache` esté condicionalmente registrado en Program.cs
