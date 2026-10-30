---
tags: [gamejam, gdd, diseño, equipo, fantasía]
estado: documento-vivo
actualizado: 2026-10-28
---
# GDD — Runner de torre medieval mágica

> [!abstract] Estado del documento
> Documento vivo para coordinar una game jam de **3 días**. El título, la historia y varios valores están pendientes. Diferenciamos lo confirmado para el equipo, lo que ya es prototipo y las ideas que necesitan decisión o pruebas.

## 1. El juego en una frase

Un runner roguelite en **primera persona** donde el jugador desciende una torre medieval mágica, encadena acciones y gana velocidad hasta que esquivar, girar y tomar decisiones se vuelve cada vez más difícil.

**Título:** pendiente.  
**Tema de la jam:** *Out of Control*.  
**Género:** runner de salas, acción rápida, puntuación y progresión roguelite.  
**Perspectiva:** primera persona.  
**Presentación:** escenarios y objetos 3D con tratamiento visual de pixel art.  
**Ambientación:** fantasía medieval mágica.  
**Plataforma de prueba:** PC, teclado y ratón.

## 2. Fantasía del jugador y tema

El jugador comienza con control suficiente para recorrer las salas, reconocer las salidas y escoger riesgos. Al superarlas acumula velocidad: romper objetos y mantener combos se vuelve atractivo, pero también cuesta más frenar, cambiar de dirección y reaccionar. La tensión nace de aprovechar ese impulso sin que termine dominando la partida.

**El jugador controla:** sus movimientos, saltos y pisotones; la ruta inmediata dentro de la sala; los objetos que intenta romper; y, en el diseño previsto, las mejoras y riesgos que acepta entre torres.

**Se vuelve difícil de controlar:** la velocidad acumulada, la inercia, el tiempo de reacción disponible y los efectos adversos de ciertos ítems o peligros. La intención es que el jugador siga tomando decisiones incluso cuando ejecutarlas se vuelva complicado.

Referencia de sensación: la recompensa por encadenar acciones y el estilo de puntuación de *Ultrakill*, adaptados al descenso por salas. No implica copiar su contenido ni todas sus mecánicas.

## 3. Bucle principal

1. El jugador comienza dentro de la sala inicial de la torre.
2. Recorre salas en primera persona: se mueve, salta, evita obstáculos, rompe objetos y busca la salida. Las acciones pueden sumar puntos y mantener el combo.
3. Si encuentra una trampilla, salta y ejecuta un pisotón para romperla y caer. Si encuentra una puerta, la mira y pulsa **E** para abrirla y avanzar.
4. Al superar salas aumenta la velocidad. El recorrido es de una sola dirección: no se vuelve a las salas anteriores.
5. Llega a la sala final y completa la torre.
6. **Progresión prevista, todavía pendiente de implementar:** usa monedas recogidas en el recorrido para comprar una o varias mejoras según su presupuesto; al iniciar la siguiente torre elige un enemigo acompañado de un beneficio. Conserva sus ítems y afronta una torre más larga y rápida.

La primera y la última sala cuentan dentro del número total de salas de la torre. El mínimo técnico actual es dos: inicial y final.

## 4. Controles actuales del prototipo

| Entrada | Acción |
| --- | --- |
| W/A/S/D | Movimiento relativo a la mirada. S retrocede mientras está en el suelo. |
| Ratón | Mirar y girar. |
| Espacio | Saltar. |
| Ctrl en el aire | Pisotón vertical. |
| C | Agacharse; con velocidad suficiente al pulsar, deslizarse con un breve impulso. |
| E, mirando una puerta cercana | Abrir puerta. |
| Clic derecho | Repeler enemigos dentro del radio, sin necesidad de mirarlos. |
| Escape / clic izquierdo | Liberar / capturar el cursor. |

Estos controles describen el prototipo; se pueden ajustar después de probar la sensación. La cámara pierde capacidad de giro progresivamente con la velocidad y esa capacidad es una estadística modificable.

## 5. Mecánicas

### Movimiento, velocidad y pisotón

- El movimiento tiene aceleración, frenado, giro e inercia ajustables. La velocidad objetivo aumenta al superar salas.
- El pisotón inicia una caída rápida y anula el movimiento horizontal en el prototipo. Abre trampillas al impactarlas desde arriba y puede dañar destructibles.
- La velocidad alta debe sentirse poderosa pero cada vez más difícil de gestionar. Los valores concretos y su curva requieren playtesting.
- Las estadísticas pueden modificarse con efectos: velocidad, aceleración, frenado, giro, control aéreo, salto, gravedad, pisotón y control de cámara, entre otras.

### Destructibles, puntos y combo

Los objetos destructibles tienen vida y un umbral de velocidad. Un impacto suficientemente rápido puede romperlos de inmediato; uno menor puede causar daño. El pisotón añade daño. Esto permite usar la velocidad para conseguir puntos, aunque chocar con un peligro siga siendo arriesgado.

