# Guía del proyecto

Lee esta guía al comenzar trabajo en el proyecto. Para integrar FEEL/Odin y consultar versiones, dependencias y eventos disponibles, revisa `Docs/Runner - Herramientas y game feel.md`. El GDD actualizado para el equipo está en `Docs/GDD - Torre medieval magica.md` y el brief de modelado en `Docs/Brief de arte - Salas y recursos.md`. Para detalles y estado más amplio consulta `Docs/Runner - Movimiento y sistemas.md` y `Docs/Runner - Estadisticas efectos y eventos.md` y `Docs/Runner - Combo y puntuacion.md` y `Docs/Runner - Primera persona y estadisticas.md` y `Docs/Runner - Movimiento y superficies.md` y `Docs/Runner - Salas y generacion.md` y `Docs/Runner - Contrato de salas y marcadores.md` y `Docs/Runner - Pools final y puertas.md` y `Docs/Runner - Eventos de partida y salas.md` y `Docs/Runner - Orientacion de salas.md` y `Docs/Runner - Flupin y enemigos.md`; verifica siempre el código actual, pues el prototipo puede evolucionar.

## Visión y alcance

Game jam de tres días: runner 3D con estética pixel art y tema **Out of Control**. El jugador desciende por salas, acumula velocidad y puntos, y conserva decisiones aunque la inercia y el tiempo de reacción vuelvan más difícil controlarse. Son importantes salto, pisotón, destructibles, combo, peligros, una pelota atraída al jugador e ítems con ventajas o desventajas. No asumir que todas estas ideas están implementadas.

El diseño previsto usa salas prefabricadas semiprocedurales, conectadas hacia delante o hacia abajo, sin retorno: se mantienen como máximo la anterior, la actual y la siguiente. Al acabar una torre se compran mejoras con monedas y la siguiente es más larga y rápida. La perspectiva es en primera persona; la derrota y varios detalles de progresión aún deben definirse con el usuario.

## Código actual

- `Assets/Proyect/Assets/Scripts/Player`: movimiento de prueba en primera persona, cámara, impactos, estadísticas del jugador y activadores de efectos. `RegisterRoomCleared()` actualiza la progresión y lo invoca el gestor de salas al confirmar la transición.
- `Assets/Proyect/Assets/Scripts/Systems/Events`: `EventStream` y `GameEventBus` locales con suscripciones tipadas; eventos de ciclo de sala/torre en `Rooms/GameEvents.cs`.
- `Assets/Proyect/Assets/Scripts/Systems/Stats`: cálculo reutilizable de estadísticas y tipos de modificador.
- `Assets/Proyect/Assets/Scripts/Systems/Timers`: temporizador y scheduler compartido del PlayerLoop; el tiempo usa `Time.deltaTime`.
- `Assets/Proyect/Assets/Scripts/Systems/Effects`: efecto reutilizable que añade y retira un modificador a una estadística.
- `Assets/Proyect/Assets/Scripts/Systems/Scoring`: estado de combo y datos de puntuación; `PlayerScore` conecta estadísticas, puntos y eventos, y `ScoreDebugView` muestra un HUD temporal.
- `Assets/Proyect/Assets/Scripts/Rooms`: gestor de tres salas activas, marcadores vacíos puerta/trampilla y puntos de aparición opcionales por prefab. La primera sala solo necesita salida de trampilla y posición inicial; la final tiene entrada por trampilla. Las cuatro combinaciones intermedias usan pools separados. Al bajar se puede girar la sala inferior en incrementos de 90° con `Hatch Rotations`; las puertas alinean directamente. Las notas de marcadores actualizan la guía de generación anterior. Las puertas se abren al mirar y pulsar E. La torre cuenta inicial y final y tiene final provisional sin tienda ni siguiente torre.
- `Assets/Proyect/Assets/Scripts/Enemies`: `Flupin`, acercamiento en espiral desde lejos en espacio del mundo, repulsión con G, eventos tipados de disponibilidad/golpe, estadísticas modificables por instancia y coordinador de turnos/condiciones; conservar el material y shader asignados por el usuario; el Flupin de demostración usa FEEL 5.5.
- `Assets/Proyect/Assets/Scripts/Environment`: superficies de prueba consultadas bajo los pies por `GroundSurfaceSensor`; lava usa `PlayerHealth`.
- `Assets/Proyect/Assets/Scripts/Items`: definiciones de efectos de estadísticas y pickups de prueba; aún no hay inventario ni tienda conectados.
- `Assets/Proyect/Assets/Scripts/Rendering`: pixelado selectivo existente y feature URP de Flupin que dibuja la capa `FlupinOverlay` sobre el escenario con el material original; respetar su configuración.
- `Assets/Proyect/Assets/Scripts/Editor`: creación de escena de prueba y verificaciones manuales.

