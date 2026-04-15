# Especificaciones — Aplicación WEB para el Registro de Actividades

> **Versión:** 1.0  
> **Fecha:** 2026-04-14  
> **Basado en:** Directivas.md (original) con correcciones e incorporación de definiciones faltantes

---

## 1. Objetivo

Aplicación para que los diferentes elementos del equipo realicen un registro de sus actividades diarias con el objetivo de realizar un análisis del tiempo invertido en cada proyecto-cliente, y en caso de que la actividad sea facturable para el cliente, obtener la relación de actividades-horas.

---

## 2. Stack Tecnológico

|Capa|Tecnología|
|---|---|
|Frontend|Angular (latest)|
|Backend|.NET 10|
|Base de datos|SQL Server 2022|
|ORM|Entity Framework|
|Autenticación|Azure Entra ID|

---

## 3. Seguridad y Roles

### 3.1 Roles

La aplicación define 4 roles con la siguiente jerarquía:

|Rol|Descripción|
|---|---|
|Superusuario|Acceso total e implícito a todas las funcionalidades del sistema. No requiere mención explícita en cada módulo.|
|Administrador|Gestión completa de catálogos, usuarios y configuración.|
|Líder|Gestión de proyectos asignados y visibilidad sobre su equipo.|
|Usuario|Registro de actividades propias y consulta de información asignada.|

**Regla global:** El Superusuario tiene acceso implícito a TODAS las funcionalidades sin excepción. No es necesario mencionarlo explícitamente en cada sección; si un Administrador puede hacerlo, el Superusuario también.

### 3.2 Autenticación con Azure Entra ID

- La aplicación utiliza Azure Entra ID como servicio de autenticación.
- Como segunda capa de seguridad, el usuario debe estar registrado en la aplicación a través de su correo electrónico.
- Si el usuario autenticado en Azure Entra ID no está dado de alta en la aplicación:
    - No podrá ver el menú ni acceder a ninguna ruta (frontend ni backend).
    - Se muestra una página con la opción de solicitar permisos al portal.
    - Las solicitudes se almacenan para que un Administrador las acepte o rechace.
    - Al aceptar, el Administrador asigna permisos mínimos (rol de Usuario).

### 3.3 Control de Acceso en Backend

- Todos los endpoints están protegidos usando los roles como control de acceso.
- Una vez identificado el usuario, la aplicación web consume un endpoint en el backend para obtener los permisos del usuario dentro de la aplicación y utilizarlos en el frontend (menús, rutas, acciones).

---

## 4. Máquina de Estados (Estatus)

### 4.1 Estatus Estándar (aplica a: Divisiones, Plataformas, Proyectos, Perfiles, Usuarios)

|Estatus|Descripción|
|---|---|
|Borrador|Valor inicial. No es usable ni visible para otros usuarios. La eliminación en este estatus es **física** (DELETE de la base de datos).|
|Activo|Registro usable y visible. Una vez activo, **no es posible regresar a Borrador**.|
|Inactivo|No usable pero visible para consultas.|
|Eliminado|No usable, no visible para usuarios regulares, visible solo para Administradores. Los registros persisten en la base de datos (soft delete).|

**Transiciones permitidas:**

```
Borrador → Activo
Borrador → Eliminado (eliminación física)
Activo → Inactivo
Activo → Eliminado (soft delete)
Inactivo → Activo
Inactivo → Eliminado (soft delete)
Eliminado → Activo (solo Administrador o Superusuario)
```

**Transiciones prohibidas:**

```
Activo → Borrador
Inactivo → Borrador
Eliminado → Borrador
Eliminado → Inactivo
```

**Regla de cascada:** Al marcar un registro como Eliminado, este no aparecerá en listas desplegables ni podrá ser usado para ningún registro nuevo, incluyendo sus dependencias.

### 4.2 Estatus para Clientes (excepción)

Los Clientes solo manejan 3 estatus: **Borrador**, **Activo** e **Inactivo**. No tienen estatus "Eliminado".

---

## 5. Catálogos

### 5.1 Divisiones

Departamentos o divisiones de la empresa.

**Campos:**

- Descripción (texto, obligatorio)
- Estatus (máquina de estados estándar §4.1)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:** Solo Administradores (y Superusuario por regla global §3.1).

