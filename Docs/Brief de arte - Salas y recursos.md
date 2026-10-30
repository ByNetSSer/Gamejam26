---
tags: [gamejam, arte, modelado, salas, brief]
estado: guia-de-trabajo
actualizado: 2026-10-28
---
# Brief de arte — Salas y recursos

> [!abstract] Para modeladores y artistas
> Estamos creando un runner rápido en primera persona que desciende una torre de **fantasía medieval mágica**. Los escenarios y objetos son 3D y se verán con un tratamiento pixel art. Necesitamos piezas que se reconozcan rápido y salas que puedan conectarse de forma reutilizable.

El diseño global está en [[GDD - Torre medieval magica]]. El lore y la dirección artística detallada siguen abiertos; pueden proponer ideas al equipo.

## 1. Qué necesitamos primero

### Entregables prioritarios

| Entregable | Necesidad |
| --- | --- |
| **Sala inicial** | Zona donde aparece el jugador y una trampilla de salida en el suelo; no necesita entrada. |
| **Sala final** | Entrada por trampilla en el techo; sin salida de recorrido. |
| **Cuatro salas intermedias** | Una por combinación de entrada y salida de la tabla siguiente. Inicialmente pueden ser variantes sencillas construidas con las mismas piezas. |
| **Trampilla** | Cubierta claramente reconocible del hueco del suelo; debe dejar espacio libre al romperse. |
| **Puerta** | Pieza reconocible para abrir mirando y pulsando E; preparar panel y bisagra o pivote para la apertura. |
| **Caja destructible** | Silueta clara y diferente de elementos peligrosos. |

| Sala intermedia | Entrada | Salida |
| --- | --- | --- |
| A | Trampilla en techo | Trampilla en suelo |
| B | Trampilla en techo | Puerta en pared |
| C | Puerta en pared | Puerta en pared |
| D | Puerta en pared | Trampilla en suelo |

Después de validar la conexión de estas plantillas, se pueden hacer variantes largas, cortas, en L o con nuevas distribuciones. Las paredes y techos se pueden completar gradualmente: lo primero es que la geometría jugable, los huecos y los puntos de conexión funcionen.

### Recursos secundarios

- Piezas modulares de piedra, suelo, pared y techo; elementos de madera y metal.
- Candelabros, estandartes, mobiliario y detalles arcanos reutilizables.
- Tratamientos o módulos para **hielo, brea y lava**, distinguibles al primer vistazo.
- Espinas o mazas con púas como peligros; acordar movimiento y colisiones con programación antes de animarlas.
- Concepto y silueta de **Flupin, la pelota orbital mágica**. Existe un prototipo que respeta el material asignado; queda por definir la apariencia final y probarla.

Las ideas de materiales o decoración son una guía inicial, no lore ni diseño final obligatorio.

## 2. Medidas de referencia para empezar

> [!warning] Son medidas del prototipo, no una especificación artística definitiva
> Validarlas con programación y dentro de Unity antes de modelar todo el set. Una sala no tiene que mantener exactamente estas dimensiones para funcionar.

| Elemento | Referencia de la torre de prueba |
| --- | --- |
| Sala de ejemplo | Aproximadamente **10 × 10 unidades**, con paredes de **6 unidades** de altura. |
| Hueco de trampilla y trampilla de prueba | Aproximadamente **3 × 3 unidades**. |
| Abertura y panel de puerta de prueba | Aproximadamente **3 unidades de ancho × 3 de alto**. |
| Marcadores de conexión | Escala global **(1, 1, 1)**, sin inclinación en X/Z y con +Y hacia arriba. |
| Jugador | El prototipo usa un `CharacterController`; verificar sus dimensiones reales en la escena antes de cerrar pasillos y aberturas. |

Para que las salas puedan girar 90° al conectar una trampilla, **es conveniente que los huecos sean cuadrados y compatibles entre sí**. Si son rectangulares, acordar con programación qué giros se permiten.

No se ha definido una resolución final de texturas, un límite de polígonos ni una paleta cerrada. Proponer un primer set ligero y probarlo con el shader/render pixelado; evitar producir muchas variantes antes de esa revisión.

## 3. Marcadores y huecos

Cada sala usa objetos vacíos como marcadores; la geometría debe tener un hueco real para cada conexión. El sistema no recorta el modelo ni elimina colliders por sí solo.

- **Entrada por puerta:** vacío en el centro de la abertura; eje azul local **+Z apunta hacia el interior** de la sala.
- **Salida por puerta:** vacío en el centro de la abertura; **+Z apunta hacia fuera** de la sala.
- **Entrada por trampilla:** vacío centrado en el hueco del techo, a la altura del techo.
- **Salida por trampilla:** vacío centrado en el hueco del suelo, a la altura del suelo.
- **Sala inicial:** añadir además un vacío para la posición inicial del jugador; no necesita marcador de entrada.
- **Sala final:** entrada por trampilla y sin marcador de salida.

En trampillas, +Z indica la orientación horizontal de referencia de la sala. Mantener los marcadores activos, nivelados y con escala global 1. El equipo de Unity puede añadir el componente `RoomDefinition` y asignar los vacíos; el modelador no necesita programar.

El prefab de trampilla y el de puerta los instancia el sistema en el marcador de salida. **Evitar duplicarlos dentro del modelo de sala** salvo que se acuerde otra configuración con programación. Ajustar el tamaño de los huecos y del modelo compartido en conjunto.

