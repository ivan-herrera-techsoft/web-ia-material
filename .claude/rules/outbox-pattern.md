---
description: Convenciones para el patrón Outbox — entrega confiable de notificaciones y eventos con Bisoft.NotificationBus
globs: "**/BackgroundServices/**,**/Entities/**,**/Domain/**,**/Application/**"
---

## Propósito del patrón Outbox

El patrón Outbox garantiza que las notificaciones (email, Telegram, SMS, HTTP) se envíen **exactamente una vez** incluso si el proceso falla después de confirmar un cambio en BD. La notificación se almacena en la misma transacción que la operación de negocio; un Background Service la procesa de forma asíncrona.

---

## Cuándo usar Outbox

Usar el patrón Outbox para **cualquier efecto secundario fuera de la BD principal**:
- Envío de email (confirmación, alerta, reporte)
- Mensajes de Telegram, SMS, WhatsApp
- Llamadas HTTP a APIs externas (webhooks)
- Publicación de eventos a otras aplicaciones

**Nunca enviar notificaciones directamente desde Application Service ni Domain Service.** Si falla el envío, la operación de negocio ya está confirmada y el evento se pierde.

---

## Paquete Bisoft.NotificationBus

El paquete `Bisoft.NotificationBus` proporciona:
- `INotificationBus` — interfaz para encolar notificaciones
- `NotificationChannel` — enum de canales: `Email`, `Telegram`, `Sms`, `Http`
- `NotificationMessage` — DTO con destinatario, asunto, cuerpo y canal
- Background Service base para procesar la cola pendiente

---

## Flujo de implementación

```
Application Service
  └─ guarda entidad en BD
  └─ encola notificación en tabla Outbox (misma transacción)
       └─ [commit]
            └─ Background Service (timer)
                 └─ lee pendientes de Outbox
                 └─ envía via INotificationBus
                 └─ marca como procesado
```

---

## Application Service — encolar en Outbox

El Application Service usa `INotificationBus` para encolar (no enviar directamente):

```csharp
public class CanalService(
    ICanalDomainService domainService,
    INotificationBus notificationBus)
{
    public async Task<CrearCanalResponse> Guardar(CrearCanalRequest solicitud, CancellationToken ct)
    {
        var canal = await domainService.Guardar(solicitud.Nombre, solicitud.Descripcion, ct);

        // Encolar en Outbox — misma transacción que el Guardar del Domain Service
        await notificationBus.Encolar(new NotificationMessage
        {
            Canal = NotificationChannel.Email,
            Destinatario = solicitud.EmailContacto,
            Asunto = "Canal creado",
            Cuerpo = $"El canal '{canal.Nombre}' fue creado exitosamente."
        }, ct);

        return canal.Adapt<CrearCanalResponse>();
    }
}
```

---

## Background Service — procesar Outbox

El procesador de Outbox hereda de `TimedBackgroundService` (ver `rules/background-services.md`):

```csharp
public class OutboxProcessorBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxConfiguration> opciones,
    LoggerWrapper<OutboxProcessorBackgroundService> logger)
    : TimedBackgroundService(opciones.Value, logger)
{
    protected override async Task ExecuteAutomatedTask(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<INotificationBus>();
        await bus.ProcesarPendientes(ct);
    }
}
```

---

## Reglas

- **Atómico**: el encolado en Outbox DEBE estar en la misma transacción que la operación de negocio
- **Idempotente**: el procesador marca cada notificación como procesada tras el envío exitoso
- **Reintentos**: configurar máximo de reintentos y tiempo de espera entre intentos en `appsettings.json`
- **Nunca**: llamar a `INotificationBus.Enviar()` directamente desde Domain Service
- **Nunca**: enviar notificaciones síncronas en el hilo del request
