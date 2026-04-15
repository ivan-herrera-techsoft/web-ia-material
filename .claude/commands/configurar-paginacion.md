Lee el archivo `claude/skills/configurar-paginacion/SKILL.md` y sigue sus instrucciones para implementar la paginación en el endpoint o servicio solicitado.

Entidad/endpoint a paginar: $ARGUMENTS

Pasos obligatorios según el skill:
1. Usar `SolicitudPaginacion` como parámetro de entrada con: `Pagina`, `TamanoPagina`, `OrdenarPor`, `Filtro`, `Descendente`
2. Retornar `ListaPaginada<T>` con metadatos de paginación
3. Agregar el header `X-Pagination` en la respuesta del endpoint
4. Verificar que CORS expone el header: `.WithExposedHeaders("X-Pagination")`
5. Implementar el ordenamiento dinámico y filtrado en el repositorio via IQueryable
