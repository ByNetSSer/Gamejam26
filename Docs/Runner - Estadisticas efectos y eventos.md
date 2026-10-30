---
tags: [gamejam, unity, efectos, estadísticas, programación]
estado: implementado-parcial
---
# ⏱️ Estadísticas, efectos y eventos

> [!abstract] Propósito
> Adaptación del sistema del proyecto RPG al runner: estadísticas por jugador, efectos con vida propia y temporizadores gestionados desde un único punto del PlayerLoop.

## 🗂️ Organización del código

- `Systems/Stats`: `StatValue` calcula modificadores y `StatModifierType` define su tipo; no conoce al jugador.
- `Systems/Timers`: `Timer` administra su ciclo y `TimerScheduler` actualiza los temporizadores activos.
- `Systems/Effects`: `StatEffect` aplica y retira un modificador sobre cualquier `StatValue`.
- `Player`: `PlayerStats`, `PlayerStatEffects`, `PlayerEvents` y `PlayerEffectTrigger` conectan los sistemas reutilizables con las reglas específicas del jugador.
- `Items`: `PlayerStatItem` define modificaciones de estadísticas del jugador.

Se movieron los archivos junto a sus `.meta` para preservar referencias de Unity. El prefijo `Player` identifica lo que depende de las estadísticas o eventos del jugador.

## 🧩 Estadísticas

`PlayerStats` mantiene una `StatValue` por estadística. Cada modificador tiene una fuente de ejecución; retirar una fuente no elimina los modificadores de otras copias.

**Orden:** `(base + planos) × (1 + suma de porcentajes aditivos) × producto de multiplicadores`. El resultado se limita como mínimo a cero. `Percent` conserva su valor serializado anterior; se añadió `PercentMult`. Los porcentajes se escriben como fracciones: `0.2` = +20 %.

La base puede cambiar en cada evaluación, como sucede al aumentar la velocidad por sala; los modificadores no alteran los assets. `StatsCalculator` del RPG dependía de personajes, niveles, inventario y equipamiento del otro juego, por lo que no se trasladó.

## ⏳ Temporizadores

`TimerScheduler` se inserta una sola vez en el PlayerLoop y actualiza los temporizadores activos con tiempo escalado (`Time.deltaTime`). Los temporizadores pertenecen a cada efecto, se cancelan de forma explícita y no requieren un `Update` individual. Un temporizador admite duración y, opcionalmente, intervalos; `0` significa sin intervalos, y valores positivos menores a 0.001 se rechazan.

Los intervalos atrasados se procesan hasta 256 por actualización para evitar bloqueos. Si faltan, la finalización espera a que se procesen. Al pausar con `timeScale = 0`, el tiempo de efectos también se pausa. Cancelar no equivale a completar; el dueño hace la limpieza.

## ✨ Efectos

`PlayerStatEffects.Apply(stat, tipo, valor, duración)` crea una instancia independiente y devuelve su referencia. Duración positiva: temporal; negativa: permanece hasta cancelarla; cero: termina inmediatamente. Al finalizar o desactivar el receptor se retira únicamente su modificador y se detiene el temporizador. Las aplicaciones temporales simultáneas **se acumulan**; límites de stacks o refresco son reglas futuras.

## ⚡ Eventos y activadores

`PlayerEvents` comunica sala superada y daño recibido. `PlayerEffectTrigger` permite configurar en el inspector:

- **RoomCleared:** aplica un modificador temporal por cada sala notificada.
- **DamageReceived:** aplica uno al recibir una notificación de daño aceptado.
- **SpeedPresence:** mantiene un modificador mientras la velocidad horizontal alcanza el umbral y lo retira al bajar.

Para probar, agrega `PlayerEffectTrigger` al jugador y configura tipo, stat, valor y duración o umbral. Los componentes requeridos se agregan automáticamente. Al desactivar el activador se cancelan sus efectos. El gestor de salas todavía debe invocar `RegisterRoomCleared()` exactamente una vez por transición; el futuro sistema de salud debe invocar `PlayerEvents.NotifyDamage(cantidad)` después de aplicar daño real. No hay salud ni generación automática implementadas.

Los ítems `PlayerStatItem` existentes siguen aplicando modificadores permanentes mediante su API. **La tienda/inventario aún no instala los activadores**: conectar su creación, propiedad y retirada es trabajo pendiente. La comprobación de velocidad se hace en el `Update` del activador de presencia; el temporizador no tiene un `Update` individual.

## 🔍 Hallazgos del código RPG

- El `SetRange` de `Stat` no aplicaba el rango durante el recálculo; aquí las estadísticas se limitan a cero como mínimo.
- El temporizador de intervalo original no validaba un intervalo cero, lo que podía provocar un bucle infinito. La implementación nueva lo evita y limita trabajo acumulado.
- Algunos efectos originales podían limpiar o notificar finalización más de una vez; aquí la cancelación es idempotente.
- El efecto de modificador por intervalos tenía la aplicación del modificador comentada. Aquí existe el temporizador por intervalos, pero un efecto de acumulación periódica sigue pendiente.
- El bootstrap original importaba `UnityEditor` desde código de runtime; la adaptación no lo necesita.

## 🧪 Verificación

Los proyectos C# de runtime y editor compilan. En Unity, fuera de Play Mode, **Tools → Runner → Verificar estadísticas y efectos** ejecuta verificaciones de orden de modificadores, eliminación por fuente, intervalos, expiración y cancelación. Debe ejecutarse y complementarse con pruebas reales de eventos en Play Mode; esas pruebas todavía no se han realizado.
