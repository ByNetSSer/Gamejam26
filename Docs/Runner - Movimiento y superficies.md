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
| Lava | 1 punto de daño por segundo de contacto continuo, después del primer segundo. |

`PlayerHealth` tiene 10 puntos de vida en la prueba y emite una notificación de daño aceptado. Al llegar a cero emite `Died`; **todavía no hay pantalla de derrota, reinicio ni inmunidad por ítems**. El daño se pausa si el jugador deja de estar apoyado, sale o si `timeScale` es cero. El intervalo se configura por superficie y usa tiempo de juego.

## 🧪 Cómo probar

Crear nuevamente la escena con **Tools → Runner → Crear escena de prueba**. En la sala principal hay zonas celeste, marrón y naranja para hielo, brea y lava; el HUD muestra la vida. Recoge los ítems de prueba y compara el control a distintas velocidades. Al pisotear la escotilla, la conexión de demostración suma una sala.

La compilación pasó y se probaron los cálculos de modificadores por fuente de forma aislada. Faltan pruebas en Play Mode de triggers sobre el `CharacterController`, entrada y salida a gran velocidad, saltos y transición entre superficies. Las zonas por trigger no garantizan detectar un paso a cualquier velocidad; ajustar el tamaño y probar los límites físicos.


## Choque extremo contra paredes (2026-10-30)

Si la velocidad horizontal llega a 120 unidades/s, está al menos al 95 % del sprint máximo actual y al menos el 55 % apunta hacia una pared, el jugador rebota. Conserva 35 % de la velocidad tangencial y sale en la normal de la pared con 20 % de la velocidad de impacto, hasta 25 unidades/s. Durante 0,2 s mantiene el impulso sin aceleración de input; hay 0,35 s entre rebotes. Estos parámetros están en `PlayerController`. Los props que se rompen antes del movimiento no causan el choque; los obstáculos que sobreviven sí pueden causarlo. No añade daño de vida ni un modo de locura. El evento `WallCrashed` permite conectar presentación.

120 unidades/s es un umbral extremo, muy por encima del inicio (sprint base 6,9 u/s), y puede no alcanzarse en una partida normal. Pendiente probar en Unity colisiones frontales y oblicuas, esquinas, objetos rompibles, deslizamiento, pausas y velocidades extremas; los subpasos tienen un límite de distancia por fotograma.