El prototipo registra motivos visibles como **«Caja rota +1»**, además del combo y multiplicador. Una acción reinicia la ventana del combo; si pasa demasiado tiempo, este termina. La puntuación se concede por acción. Los valores mostrados son de prueba.

Los ítems podrán modificar estadísticas como duración del combo, crecimiento del multiplicador y amplificación de puntos. La puntuación mide rendimiento; **las monedas están previstas como recurso de compra**, pero su obtención y la tienda aún no están implementadas.

### Peligros de sala

| Peligro | Intención de gameplay | Estado |
| --- | --- | --- |
| Espinas / maza con púas | Obstáculo peligroso que se debe evitar, salvo que un ítem permita otra cosa. | Idea confirmada; comportamiento y daño pendientes. |
| Hielo | Menos frenado y control al girar. | Superficie de prueba, pendiente de validar en Play Mode. |
| Brea / suelo pegajoso | Reduce velocidad y aceleración. | Superficie de prueba, pendiente de validar en Play Mode. |
| Lava / suelo caliente | Daña si se permanece sobre él. | Daño y vida básicos de prueba; falta validar y definir derrota completa. |

Los peligros deben poder distinguirse con rapidez desde primera persona. Su aspecto concreto y las combinaciones de salas los definirá el equipo de arte con diseño.

### Primer enemigo: Flupin, la pelota orbital

Una pelota es atraída hacia el jugador. El jugador puede repelerla en área con clic derecho, incluso sin mirarla; al recibir el golpe sale impulsada y después vuelve a acercarse gradualmente. Si alcanza al jugador, quita un tercio de su vida actual y se aleja con más fuerza y distancia. El prototipo usa movimiento vectorial simple, sin pathfinding complejo.

**Estado:** Flupin y el clic derecho están implementados como prototipo; la órbita sigue al jugador en espacio del mundo y respeta el material asignado. Falta validarlos en Play Mode. La apariencia medieval mágica y el nombre final quedan abiertos al equipo.

## 6. Salas y generación

La torre se construye con **salas prefabricadas reutilizables**, elegidas de forma semiprocedural. Se pueden hacer variantes cortas, largas o en L. Cada sala puede tener puntos invisibles opcionales para que aparezcan cajas, decoraciones, trampas u otros prefabs compatibles.

La generación mantiene como máximo tres salas activas: **anterior, actual y siguiente**. Al avanzar se cierra el retorno y se retira la sala que quedó atrás.

| Plantilla | Entrada | Salida |
| --- | --- | --- |
| Inicial | Sin entrada; el jugador empieza dentro. | Trampilla en el suelo. |
| Intermedia A | Trampilla en el techo. | Trampilla en el suelo. |
| Intermedia B | Trampilla en el techo. | Puerta en una pared. |
| Intermedia C | Puerta en una pared. | Puerta en una pared. |
| Intermedia D | Puerta en una pared. | Trampilla en el suelo. |
| Final | Trampilla en el techo. | Sin salida de recorrido. |

Las cuatro combinaciones intermedias tienen pools separados. Una puerta conecta con una entrada de puerta; una trampilla conecta con una entrada desde arriba. Al bajar, la sala inferior puede girar 0°, 90°, 180° o 270° alrededor de la conexión si los huecos lo permiten.

**Importante para diseño:** los marcadores alinean conexiones, pero el generador aún no verifica que la geometría se solape, que los colliders dejen pasar ni que las aberturas tengan dimensiones compatibles. Las salas reales necesitan pruebas en Unity.

## 7. Ítems, riesgo y progresión

### Dirección confirmada

- Entre torres se conservan los ítems; las monedas del recorrido permitirán comprar más de un beneficio si alcanza el presupuesto.
- Al iniciar una nueva torre se plantea elegir un enemigo junto con un beneficio para que la elección tenga una recompensa clara.
- Algunos ítems combinarán beneficios y desventajas. Las maldiciones se consideran efectos perjudiciales.
- Cada torre posterior será más larga y empezará a un ritmo mayor.

### Ejemplos de diseño, no catálogo final

- Más velocidad a cambio de menos capacidad de giro de cámara a altas velocidades.
- Beneficio de velocidad por sala acompañado de un elemento que obstruye parte de la interfaz.
- Un ítem que permita tolerar o atravesar un peligro concreto.

**Por decidir:** cómo se consiguen las maldiciones, cuáles son las reglas exactas de compra, cuántas opciones se ofrecen, qué se conserva entre torres y qué condición representa una victoria definitiva de la partida. Los efectos que reducen visibilidad o control deben probarse para evitar situaciones imposibles de leer.

## 8. Dirección artística para el equipo

**Confirmado:** fantasía medieval mágica; escenarios y modelos 3D con presentación pixel art; juego rápido en primera persona y ambientado en una torre.

**Propuesta de lectura visual — abierta al equipo:** piedra y madera para la estructura; hierro, telas y elementos arcanos para reforzar la fantasía. Contrastar materiales seguros con trampas y superficies peligrosas. Las puertas y trampillas de prueba son marrones; su diseño final puede ser medieval mágico mientras siga siendo reconocible.