Los sistemas genéricos deben seguir independientes de las reglas del jugador; usa nombres que describan su responsabilidad, sin añadir el prefijo `Runner` a clases genéricas. Conserva los `.meta` al mover o renombrar assets para mantener sus GUID y referencias en Unity.

## Estado y validación

- Menú y audio: `Assets/Scenes/MainMenu.unity` carga `Gameplay`; `Assets/MenuUIBase/Runtime` incluye navegación, servicios persistentes, pool de 24 efectos y ajustes de volumen/sensibilidad en PlayerPrefs. `Assets/Proyect/Assets/Audio` contiene banco de SFX conectados mediante `GameSfx`, incluido ambiente 3D de sala de brea, cierre y cobro del combo por daño y transición sonora de muerte; hielo, entrada/salida de lava y pasos por superficie tienen cues preparados sin clips; el trueno queda como efecto independiente disponible. Música de menú/gameplay asignada, listas con fundido cruzado de 4 s y prioridades; listas de salas seguras (inicio/tienda), intensa (desde sala 3) y último minuto preparadas sin canciones en `Audio/Resources/Music`. Ver `Docs/Runner - Menu audio y ajustes.md` para integración y límites; falta validación visual/interactiva. El master aún no controla AudioSources externos/FEEL.

El movimiento, impactos y efectos son prototipos, no sistemas finales. El barrido anticipado rompe los objetos que no resistirían el impacto antes del subpaso; conserva el bloqueo de obstáculos resistentes. Los subpasos no garantizan detección a cualquier velocidad; probar en Unity con obstáculos finos y tirones. La puntuación y combo tienen una base experimental; faltan su UI final y balance. Hay vida y daño de lava básicos: al llegar a cero se emite derrota y se detiene el jugador, pero no hay pantalla ni reinicio. El gestor de salas es experimental. Flupin es el primer enemigo prototipo; no están implementados vida/daño completo, tienda ni coordinación de trampas con enemigos. `PlayerEvents.NotifyDamage` necesita una llamada del futuro sistema de vida.

En Unity, `Tools > Runner > Crear torre de prueba` genera prefabs y escenario para probar las salas; `Tools > Runner > Crear escena de prueba` prepara una escena temporal; `Tools > Runner > Verificar estadísticas y efectos` y `Tools > Runner > Verificar conexiones de salas` preparan comprobaciones fuera de Play Mode. La compilación de runtime y editor pasó y el cálculo puro de combo se probó; la escena actual en primera persona aún requiere prueba; las verificaciones del editor y las pruebas en Play Mode siguen pendientes. Si los `.csproj` generados por Unity quedan desactualizados después de mover scripts, regenerarlos desde Unity o verificar con proyectos temporales; no asumir que un build de un proyecto obsoleto cubre los archivos nuevos.

## Documentación de diseño

La bóveda de Obsidian detectada está en `C:/Users/bynet/Documents/Obsidian Vault`. Hay notas equivalentes a las de `Docs` con decisiones y pendientes; pueden haber cambiado por separado, así que revisar antes de sobrescribir. Distinguir ideas o supuestos de decisiones confirmadas.
