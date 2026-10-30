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
| `RunWonEvent` | El jugador activa el botón de victoria después de completar la última torre. | Total de salas de esa torre y puntuación. |
| `TowerGenerationFailedEvent` | Falta configuración o no se puede preparar una secuencia. | Mensaje e índice alcanzado; no cuenta como derrota del jugador. |

**Completar una torre no implica ganar toda la partida:** se emite `TowerCompletedEvent`. Tras completar la torre 12, la zona segura final muestra solo un botón de victoria; al mirarlo y pulsar E se llama a `RoomManager.DeclareRunVictory()` y se emite `RunWonEvent` una sola vez. El límite se configura en `Total Towers` del gestor y no permite iniciar otra torre. Todavía no hay pantalla de victoria ni reinicio de partida.

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

Los listeners que necesiten el evento inicial deben suscribirse antes del `Start` del gestor; no se vuelven a emitir eventos pasados al suscribirse. `DeclareRunVictory()` está protegida contra llamadas antes de completar la última torre, duplicados y una derrota previa.

## Verificación

Runtime y editor compilan. Se ejecutaron cinco comprobaciones aisladas del canal: orden de eventos anidados, cancelación de suscripción, aislamiento de excepciones, independencia entre canales y limpieza. Falta probar en Play Mode los eventos reales al avanzar, perder vida, terminar la torre y conectar la UI o ítems.

## Ciclo de zonas seguras en la escena e

La escena `e` usa `ZonaSegura` como sala inicial y final. El inicio de la primera torre muestra la sala y la trampilla de salida; los altares quedan ocultos. En el final de las torres anteriores a la última, `TowerRewardStation` usa los tres altares existentes para ofrecer mejoras con precios y permite una renovación gratuita de las ofertas por torre. El botón inicia la torre siguiente sin exigir una compra. En el final de la última torre los altares se ocultan y solo aparece el botón de victoria. Se conservan los efectos en `PlayerStats` durante la partida.

En las torres pares (2, 4, 6…) aparecen tres maldiciones provisionales en la zona inicial. El jugador debe elegir una gratis con E para desbloquear la trampilla; sus modificadores persisten. La selección incluye al menos una maldición de enemigo cuando hay alguna disponible: la primera elección incorpora al enemigo y las posteriores aumentan la cantidad de Flupin o máscaras, o la frecuencia de Dontsee. Los valores de las maldiciones son provisionales. `TowerDifficulty` aplica los valores configurados en la escena: en `e`, una sala adicional por torre, un 2 % aditivo de velocidad por torre y un 12 % relativo a las probabilidades de aparición por torre (limitadas al 100 %). El crecimiento de velocidad por sala sigue siendo independiente. `RoomAutoLayout.Size` distingue salas pequeñas, medianas y grandes; las probabilidades de cada tamaño se configuran por separado.

La sala inicial y la primera intermedia (índices 1 y 2) no activan enemigos ni maldiciones de suelo. Al avanzar se conservan vida, puntuación, monedas, mejoras y progreso de velocidad por salas. Las conexiones, recompensas, renovación y reinicios requieren validación completa en Play Mode; los proyectos generados por Unity deben regenerarse para incluir los scripts nuevos.

## Salas medianas y curva actual en `e`

Las seis variantes de `PrefabsMapas/Medio` tienen entrada y salida de trampilla; se identifican por sus superficies `EntradaT` y `SalidaT` o `SalidaT-P`. Se incluyen en el pool T-T. Las torres 1–3 utilizan salas pequeñas; desde la 4 la probabilidad de escoger una mediana entre las salas T-T compatibles empieza en 10 % y sube 7 puntos por torre, hasta 66 % en la torre 12. Las demás combinaciones siguen usando las salas pequeñas compatibles. La selección y las conexiones deben comprobarse en Play Mode.

En `e` hay 12 torres; la primera tiene 4 salas y se añade una por torre, hasta 15. El temporizador base se mantiene en 180 s y suma 12 s por cada mediana que se prepara, para compensar su recorrido más largo. La probabilidad de los puntos de contenido de las medianas se multiplica por 0,65 antes de aplicar sus límites, ya que tienen más puntos. Las probabilidades, tiempos y longitud son valores iniciales de balance ajustables en el gestor; requieren medir partidas reales.