La claridad tiene prioridad: el jugador debe identificar salidas, obstáculos, cajas rompibles y peligros en poco tiempo. Conviene probar siluetas, colores y contraste dentro de Unity con el tratamiento pixelado, ya que los detalles pequeños pueden perderse.

### Primeros recursos útiles

| Prioridad sugerida | Recurso | Para qué sirve |
| --- | --- | --- |
| 1 | Sala inicial, sala final y bloques para las cuatro conexiones. | Validar el recorrido antes de producir variaciones. |
| 1 | Puerta y trampilla diferenciadas. | Identificar y atravesar cada salida. |
| 1 | Caja destructible. | Probar velocidad, impacto y puntuación. |
| 2 | Módulos de piedra, madera, pared y techo. | Variar las salas reutilizando piezas. |
| 2 | Tratamientos visuales de hielo, brea, lava y espinas. | Hacer legibles los peligros. |
| 2 | Diseño visual de la pelota orbital. | Definir su presentación final y comunicar la amenaza. |
| 3 | Candelabros, banderas, mobiliario y adornos mágicos. | Enriquecer la ambientación si queda tiempo. |

La guía concreta de entregables y marcadores está en [[Brief de arte - Salas y recursos]].

## 9. Mundo y narrativa

**Lore: sin definir.** No hay aún historia oficial, personaje protagonista, nombre de la torre ni explicación del poder que acelera al jugador.

El equipo puede proponer por qué existe la torre, qué fuerza mágica provoca el descenso y qué persigue la pelota. Son oportunidades creativas, no requisitos cerrados. Para el alcance de la jam, la ambientación puede comunicarse con la arquitectura, objetos y señales visuales sin depender de diálogo o cinemáticas.

## 10. Estado actual del proyecto

| Sistema | Situación |
| --- | --- |
| Movimiento y cámara en primera persona | Prototipo implementado; falta validar sensación y colisiones a alta velocidad. |
| Salto, pisotón, cajas y trampilla | Base implementada; requiere pruebas con geometría real. |
| Puertas con E | Base implementada; requiere prueba de interacción y colliders. |
| Puntuación y combo | Base funcional y HUD temporal; balance y UI final pendientes. |
| Estadísticas y efectos | Base reutilizable implementada; faltan inventario y conexiones de contenido. |
| Salas y conexiones | Generador y pools implementados; falta validar prefabs, huecos, cierres y solapamientos en Play Mode. |
| Suelos especiales y vida | Prototipos de prueba; derrota solo como estado básico, sin pantalla ni reinicio. |
| Flupin | Prototipo de órbita, ataque y repulsión; validación visual y de gameplay pendiente. |
| Espinas y otras trampas | Pendientes. |
| Monedas, tienda, elección de enemigo/beneficio, siguiente torre | Pendientes. |
| Arte final, sonido, interfaz final y lore | Pendientes del equipo. |

**No confundir compilación con juego validado:** varias partes se han compilado y probado de forma aislada, pero aún hace falta una partida completa con salas y arte reales.

## 11. Alcance propuesto para la jam

> [!note] Propuesta para coordinar; ajustar según la capacidad del equipo
> 1. **Primero:** recorrido jugable de una torre, entrada y salida fiables, movimiento, pisotón, una caja y una forma clara de finalizar o perder.
> 2. **Después:** algunas variantes de sala, peligros legibles, pelota orbital y combo/puntos visibles.
> 3. **Si alcanza el tiempo:** monedas, compra de mejoras, siguiente torre, contratos, efectos mixtos, más decoración y pulido.

Se mantienen fuera del alcance inicial los sistemas de diálogo, ondas expansivas y las IAs avanzadas. Las prioridades no sustituyen la prueba temprana del prototipo con los modelos del equipo.

## 12. Decisiones abiertas

- Nombre, lore y diseño del protagonista, de la torre y de la pelota.
- Qué constituye el final completo de una partida y cómo se reinicia tras perder.
- Ritmo de aumento de velocidad, tamaño mínimo seguro y cantidad de salas por torre.
- Lista final de ítems, monedas, precios, beneficios, enemigos y maldiciones.
- Dirección visual final: paleta, densidad de detalle, tratamiento pixelado, iluminación y sonido.
- Cómo comunicar visualmente estados del combo, daño, interacción y salidas a alta velocidad.

## Documentos relacionados

- [[Brief de arte - Salas y recursos]] — entregables, escala y recomendaciones para modeladores.
- [[Runner - Contrato de salas y marcadores]] — configuración detallada de los prefabs.
- [[Runner - Pools final y puertas]] y [[Runner - Orientacion de salas]] — conexiones y rotaciones.
- [[Runner - Movimiento y superficies]] y [[Runner - Combo y puntuacion]] — números provisionales.
- [[Runner - Eventos de partida y salas]] — integración para programación.