**Reglas:**

- Aplican las reglas estándar de estatus (§4.1).

---

### 5.2 Plataformas

Soluciones de software asociadas a una División. Relación 1:N (una División tiene muchas Plataformas).

**Campos:**

- Nombre descriptivo (texto, máximo 50 caracteres, obligatorio)
- División asociada (relación, obligatorio)
- Estatus (máquina de estados estándar §4.1)
- Recursos asignados (usuarios, relación N:N)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:** Solo Administradores (y Superusuario por regla global §3.1).

**Reglas:**

- Aplican las reglas estándar de estatus (§4.1).
- Solo se pueden asociar Divisiones con estatus Activo.

---

### 5.3 Proyectos

Elemento asociado a una Plataforma y a un Cliente (interno o externo).

**Campos:**

- Nombre (texto, obligatorio)
- Plataforma asociada (relación, obligatorio) — muestra la División como dato informativo (heredado de la Plataforma)
- Cliente asociado (relación, obligatorio)
- Estatus (máquina de estados estándar §4.1)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:**

- Administradores y Superusuarios: Crear nuevos proyectos y modificar el nombre de proyectos existentes.
- Líderes: Pueden agregar/remover recursos en la pantalla de edición y cambiar el estatus (inhabilitar).
- Los recursos (usuarios) de un Proyecto se heredan de la Plataforma asociada.

**Reglas:**

- Aplican las reglas estándar de estatus (§4.1).
- Solo se pueden asociar Plataformas con estatus Activo.
- Solo se pueden asociar Clientes con estatus Activo.

---

### 5.4 Categorías

Clasificadores de actividades registrados por un Administrador y asignados a Usuarios según su perfil.

**Campos:**

- Nombre (texto, máximo 50 caracteres, obligatorio, único)
- Estatus (máquina de estados estándar §4.1)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:** Administradores para crear/editar. Administradores y Líderes para asignar categorías a usuarios.

**Reglas:**

- No se puede capturar dos categorías con el mismo nombre.
- Aplican las reglas estándar de estatus (§4.1).

**Categoría especial — "0:Tiempo Adicional":**

- Es seed data del sistema (se crea automáticamente con la instalación).
- Está asignada a **todos** los usuarios por defecto.
- No puede ser eliminada ni desasignada.
- Se utiliza para horas que exceden las 8 reglamentarias o actividades en fines de semana.

---

### 5.5 Perfiles

Describen el rol administrativo y operativo del usuario.

**Campos:**

- Título (texto, máximo 20 caracteres, obligatorio, único)
- Descripción breve del puesto (texto, máximo 150 caracteres)
- Clasificación: Administrativo | Operativo | Staff
- Estatus (máquina de estados estándar §4.1)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:** Solo Administradores (y Superusuario por regla global §3.1).

**Reglas:**

- No se puede capturar dos perfiles con el mismo título.
- Aplican las reglas estándar de estatus (§4.1).

---

### 5.6 Clientes

Razones sociales que adquieren el producto.

**Campos:**

- Razón Social (texto, máximo 200 caracteres, obligatorio, único)
- Nombre (texto, máximo 50 caracteres, obligatorio, único)
- RFC (texto, máximo 15 caracteres, obligatorio, único)
- Estatus: Borrador, Activo, Inactivo **(sin estatus Eliminado)**
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

**Acceso:** Solo Administradores (y Superusuario por regla global §3.1).

**Reglas:**

- Razón Social, Nombre y RFC son campos únicos (no pueden duplicarse).
- La eliminación en estatus Borrador es física.
- No existe estatus Eliminado; los registros solo pueden pasar a Inactivo.

---

## 6. Usuarios

### 6.1 Campos

- Correo electrónico (desde Azure Entra ID, identificador principal)
- Nombre de Usuario
- Apellido Paterno
- Apellido Materno
- Perfil (lista desplegable, relación con catálogo de Perfiles)
- Estatus (máquina de estados estándar §4.1, con regla adicional: al pasar a "Eliminado" se revoca el acceso al portal)
- Asignación de Proyectos (relación N:N)
- Asignación de Categorías (relación N:N)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

### 6.2 Alta de Usuarios

