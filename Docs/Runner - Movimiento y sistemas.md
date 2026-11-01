---
tags: [gamejam, out-of-control, diseño, unity]
estado: prototipo
---
# 🌀 Runner — Movimiento, velocidad e impactos

> [!abstract] Fantasía central
> El jugador decide cómo actuar y qué riesgos aceptar. La velocidad acumulada recompensa el impulso, pero reduce el tiempo de reacción y hace más difíciles las correcciones.

## 🧭 Estado del diseño

### Confirmado
- Descenso por salas prefabricadas semiprocedurales, con salidas hacia delante o hacia abajo y sin retorno. Se mantienen como máximo la sala anterior, la actual y la siguiente.
- La velocidad aumenta al superar salas; se buscan combos, puntos y monedas. El pisotón abre escotillas y puede dañar cajas.
- Los destructibles tienen vida y velocidad mínima de rotura. Hay enemigos y peligros de sala, como espinas y suelos pegajosos o calientes.
- Al final de la torre se compran beneficios con monedas. Al comenzar otra se escoge un enemigo junto con un beneficio. Se conservan ítems; existen ideas de ítems con ventajas y desventajas y maldiciones.

### Por definir
- Perspectiva y movimiento hacia delante; condiciones de derrota; adquisición y compensación de maldiciones; números finales y ritmo.
- Cómo aplicar ítems desde inventario/tienda, interfaz de selección, combos, puntos, monedas, peligros, enemigos y generación de salas.

## 🏃 Jugador — prototipo actual

| Acción | Prueba actual |
| --- | --- |
| Mover | A/D o flechas izquierda/derecha; eje X mundial |
| Saltar | Espacio, con ventana de tolerancia al salir del borde y buffer |
| Pisotón | S o flecha abajo en el aire; caída vertical rápida |
| Aumentar velocidad | `RegisterRoomCleared()`; debe llamarlo el futuro gestor una vez por transición |

La aceleración y el frenado permanecen constantes por defecto mientras aumenta la velocidad objetivo. El resultado es que alcanzar mayor velocidad y cambiar de sentido requieren más distancia. Se debe ajustar con pruebas reales.

### 🛡️ Precauciones a alta velocidad

El `CharacterController` desplaza al jugador mediante movimientos consecutivos de hasta `maxStepDistance`. Si la distancia solicitada supera `maxStepDistance × maxStepsPerFrame`, se limita la distancia simulada en ese cuadro para evitar un salto inseguro. Esto **reduce** el riesgo, no garantiza que todas las geometrías o triggers funcionen a cualquier velocidad; puede causar cámara lenta momentánea durante tirones o a velocidades extremas. Probar obstáculos finos, escotillas, triggers, pendientes y caídas largas en el editor.

## 💥 Impactos y destructibles

Se usa la **componente de la velocidad perpendicular al objeto**, antes de frenar contra la pared. Un roce oblicuo produce menos impacto que un choque directo.

En el modo anterior de cajas de prueba:

1. Si se alcanza `breakSpeed`, el objeto se destruye inmediatamente.
2. Por debajo del umbral, el impacto quita vida: `velocidad de impacto × daño por velocidad`, más un bono si es un pisotón desde arriba. La estadística `ImpactDamage` modifica ese daño.
3. Un intervalo mínimo entre impactos impide que los subpasos causen daño repetido en un solo instante.
4. La escotilla ignora los impactos normales y se rompe con un pisotón desde arriba. Su apertura no avanza la sala automáticamente todavía.

Estos valores son iniciales. El umbral de destrucción es por objeto y no se modifica actualmente mediante ítems; la vida, el umbral, el daño y el intervalo se editan en el inspector. Los nuevos props usan el modo proporcional descrito al final de esta nota.

## 📊 Estadísticas e ítems

`PlayerStats` mantiene modificadores por instancia del jugador. Hay valores planos y porcentajes aditivos; se aplica `(base + suma plana) × (1 + suma porcentajes)` con resultado mínimo cero. Para retirar una copia se eliminan los modificadores por su fuente.

| Estadística | Uso |
| --- | --- |
| MoveSpeed | Velocidad objetivo lateral, incluida la progresión por sala |
| Acceleration / Braking | Aceleración y frenado |
| AirControl | Multiplicador de control aéreo |
| JumpSpeed / PoundSpeed | Salto y pisotón |
| ImpactDamage | Daño de impacto que reduce vida, no el umbral de rotura |
| CameraControl | Sensibilidad del giro de cámara |

Se pueden crear definiciones con **Create → Runner → Item de estadísticas**. Cada efecto usa `Flat` o `Percent`; por ejemplo, `0.2` significa +20 % y `-0.3` significa −30 %. `Apply(stats, fuente)` y `Remove(stats, fuente)` son la conexión para el futuro inventario; usar una fuente distinta por copia. Crear el asset aún no equipa ni compra el ítem automáticamente.

## 🎥 Cámara de prueba

