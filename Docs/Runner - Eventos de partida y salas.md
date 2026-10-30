---
tags: [gamejam, unity, arquitectura, eventos, salas]
estado: prototipo
---
# 📣 Eventos de torre, salas y partida

> [!abstract] Objetivo
> Los sistemas futuros pueden reaccionar a la torre sin que el gestor conozca la interfaz, audio, ítems, logros u otros receptores. Cada `RoomManager` tiene su propio `GameEventBus`: no hay singleton ni eventos estáticos compartidos.

## Eventos disponibles

| Evento | Cuándo ocurre | Datos principales |
| --- | --- | --- |
| `TowerStartedEvent` | La configuración es válida y se prepara la torre. | Número total de salas y jugador. |
| `RoomEnteredEvent` | Se entra a la sala inicial o se confirma una transición. | `RoomContext`: sala, índice desde 1, total y tipo inicial/intermedia/final. |
| `RoomExitedEvent` | Se abandona una sala por avance, victoria de torre o derrota. | Contexto y motivo `Advanced`, `Completed` o `Defeated`. |
| `TowerCompletedEvent` | El jugador entra y toca el suelo de la sala final. | Total de salas y puntuación. |
| `RunLostEvent` | `PlayerHealth` llega a cero durante la torre. | Sala actual, puntuación y motivo `PlayerHealthDepleted`. |
| `RunWonEvent` | Un director futuro llama `DeclareRunVictory()` tras completar la torre final de la partida. | Total de salas de esa torre y puntuación. |
| `TowerGenerationFailedEvent` | Falta configuración o no se puede preparar una secuencia. | Mensaje e índice alcanzado; no cuenta como derrota del jugador. |

**Completar una torre no implica ganar toda la partida:** se emite `TowerCompletedEvent`, y `RunWonEvent` solo se emite si un director llama expresamente a `RoomManager.DeclareRunVictory()`. Actualmente no existe ese director ni una condición final de victoria; la torre de prueba se detiene al completarse. La derrota detiene el controlador y muestra estado provisional; aún no hay pantalla de fin, reinicio o transición a tienda.

## Orden esperado

- Inicio: `TowerStartedEvent` → `RoomEnteredEvent` de la inicial.
- Avance: `RoomExitedEvent(Advanced)` de la anterior → se cuenta su progreso → `RoomEnteredEvent` de la nueva.
- Fin de torre: se cuenta la sala final → `RoomExitedEvent(Completed)` → `TowerCompletedEvent`.
- Sin vida: `RoomExitedEvent(Defeated)` → `RunLostEvent`. No se emite la victoria de torre ni la de partida.

Cada salida se publica una sola vez. Si un listener publica un evento mientras se procesa otro, el canal lo encola hasta terminar de informar a los listeners del evento actual. Una excepción de un listener se registra y no impide notificar a los otros. Los eventos se procesan en el hilo principal cuando el emisor los publica; no son un sistema de eventos multihilo ni guardan historial.

`RoomContext.Room` es una referencia temporal a la instancia: al continuar, la sala puede desactivarse y destruirse. Para guardar estadísticas históricas usar `Index`, `TotalRooms` y `Kind`, no depender de esa referencia después de la notificación.

## Cómo escuchar

Suscribirse preferiblemente en `OnEnable` y liberar en `OnDisable`; esto evita que un objeto deshabilitado siga recibiendo eventos. Asignar el gestor por inspector o inyección, sin buscar un singleton.

```csharp
using System;
using UnityEngine;

public sealed class EjemploEventoSala : MonoBehaviour
{
    [SerializeField] RoomManager manager;
    IDisposable subscription;

    void OnEnable()
    {
        if (manager != null)
            subscription = manager.Events.Subscribe<RoomEnteredEvent>(OnRoomEntered);
    }

    void OnRoomEntered(RoomEnteredEvent e)
    {
        Debug.Log($"Entró a la sala {e.Context.Index}");
    }

    void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
```

Los listeners que necesiten el evento inicial deben suscribirse antes del `Start` del gestor; no se vuelven a emitir eventos pasados al suscribirse. Para una victoria final, un director futuro escucha `TowerCompletedEvent`, decide si hay más torres y llama a `DeclareRunVictory()` solo si corresponde. La llamada está protegida contra duplicados y contra una derrota previa.

## Verificación

Runtime y editor compilan. Se ejecutaron cinco comprobaciones aisladas del canal: orden de eventos anidados, cancelación de suscripción, aislamiento de excepciones, independencia entre canales y limpieza. Falta probar en Play Mode los eventos reales al avanzar, perder vida, terminar la torre y conectar la UI o ítems.

## Ciclo provisional de recompensas en la escena e

Al completar la torre, `TowerRewardStation` crea tres pilares dentro de la sala final: dos opciones comunes distintas y una de nivel superior. Sus representaciones y efectos son provisionales; se elige una gratis con E y el botón situado al otro lado de la sala inicia la torre siguiente después de elegir. Los efectos se conservan en `PlayerStats` durante la partida. El catálogo de `ItemDefinition` sigue siendo descriptivo y no se conecta automáticamente a estos efectos; el inspector admite un sprite común y un modelo excepcional cuando estén disponibles.

`TowerDifficulty` mantiene el número inicial de salas configurado y añade una sala por torre hasta 20, 0,5 de velocidad inicial por torre y un 10 % relativo a las probabilidades de los puntos de aparición de objetos por torre (limitadas al 100 %). `RoomAutoLayout.Size` permite marcar salas grandes; la probabilidad de elegirlas crece un 10 % por torre cuando hay salas grandes compatibles. Las salas actuales son pequeñas y se usan como alternativa mientras no haya grandes. Los valores son experimentales, no balance final. No hay monedas ni compras en esta versión.

Al avanzar se conservan vida, puntuación, mejoras y progreso de velocidad por salas. La torre inicial y la final siguen usando los prefabs existentes. Las nuevas conexiones, recompensas y reinicios requieren validación completa en Play Mode; los proyectos generados por Unity deben regenerarse para incluir los scripts nuevos.