- Se da de alta proporcionando el correo electrónico del usuario.
- El Administrador puede aceptar o rechazar solicitudes de acceso.
- Al aceptar, se asignan permisos mínimos (rol de Usuario).

### 6.3 Solicitudes de Acceso

- Los usuarios no registrados ven una página con opción de solicitar acceso.
- Las solicitudes se almacenan en la base de datos.
- Los Administradores tienen una sección para visualizar solicitudes pendientes y aceptar o rechazar.

---

## 7. Registro de Actividades

### 7.1 Campos de una Actividad

- Proyecto (relación, obligatorio — solo proyectos asignados al usuario)
- Fecha de la actividad (date, obligatorio)
- Tiempo invertido en horas (decimal, permite fracciones, obligatorio)
- Categoría (relación, obligatorio — solo categorías asignadas al usuario)
- Facturable al cliente (booleano, obligatorio)
- Descripción (texto, máximo 100 caracteres, obligatorio)
- Notas adicionales (texto, máximo 500 caracteres, opcional)
- Creado por (usuario, automático)
- Fecha de creación (UTC, automático)

### 7.2 Reglas de Negocio

- Un usuario no puede superar las 8 horas productivas por día. Si desea capturar más, las horas adicionales deben clasificarse en la categoría **0:Tiempo Adicional**.
- Los días laborables son de lunes a viernes. Horas capturadas en sábados y domingos deben clasificarse como categoría **0:Tiempo Adicional**.
- Los días festivos **quedan fuera del alcance** del sistema; se tratan como días normales de lunes a viernes.
- El usuario solo puede capturar actividades en las categorías y proyectos que le fueron asignados.
- Si al usuario ya no le quedan horas restantes en el día (completó 8 horas), solo se le permite capturar en la categoría **0:Tiempo Adicional**.

### 7.3 Bloqueo por Mes Cerrado

- Las actividades de un mes cerrado **no pueden ser editadas ni eliminadas**.
- El cierre de mes bloquea la edición de todos los registros cuya fecha de actividad pertenezca a ese mes.
- Solo los administradores pueden cerrar el mes en la sección de administración.

---

## 8. Favoritos

### 8.1 Descripción

Los favoritos permiten al usuario guardar combinaciones frecuentes de datos para agilizar la captura de actividades.

### 8.2 Almacenamiento

- Los favoritos se **persisten en el backend** asociados al usuario (no en localStorage del navegador).
- Esto permite que el usuario acceda a sus favoritos desde cualquier dispositivo o navegador.

### 8.3 Campos de un Favorito

- Nombre (texto, máximo 20 caracteres, obligatorio)
- Datos del formulario de actividad precargados (proyecto, categoría, facturable, descripción, notas)
- **Excluye:** fecha de la actividad y horas (se toman del contexto al momento de usar el favorito)

### 8.4 Reglas

- En la pantalla de registro de actividades se muestran los últimos 5 favoritos como listado horizontal más un ícono "+".
- Al hacer clic en un favorito, se precarga el formulario con los datos almacenados excepto la fecha (que se obtiene del contexto actual).
- El usuario puede administrar sus favoritos: ver listado, eliminar individualmente o eliminar todos.

---

## 9. Flujos de Usuario

### 9.1 Registro de Actividades (Vista Calendario)

1. El usuario entra a la opción del menú "Actividades".
2. Ve un calendario del mes en curso con un badge en cada día indicando las horas faltantes para completar 8 horas reglamentarias (solo lunes a viernes).
3. El usuario da clic en el día que desea capturar.
4. Es dirigido a la pantalla de Registro de Actividades del día, donde visualiza:
    - Una tabla con los registros del día seleccionado.
    - Un listado horizontal de favoritos (últimos 5 + ícono "+").
    - Un indicador de horas restantes para completar las 8 horas. Para sábados y domingos, las horas requeridas son 0.
5. El usuario puede cambiar de fecha con un datepicker en la parte superior.
6. El botón "Regresar" redirige a la pantalla del calendario de Actividades.

### 9.2 Captura de Nueva Actividad

