---
tags: [gamejam, unity, colisiones, superficies]
estado: pendiente-playtest
---
# 🛠️ Superficies e impactos anticipados

Esta nota actualiza [[Runner - Movimiento y superficies]]: las superficies ya no dependen de callbacks `OnTriggerStay`.

## Suelos

`GroundSurfaceSensor` consulta una esfera debajo del `CharacterController` al final de cada frame cuando el jugador está apoyado. Busca colliders de `GroundSurface`, incluidos triggers, aplica cada superficie al entrar y retira su fuente al salir o saltar. Cada superficie conserva sus modificadores configurables y controla el intervalo de daño; la lava necesita `PlayerHealth`. El HUD de prueba muestra la zona detectada y la vida.

La comprobación usa buffers reutilizables y una consulta ampliada si hay muchos colliders. Los parámetros de la sonda se editan en el componente. Está pensado para zonas poco altas a nivel del suelo; probar bordes, saltos, superficies superpuestas y movimientos rápidos en Play Mode.

## Impacto antes de frenar

Antes de cada subpaso del movimiento, el jugador barre su cápsula en la dirección del desplazamiento. Los contactos se procesan del más cercano al más lejano. Si el objeto tiene umbral de rotura suficiente, o el daño previsto agotaría su vida, se rompe y desactiva antes de mover el controlador. El pisotón sobre una escotilla usa la misma comprobación. Si un objeto resiste o hay una pared, termina la predicción: no se rompe nada detrás de ese bloqueo. Los impactos que no rompen todavía se procesan al colisionar.

La predicción usa la velocidad perpendicular a la superficie y respeta el intervalo de daño. No garantiza comportamiento perfecto para cualquier geometría, paso o velocidad; comprobar superficies finas, colisiones oblicuas y varios objetos juntos. Las consultas usan un buffer y amplían solo cuando se llena.

## Verificación pendiente

Los scripts de runtime y editor compilan. No se ha reproducido en Play Mode la reparación ni validado el comportamiento real de la física. La escena abierta del usuario tenía cambios sin guardar y no se sustituyó. Abrir la escena de prueba, pisar hielo/brea/lava y verificar la lectura del HUD; correr hacia cajas con y sin velocidad suficiente, y pisotear la escotilla.
