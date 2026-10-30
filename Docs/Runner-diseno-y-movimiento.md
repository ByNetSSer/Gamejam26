# Runner: decisiones y prototipo de movimiento

## Confirmado por el diseño
- Runner 3D de descenso rápido con estética pixel art; la velocidad crece y dificulta el control.
- Salas prefabricadas semiprocedurales, conexión hacia delante o hacia abajo y avance sin retorno. Como máximo se mantienen la anterior, actual y siguiente; la anterior se retira después.
- A/D o flechas para movimiento lateral; salto y pisotón con S o flecha abajo en el aire. La escotilla se rompe al recibir un pisotón.
- Las cajas tienen vida y un umbral de velocidad de rotura; los pisotones también pueden dañarlas. Hay combo, puntos y monedas.
- Hay peligros como espinas, suelo pegajoso y caliente, además de la pelota atraída al jugador y repelida por un golpe.
- Al terminar la torre se compran beneficios con monedas; se conservan ítems. Al iniciar otra torre se elige un enemigo con beneficio asociado. La siguiente torre es más larga y rápida.
- Algunos ítems combinan ventajas y desventajas; también se consideran maldiciones perjudiciales.

## Prototipo implementado
- CharacterController lateral, aceleración y frenado independientes, control aéreo, salto con coyote y buffer, pisotón y método público RegisterRoomCleared para incrementar velocidad por transición.
- Objetivo de impacto básico: rotura por velocidad normal de impacto, daño por pisotón y escotilla que solo acepta pisotón desde arriba.
- La orientación lateral usa el eje X mundial. Es una suposición inicial para probar el movimiento; la perspectiva y el movimiento hacia delante aún requieren definición.
- La escena de prueba se crea desde el menú Tools > Runner > Crear escena de prueba. No genera ni conecta salas automáticamente.

## Pendiente de decidir o probar
- Perspectiva/cámara y movimiento hacia delante, condición de derrota, cómo se consiguen y compensan las maldiciones.
- Valores de aceleración, frenado, salto y velocidad por sala mediante playtests. La velocidad crece sin límite por ahora.
- Puntuación, monedas, combo, golpes a enemigos, trampas, tiendas, progresión de torres y generación de salas aún no están implementados.
- Ideas no confirmadas: indicador de velocidad, señales de salida y temporizador de combo. Probar con cuidado los efectos que obstruyen pantalla o limitan cámara.
