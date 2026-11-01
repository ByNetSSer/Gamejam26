---
tags: [unity, menu, pausa, estado]
estado: implementación provisional editable
actualizado: 2026-10-31
---

# Pausa de gameplay

La escena `e`, que carga el menú principal, contiene una instancia editable de `Assets/Proyect/Assets/Tutoriales/Menu pausa.prefab`. `GameplayPauseMenu` está en su raíz; ya no construye el menú en ejecución. Escala y posición del hacha, zonas de clic, papel, botones y controles se editan en el prefab o como overrides de la escena.

## Funcionamiento

Escape bloquea las acciones e inicia una reducción progresiva de `Time.timeScale` hasta cero. La transición usa un `LocalTimeScale` de Magic Time como valor y lo conecta al tiempo global que usan los sistemas existentes. Después aparece el hacha desde la izquierda mediante rotación; el cursor queda libre. La duración del frenado y los ángulos y duración del hacha son ajustables en el controlador. El hacha contiene zonas de clic y sprites independientes sobre las palabras dibujadas: Reanudar, Opciones y Exit; Exit vuelve al menú principal. La palabra Menú es decorativa y no es interactiva. Cada sprite es un hijo editable del objeto correspondiente.

Reanudar rota el hacha de vuelta antes de restaurar la escala y el cursor anteriores. Opciones baja el papel; Volver o Escape lo suben y devuelven el control al hacha. El papel contiene controles para volumen general, música, efectos y sensibilidad, conectados a `AudioManager` y `GameSettings`. Las posiciones de inicio/final y duración del papel también son ajustables. La UI usa tiempo no escalado y bloquea los botones durante cada transición.

La muerte deshabilita los controles inmediatamente, hace una ralentización similar mediante Magic Time y después muestra la pantalla de resultado existente. `deathSlowDuration` se ajusta en `RunEndScreen`. Si sucede durante la entrada a pausa, se cancela la transición de pausa y prevalece la muerte. La victoria conserva su detención inmediata.

## Límites y validación

La escena `Gameplay` alternativa no incluye el prefab de pausa; la escena principal configurada es `e`. La pausa no modifica globalmente `AudioListener.pause`, de modo que el comportamiento de música y fuentes independientes requiere escucha. El efecto de parry puede alterar el tiempo y se conserva la escala previa de pausa; comprobar especialmente pausas y muerte durante ese efecto. Probar en Play Mode todos los botones, los sliders, Escape, entrada/salida, tutorial, cursor, opciones y vuelta al menú, además del frenado previo a la muerte. Las posiciones de los sprites y de sus zonas de clic requieren ajuste visual en Play Mode.