1. El usuario da clic en el botón "Nuevo".
2. Aparece un formulario solicitando: proyecto, categoría, horas, facturable, descripción, notas.
3. En la parte inferior del formulario se muestran las horas restantes del día.
4. Si ya no hay horas restantes, solo se permite la categoría **0:Tiempo Adicional**.
5. Opciones al completar el formulario:
    - **Aceptar:** Guarda el registro, cierra el formulario, regresa al listado mostrando la nueva entrada.
    - **Capturar:** Guarda el registro, limpia el formulario para una siguiente captura, actualiza las horas restantes. No cierra el formulario.
    - **Agregar a Favoritos:** Solicita un nombre (máx. 20 caracteres) y guarda los datos del formulario como favorito en el backend.
    - **Cancelar** (o clic fuera del formulario): Descarta la información, regresa al listado.

### 9.3 Registro de Actividades por Favoritos

1. El usuario visualiza el listado horizontal de favoritos (últimos 5 + ícono "+").
2. Al hacer clic en un favorito, se abre el formulario de registro con datos precargados (excepto la fecha, que se toma del contexto).
3. A partir de aquí se sigue el flujo de Captura de Nueva Actividad (§9.2).

### 9.4 Administración de Favoritos

1. El usuario da clic en el ícono de administrar favoritos.
2. Aparece un formulario con el listado de los nombres de favoritos y un botón de acción para eliminar cada entrada.
3. En la parte superior hay un botón "Eliminar" para eliminar todas las entradas.
4. Al dar clic en "Aceptar" o fuera del formulario, este se cierra.

### 9.5 Vista por Tabla (Actividades)

1. Desde la pantalla de Actividades, el usuario cambia a vista de tabla con un ícono.
2. Aparece un buscador, filtros y un botón exportar.
3. Filtros disponibles: Categoría, Periodo de Tiempo, Proyecto, Usuario que creó el registro.
4. Los filtros se combinan entre sí. La información se filtra desde el backend.
5. La información se muestra paginada (50 registros por página).

### 9.6 Exportar Actividades (CSV)

1. Disponible en la vista por tabla.
2. Al dar clic en "Exportar", se solicita al backend un archivo CSV con los parámetros de búsqueda y filtrado activos.
3. El CSV usa valores descriptivos (no IDs) para Plataforma, Proyecto, Usuario, etc.
4. **Alcance por rol:**
    - **Usuario:** Solo puede exportar sus propias actividades.
    - **Líder:** Puede exportar las actividades de los usuarios de su equipo (proyectos asignados).
    - **Administrador / Superusuario:** Puede exportar todas las actividades.

### 9.7 Registro de Divisiones

1. El Administrador ingresa a "Divisiones" en el menú.
2. Ve un listado en forma de tarjetas con: descripción, estatus, íconos de acciones (editar, eliminar si es Borrador, inactivar).
3. Buscador en la parte superior para filtrar. Paginación desde el backend (50 registros por página).
4. Botón "Nuevo" abre formulario de captura. El usuario indica si el registro nace activo o en borrador.
5. "Aceptar" guarda y redirige al listado de **Divisiones**. "Cancelar" o clic fuera descarta datos.

### 9.8 Registro de Plataformas

1. El Administrador ingresa a "Plataformas" en el menú.
2. Ve un listado en forma de tarjetas con: descripción, División asociada, estatus, íconos de acciones.
3. Buscador y paginación desde el backend (50 registros por página).
4. Botón "Nuevo" abre formulario. El usuario selecciona la División y el estatus inicial.
5. "Aceptar" guarda y redirige al listado de **Plataformas**. "Cancelar" o clic fuera descarta datos.

### 9.9 Registro de Proyectos

1. El Administrador o Líder ingresa a "Proyectos" en el menú.
2. Ve un listado en forma de tarjetas con: descripción, Plataforma, División (informativo), estatus, íconos de acciones.
3. Buscador y paginación desde el backend (50 registros por página).
4. Botón "Nuevo" abre formulario. El usuario selecciona la Plataforma y visualiza la División como dato informativo. Indica el estatus inicial.
5. "Aceptar" guarda y redirige al listado de **Proyectos**. "Cancelar" o clic fuera descarta datos.

### 9.10 Registro de Categorías

1. El Administrador ingresa a "Categorías" en el menú.
2. Ve un listado en forma de tarjetas con: descripción, estatus, íconos de acciones.
3. Buscador y paginación desde el backend (50 registros por página).
4. Botón "Nuevo" abre formulario. El usuario indica el estatus inicial.
5. "Aceptar" guarda y redirige al listado de **Categorías**. "Cancelar" o clic fuera descarta datos.

