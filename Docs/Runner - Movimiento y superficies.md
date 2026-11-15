---
tags: [gamejam, movimiento, superficies, estadísticas]
estado: prototipo
---
# 🧊 Movimiento y superficies

> [!abstract] Intención
> Al inicio, el jugador puede acelerar, frenar y rectificar con relativa facilidad. A medida que crece la velocidad, necesita más distancia para corregir su trayectoria; ítems y superficies pueden cambiar ese comportamiento. Los números son provisionales y requieren pruebas.

## 🏃 Estadísticas de movimiento

Los valores se configuran en el inspector de `PlayerController` y se pueden modificar mediante `PlayerStats`. Los límites de precisión y los subpasos de colisión son ajustes técnicos del inspector, no estadísticas de ítems.

| Estadística | Base | Función |
| --- | --- | --- |
| `MoveSpeed` | 6 | Velocidad objetivo horizontal. |
| `SpeedPerRoom` | 0.6 | Aumento de la velocidad base por sala. |
| `Acceleration` | 28 | Aceleración horizontal. |
| `Braking` | 24 | Frenado sin entrada. |
| `TurnAcceleration` | 32 | Corrección al intentar invertir la dirección. |
| `AirControl` | 0.6 | Multiplicador de control en el aire. |
| `JumpSpeed` | 9 | Impulso vertical del salto. |
| `Gravity` | 25 | Gravedad normal. |
| `MaxFallSpeed` | 30 | Velocidad máxima de caída normal; el pisotón usa su propia velocidad. |
| `PoundSpeed` | 22 | Velocidad inicial del pisotón. |
| `PoundSpeedPerRoom` | 0.4 | Aumento del pisotón por sala. |
| `CoyoteTime` / `JumpBufferTime` | 0.1 s / 0.12 s | Ventanas de tolerancia de salto. |
| `GroundStickSpeed` | 2 | Velocidad descendente pequeña para mantener contacto con el suelo. |

La cámara también permite modificar `CameraControl` (base 1), `CameraSensitivity` (0.12), `CameraPenaltySpeed` (30), `CameraMinimumControl` (0.3) y `CameraPitchLimit` (85°; limitado a 1–89°). La penalización depende de la velocidad horizontal real. Las estadísticas de puntos y combo siguen documentadas en [[Runner - Combo y puntuacion]].

## 🌋 Superficies de prueba

`GroundSurface` es una zona trigger poco alta sobre un suelo sólido. Aplica modificadores mientras el jugador está dentro **y en el suelo**, y los retira al salir, saltar o desactivar la zona. Cada superficie tiene su propia fuente para que al retirarla no elimine modificadores de ítems u otras superficies. Porcentajes del mismo tipo se suman: una combinación de varios efectos debe probarse y balancearse.

| Superficie | Configuración de muestra |
| --- | --- |
| Hielo | Frenado −45 % y control de inversión de dirección −35 %. |
| Brea | Velocidad objetivo −30 % y aceleración −25 %. |
| Lava | 1 punto de daño tras 1,25 s de contacto continuo con el suelo; después se repite cada segundo hasta saltar o salir. |

`PlayerHealth` tiene 10 puntos de vida en la prueba y emite una notificación de daño aceptado. Al llegar a cero emite `Died`; **todavía no hay pantalla de derrota, reinicio ni inmunidad por ítems**. El daño se pausa y reinicia su gracia si el jugador salta, deja de estar apoyado o sale de la superficie; no hace falta otro raycast porque `GroundSurfaceSensor` usa el `IsGrounded` del movimiento. La gracia inicial, el daño y el intervalo de repetición se configuran por superficie y usan tiempo de juego. El daño aceptado por lava cancela la racha mediante el flujo normal de `PlayerHealth`.

Los colliders que forman una misma zona de lava comparten `GroundSurfaceDamageGroup`: caminar por una unión de tiles no multiplica el daño ni reinicia la gracia, y solo abandonar por completo la lava o despegar del suelo reinicia el temporizador.

`GroundSurfaceSensor` registra el tile nuevo antes de retirar el anterior para conservar el contacto continuo en las juntas. El resumen de `TAB` usa la maldición de sala cuando existe y, en escenas de prueba sin `RoomCurseDirector`, refleja directamente la superficie que detecta este sensor.

Las tres maldiciones visuales reutilizan los renderers del suelo existente y añaden temporalmente el material final de superficie: `IceLast`, `BreaVivaVer3` o `NuevoGraphVer4_LavaViscosa`. Los prefabs `Plane_Ice_Last`, `Plane_Brea_Last` y `Plane_Lava_Last` solo sirven para identificar esos materiales; no se instancian ni reemplazan la geometría, escala o colliders de las salas. Al salir se restauran exactamente los materiales y layers originales.

Por defecto las superficies conservan el layer original: hielo, brea y lava no reciben `PixelatedFine` ni `PixelatedLarge`. La escena de prueba conserva el selector únicamente para comparaciones manuales.

En `SkyboxTesting` hay botones `NORMAL`, `HIELO`, `BREA` y `LAVA`. También se puede pulsar `H` para hielo, `B` para brea y `L` para lava; repetir la tecla de la superficie activa vuelve a normal. Cada selección dura 30 s y `O` alterna `None → Fine → Large` únicamente para comparación. El diagnóstico muestra cuántos renderers tienen realmente el layer seleccionado, las superficies creadas, `IsGrounded` y la vida actual. `PixelatedFine` en PC es deliberadamente sutil (`2 × 1`); `Large` (`10 × 10`) sirve para comprobar a simple vista que el cambio de layer funciona sin modificar globalmente los numerosos props que ya usan `Fine`.

## 🧪 Cómo probar

Crear nuevamente la escena con **Tools → Runner → Crear escena de prueba**. En la sala principal hay zonas celeste, marrón y naranja para hielo, brea y lava; el HUD muestra la vida. Recoge los ítems de prueba y compara el control a distintas velocidades. Al pisotear la escotilla, la conexión de demostración suma una sala.

La compilación pasó y se probaron los cálculos de modificadores por fuente de forma aislada. Faltan pruebas en Play Mode de triggers sobre el `CharacterController`, entrada y salida a gran velocidad, saltos y transición entre superficies. Las zonas por trigger no garantizan detectar un paso a cualquier velocidad; ajustar el tamaño y probar los límites físicos.


## Choque extremo contra paredes (2026-10-30)

Si la velocidad horizontal llega a 120 unidades/s, está al menos al 95 % del sprint máximo actual y al menos el 55 % apunta hacia una pared, el jugador rebota. Conserva 35 % de la velocidad tangencial y sale en la normal de la pared con 20 % de la velocidad de impacto, hasta 25 unidades/s. Durante 0,2 s mantiene el impulso sin aceleración de input; hay 0,35 s entre rebotes. Estos parámetros están en `PlayerController`. Los props que se rompen antes del movimiento no causan el choque; los obstáculos que sobreviven sí pueden causarlo. No añade daño de vida, modo de locura ni desplazamiento vertical de cámara. El evento `WallCrashed` permite conectar presentación sin acoplarla al movimiento.

120 unidades/s es un umbral extremo, muy por encima del inicio (sprint base 6,9 u/s), y puede no alcanzarse en una partida normal. Pendiente probar en Unity colisiones frontales y oblicuas, esquinas, objetos rompibles, deslizamiento, pausas y velocidades extremas; los subpasos tienen un límite de distancia por fotograma.