## 4. Preparación para Unity

- Entregar la geometría de sala y los objetos interactivos como piezas separadas cuando necesiten moverse, romperse o reutilizarse.
- En la puerta, separar el panel del marco y ubicar la bisagra en el borde de giro; coordinar el sentido de apertura y la escala con programación.
- Mantener los huecos de entrada y salida despejados y reservar espacio para el personaje y el cierre de retorno invisible.
- Proponer colliders sencillos o indicar qué partes deben bloquear. **Nunca cubrir el hueco de trampilla con un collider de suelo completo.**
- Evitar que adornos o puntas pequeñas invadan el camino sin que el jugador pueda leerlos. Un objeto decorativo no debe parecer una trampa si no lo es.
- Para zonas de aparición semiprocedural, dejar superficies libres donde luego se puedan colocar puntos invisibles; acordar con diseño cuáles son seguras. Los puntos son opcionales y los configura Unity.
- Conservar una escala coherente, pivotes comprensibles y nombres claros. Entregar archivos fuente y formato de intercambio que acuerde el equipo; el formato exacto aún no está fijado.
- No hay presupuesto de geometría ni atlas definitivo; compartir un ejemplo temprano para medir rendimiento y lectura.

### Convención de nombres sugerida

Son ejemplos, no una obligación si el equipo ya tiene una convención:

```text
Sala_Inicial_Trampilla
Sala_Trampilla_Puerta_01
Sala_Puerta_Trampilla_01
Sala_Trampilla_Trampilla_01
Sala_Puerta_Puerta_01
Sala_Final_Trampilla
Marcador_Entrada
Marcador_Salida
```

## 5. Claridad y estilo

El ritmo será alto y la cámara está dentro del espacio. Antes que añadir detalle fino, asegurar que las formas funcionen de cerca y desde la distancia:

- **Salida:** reconocible y visible antes de llegar a ella.
- **Caja destructible:** claramente distinta de decoración sólida y peligros.
- **Peligros:** lectura inmediata y coherente para hielo, brea, lava y espinas; acordar colores finales y señales con diseño.
- **Formas grandes:** marcos, arcos, vigas y siluetas que sobrevivan al pixelado. Revisar texturas y bordes desde la cámara real, no solo desde el visor de modelado.
- **Fantasía mágica:** el equipo puede explorar símbolos, materiales y mecanismos arcanos, manteniendo clara la función de cada objeto.

No hay lore acordado. Una propuesta visual puede sugerir historia, pero ningún símbolo, personaje o explicación de la torre es todavía canon.

## 6. Revisión de una sala antes de producir variantes

- [ ] La entrada y la salida corresponden a la combinación indicada.
- [ ] Los huecos existen en malla y colliders; puerta o trampilla caben en ellos.
- [ ] Los marcadores están centrados, nivelados, a escala global 1 y con el +Z correcto.
- [ ] Hay espacio suficiente para avanzar, girar y aterrizar en primera persona.
- [ ] La geometría sigue legible con el tratamiento pixelado.
- [ ] Programación probó la sala en Unity, incluyendo rotaciones de trampilla y cierre tras pasar.
- [ ] Se registraron dudas o propuestas antes de duplicar la plantilla.

El generador alinea marcadores, pero no garantiza que las salas no se solapen ni comprueba que sus huecos encajen. Probar distintas combinaciones reales antes de dar el set por terminado.

## 7. Prueba existente

En Unity, **Tools → Runner → Crear torre de prueba** genera una escena temporal con salas de bloque y conexiones. También crea una carpeta nueva de prefabs de ejemplo; las carpetas antiguas no se sobrescriben. Las salas finales del equipo reemplazarán progresivamente estas piezas en los pools correspondientes. La herramienta de conexión existe, pero aún se requiere una prueba completa en Play Mode con los modelos nuevos.


## Prueba de props fragmentados (2026-10-30)

Preparar primero una caja o un cofre de prueba en Blender. Entregar el modelo intacto y una versión rota que conserve la misma escala, orientación y pivote, con entre 6 y 12 trozos visibles como objetos separados. Mantener materiales compatibles y texturas existentes; preferir fragmentos grandes y legibles frente a muchos pequeños. Los interiores pueden ser sencillos y coherentes con madera o el material del objeto.

En Unity, crear un prefab de rotura con un objeto raíz vacío y cada trozo como hijo directo, en las posiciones que reconstruyen el objeto intacto. Dejar la raíz con escala local 1 y no poner Rigidbody ni Collider en ella; evitar Rigidbody en descendientes. Opcionalmente, añadir a cada hijo un collider sencillo o MeshCollider convexo. Asignar el prefab en `ImpactTarget > Broken Fragments Prefab` del objeto intacto. El sistema crea rigidbodies, usa colliders de caja si faltan o hay mallas no convexas, aplica impulso según el golpe, ignora la colisión con el jugador y elimina los fragmentos tras cuatro segundos. Máximo 12 por defecto; los sobrantes se ocultan. Las partículas existentes siguen activas.

Es un flujo provisional: comprobar en Play Mode coincidencia de pivote, escala y materiales, objetos apilados, navegación, rendimiento y estabilidad de física antes de fragmentar todo el catálogo. No hay prefabs fragmentados asignados todavía.