### 9.11 Asignación de Categorías a Usuarios

1. El Administrador o Líder ingresa a "Categorías".
2. Da clic en el botón de edición de una categoría.
3. Se abre un formulario mostrando: Código (ID), Nombre (editable), Estatus (editable).
4. Muestra un listado de usuarios asignados a esa categoría.
5. Incluye una búsqueda para seleccionar y agregar usuarios al listado (se registran en BD).
6. Se pueden eliminar usuarios del listado (se remueven de BD).

### 9.12 Asignación de Proyectos a Usuarios

1. El Administrador o Líder ingresa a "Proyectos".
2. Da clic en el botón de edición del proyecto.
3. Se abre un formulario mostrando: Código (ID), División (informativo), Plataforma (informativo), Nombre, Estatus.
4. **Permisos de edición:**
    - **Superusuario y Administrador:** Pueden modificar el nombre del proyecto y el estatus.
    - **Líder:** Solo puede modificar el estatus.
5. Muestra un listado de usuarios asignados al proyecto.
6. Incluye una búsqueda para seleccionar y agregar usuarios (se registran en BD).
7. Se pueden eliminar usuarios del listado (se remueven de BD).

---

## 10. Reglas Transversales

### 10.1 Paginación

- Todos los listados están paginados con **50 registros por página**.
- El filtrado y la paginación se procesan desde el backend.

### 10.2 Auditoría

- Todos los registros incluyen quién y cuándo creó el registro.
- Internamente se manejan fechas en **UTC**.
- En el frontend se muestran las fechas según la región del navegador del usuario.

### 10.3 Acciones en Listados

- Todos los catálogos presentan listados con columnas/íconos de acciones.
- Las acciones están habilitadas/deshabilitadas según el rol del usuario logueado.
- Los botones con acciones de eliminar deben presentar un estilo destacable (color de advertencia).

---

## 11. Tareas Adicionales

- [ ] Incluir manual de configuración de Azure Entra ID en README.md (formato markdown).
- [ ] Incluir manual de usuario final con navegación y conceptos básicos (formato PDF).

---

## 12. Registro de Correcciones Aplicadas

Este documento incorpora las siguientes correcciones respecto al documento original (Directivas.md):

1. **§5.3 Proyectos — Reglas:** El original decía "crear nuevas Divisiones y modificar los nombres de las divisiones" — corregido a "Proyectos".
2. **§5.3 Proyectos — Reglas:** El original decía "inhabilitar las Divisiones" — corregido a "Proyectos".
3. **§9.9 Registro de Proyectos:** El original decía "listado de las Plataforma en forma de tarjetas" — corregido a "Proyectos".
4. **§9.9 Registro de Proyectos:** El original redirigía "al listado de Plataformas" — corregido a "Proyectos".
5. **§9.10 Registro de Categorías:** El original redirigía "al listado de Plataformas" — corregido a "Categorías".
6. **§9.12 Asignación de Proyectos:** El original decía "superadministradores y usuarios" pueden modificar el nombre — corregido a "Superusuario y Administrador" según aclaración del stakeholder.
7. **§3.1 Superusuario:** Se formalizó la regla de acceso implícito total para evitar inconsistencias de mención.
8. **§5.6 Clientes:** Se confirmó que solo tiene 3 estatus (sin Eliminado) — documentado como excepción explícita.
9. **§7.3 Bloqueo por mes:** Se agregó la definición de bloqueo por mes cerrado para actividades (no existía en el original).
10. **§8 Favoritos:** Se cambió el almacenamiento de localStorage del navegador a persistencia en backend.
11. **§5.4 Categoría 0:Tiempo Adicional:** Se formalizó como seed data asignada a todos los usuarios por defecto.
12. **§9.6 Exportar CSV:** Se definió el alcance por rol (no existía en el original).
13. **§10.1 Paginación:** Se definió el tamaño estándar de 50 registros por página.
14. **§7.2 Días festivos:** Se documentó explícitamente que quedan fuera del alcance.
15. **§7.2 Regla de Líder:** La regla original "Si el usuario es un Líder." estaba incompleta/cortada — pendiente de definición detallada por el stakeholder.