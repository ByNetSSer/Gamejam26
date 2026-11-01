---
tags: [unity, menu, pausa, estado]
estado: inventario del código actual
actualizado: 2026-10-31
---

# Pausa de gameplay: estado actual

Este documento registra el comportamiento existente y servirá para anotar las próximas decisiones. El código y la escena prevalecen si cambian; no presupone nuevas funciones.

## Dónde funciona y cómo se crea

`GameplayPauseMenu` se instala automáticamente al cargar una escena llamada `e`. El menú principal tiene configurada `e` como escena de partida. La escena `Gameplay` no recibe este menú automático. El objeto `Menú de pausa` se crea en ejecución; la presentación también se construye por código la primera vez que se pausa, por lo que sus elementos no están materializados en la escena para editarlos a mano.

## Controles y presentación

- Escape abre o cierra el menú si existe un teclado. Solo permite abrirlo si `Time.timeScale` es mayor que cero; Escape vuelve a cerrar cuando la pausa está activa.
- El menú provisional tiene un fondo oscuro, el título `PAUSA` y dos botones: `CONTINUAR` y `MENÚ PRINCIPAL`. No contiene opciones, créditos, reinicio, inventario ni otros paneles.
- `CONTINUAR` reanuda. `MENÚ PRINCIPAL` comprueba que la escena esté disponible, reanuda y pide a `SceneLoader` que cargue `MainMenu`.
- La UI es uGUI con texto legacy, `CanvasScaler` de referencia 1920 × 1080 y orden de dibujo 95. Crea un `EventSystem` con `InputSystemUIInputModule` si no existe y selecciona el botón de continuar al abrir.

## Tiempo, entrada y audio

Al abrir, el controlador guarda `Time.timeScale` y el estado de bloqueo y visibilidad del cursor; marca `GameplayPauseMenu.IsPaused`, pone la escala en cero y muestra y libera el cursor. Al continuar restaura los valores guardados y oculta el menú. Si el controlador se destruye estando pausado, restaura la escala guardada. `SceneLoader` establece la escala en 1 al iniciar una carga.

El tiempo escalado se detiene, incluido el contador de torre y los sistemas que usan `Time.deltaTime`. La cámara comprueba explícitamente `IsPaused`; el tutorial detiene sus transiciones mientras está pausado. El efecto de tiempo del parry tiene una comprobación de pausa y usa tiempo no escalado para su presentación. La pantalla de resultado también pone la escala en cero y usa su propia UI y estado: probar las interacciones entre ambos sistemas antes de cambiarlos.

La pausa no cambia globalmente `AudioListener.pause` ni pausa todas las fuentes. Algunos sistemas, como los pasos, responden a `deltaTime`; la música y los sonidos gestionados por otras fuentes pueden continuar. Comprobar el comportamiento auditivo real en Play Mode antes de decidir cambios.

## Otro controlador existente

`MenuUIBase.UI.PauseController` es un componente genérico distinto con un panel asignable y métodos `Pause`, `Resume` y `TogglePause`. Gestiona `timeScale`, pero no el cursor ni el estado estático de `GameplayPauseMenu`. No se encontró una instancia serializada en las escenas de gameplay consultadas. Evitar activar ambos a la vez sin coordinar sus estados.

## Próximas decisiones

Espacio reservado para anotar lo que pida el equipo: diseño visual, contenido, navegación, controles, audio y comportamiento de la partida. No hay cambios de diseño de pausa aprobados todavía.

## Comprobaciones pendientes

Probar abrir y cerrar con Escape y con Continuar, volver al menú, pausar durante el tutorial y durante un parry, intentar pausar en pantalla de resultado, revisar cursor, mando/teclado y sonido, y comprobar la escena de partida realmente usada. La inspección de código no sustituye estas pruebas en Play Mode.
