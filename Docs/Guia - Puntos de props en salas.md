# Guía para colocar props en salas

Ejemplo: abre `Assets/PrefabsMapas/Bajito/P-P/Acueducto-P-P.prefab` en Prefab Mode y despliega `Marcadores de contenido`. Las seis posiciones activas restauradas son provisionales: dos para candelabros, dos para libros/copas y dos para mobiliario. Hay también dos marcadores vacíos de posibles armas, sin comportamiento. La geometría pudo cambiar con el package: revisa y mueve todos los puntos antes de utilizarlos.

Con Gizmos activado en la vista Scene verás cajas de alambre sobre los puntos activos. Son una **referencia aproximada de espacio libre**, no dimensiones de los modelos ni una prueba de colisiones. La flecha indica su orientación local; al seleccionar un punto aparece su nombre. El tamaño real depende del prefab que salga. Las plantillas inactivas no se dibujan hasta activarlas.

Para añadir puntos propios:

1. Dentro de `Marcadores de contenido`, duplica `Pool pequeño - colocar y activar`, `Pool mediano - colocar y activar` o `Pool grande - colocar y activar`. Están desactivados y en el origen local a propósito.
2. Activa la copia y colócala sobre el suelo, con espacio suficiente alrededor y encima. Evita puertas, trampillas, pasillos y otros puntos. Rota su eje azul (+Z) hacia la dirección en la que deben mirar los props. Comprueba la altura y los pivotes de los modelos: el punto coloca el origen del prefab en su posición.
3. En `Room Spawn Point`, revisa `Prop Pool` (el catálogo `PropStackPool`), `Allowed Sizes`, `Chance` y `Pool Local Offset`. Se pueden elegir varios tamaños; el pool elige un prefab con su peso. El valor `Chance` de 0,6 equivale a 60 % en la primera torre y aumenta según la dificultad de torre.
4. Guarda el prefab. La sala necesita `RoomDefinition` y debe estar asignada al pool de salas del `RoomManager` para que `Populate` ejecute estos puntos. Los vacíos inactivos no generan objetos. Duplicar la plantilla en otra sala no configura automáticamente su entrada, salida ni su inclusión en el gestor.
5. Prueba en Play Mode varias semillas, velocidades y torres. Comprueba que haya un camino libre, que no aparezcan objetos dentro de paredes o sobre huecos y que las pilas no atraviesen el techo.

Un objeto grande con `PropStackPoint` puede generar otros encima mediante el mismo catálogo; su probabilidad y límite de profundidad se configuran en el prefab del objeto, no en el punto de sala. Las cajas de alambre no garantizan el espacio para esos objetos adicionales.

Las salas nuevas en `Assets/PrefabsMapas/Medio` no tenían estas posiciones antes de importar el package: usa el ejemplo para colocarlas manualmente según su geometría. La aparición de armas de pared y su ataque siguen pendientes.