Sigue al jugador con suavizado y gira alrededor de él con el ratón. Su sensibilidad se multiplica por `CameraControl` y disminuye progresivamente con la velocidad horizontal hasta el mínimo configurado. Esto sirve para evaluar la idea de pérdida de control; no establece todavía la perspectiva final. Falta validar visibilidad, confort, sensibilidad y restricciones de cámara en juego real.

## 🧪 Cómo probar

En Unity: **Tools → Runner → Crear escena de prueba**. Crear de nuevo la escena para incluir la cámara actualizada. Saltar sobre la escotilla y pulsar S en el aire; probar la caja de frente a varias velocidades. La escena es temporal hasta guardarla.

> [!warning] Pendiente de validación
> Los scripts compilan, pero no hay todavía una prueba de juego validada en el editor para impactos rápidos, colisiones finas ni la sensación de cámara. El prototipo no incluye sistema de salas ni tienda.
## Props destructibles de Resources

Los doce prefabs de `Assets/Proyect/Assets/Resources/Props` tienen `ImpactTarget`, collider sólido y motivo/puntos propios. Sus valores son provisionales: copa 6 de vida y 5 m/s; libros y candelabros 10–24 y 7–11 m/s; muebles y cofres 24–35 y 11–14 m/s. Esos valores deben probarse con la progresión real de velocidad. No se generan ni se colocan automáticamente en las salas.

Los props usan daño proporcional: `vida máxima × velocidad perpendicular / velocidad de rotura`, más el bono de pisotón, y después se evalúa `ImpactDamage`. Ejemplo sin modificadores ni pisotón: con 100 de vida, umbral 200 m/s e impacto perpendicular de 100 m/s, se pierden 50 puntos de vida. Un objeto ya dañado puede romperse por debajo del umbral. El barrido previo solo aplica impactos que resultarían letales; los demás se aplican al contacto, respetando el intervalo entre impactos y el bloqueo de obstáculos. El modo anterior permanece para cajas y escotillas existentes. Valores con umbral cero no son apropiados para el modo proporcional.

`ReceivePunch(player, damage)` permite que el futuro controlador de puñetazos aplique daño directo y otorgue el combo al destruir; todavía no existe entrada ni animación de puñetazo. `Damaged` y `Broken` son puntos de conexión para futuros efectos. **Audio pendiente:** conectar un controlador de audio a golpe y destrucción, con sonido por defecto cuando el prop no tenga uno específico; aún no hay clips, selección de sonidos ni reproducción. Considerar el ciclo de vida al reproducir sonido de destrucción, pues el objeto se desactiva al romperse. Los impactos de rotura instantánea del modo antiguo y las escotillas no emiten `Damaged`; `Broken` sí se emite al romperlos.


## Límite de resistencia para velocidades altas (2026-10-31)

Los destructibles proporcionales conservan su vida y umbral propios. Al comenzar cada torre, la resistencia ahora crece 0,10 por cada múltiplo adicional de la velocidad inicial y se limita a 1,5 veces la vida original (antes 0,25 y máximo 3). El jugador expone ambos parámetros en `Resistencia de destructibles`. Esto también limita el crecimiento del umbral de rotura.

La exigencia adaptativa por velocidad máxima sigue siendo 45 %, pero su contribución se limita a 35 unidades/s por defecto. Nunca reduce un umbral base que el diseñador haya puesto por encima de ese valor. Cada `ImpactTarget` permite ajustar `Maximum Adaptive Break Speed`. A modo de referencia, el cofre de plata (umbral base 11,9), con escala 1,5 y velocidad máxima 170, exige como máximo 35 de velocidad perpendicular; un choque frontal a 70 aplica al menos el doble de su vida máxima antes de modificadores y del bono de velocidad. Un roce lateral o una penalización de daño pueden dar otro resultado. Trampillas y destructibles no proporcionales conservan sus reglas.

Las fórmulas se verificaron con velocidades 70 y 170 y con un umbral especial resistente; falta probar colisiones, objetos apilados, puntuación de combo y tirones en Play Mode. El límite no garantiza que el barrido físico detecte todos los objetos a velocidades extremas.


## Golpe y parry contra destructibles

Clic izquierdo realiza un golpe frontal en área con alcance configurable (3,7 por defecto) y un 65 % del daño de colisión calculado para la velocidad y superficie del impacto. Clic derecho conserva el parry y aplica el 30 % del mismo cálculo a los destructibles en un alcance de 3. Los obstáculos sólidos detienen la búsqueda. Las trampillas y los objetos bloqueados conservan sus reglas. Los golpes muestran un corte provisional; en `PlayerRepel` se puede asignar un prefab y ajustar tamaño, color y duración.

El parry mantiene su espera base de 0,65 s. Cada uso rápido añade 0,12 s hasta un máximo adicional de 0,6 s; esperar 1,8 s desde el último uso reinicia la acumulación. Estos valores son configurables en el componente. La ventana del combo y la puntuación siguen las reglas existentes de destrucción. Requiere prueba interactiva de alcance, sensación y balance en Play Mode.
