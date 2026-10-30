---
tags: [gamejam, enemigos, estadisticas, modificadores]
estado: prototipo
actualizado: 2026-10-28
---
# Estadísticas y mejoras de enemigos

## Objetivo y alcance

Los enemigos con estadísticas deben poder recibir mejoras o maldiciones elegidas por el jugador. La implementación actual prepara los valores y modificadores; todavía no hay selección, catálogo, tienda ni sistema que aplique automáticamente una elección a los enemigos presentes y futuros. Las reglas de balance y las opciones concretas aún no están decididas.

## Contrato para futuros enemigos

- Mantener valores base serializados configurables en el inspector y un enum propio que identifique las estadísticas relevantes de ese enemigo.
- Usar `StatValue` por instancia, exponiendo `GetStat`, `AddModifier(source, stat, type, value)` y `RemoveModifiers(source)`, como Flupin y Dontsee. No compartir accidentalmente modificadores entre instancias ni modificar el prefab en ejecución.
- Cada efecto usa una fuente propia; retirar una fuente deja intactas las demás. `Flat`, `Percent` y `PercentMult` se calculan en ese orden: `(base + suma Flat) * (1 + suma Percent) * producto(1 + PercentMult)`, con resultado no negativo. Aplicar además límites propios de la regla (por ejemplo, fracción de daño entre 0 y 1).
- Documentar qué valores se leen continuamente y cuáles se fijan al iniciar una fase. Los efectos temporales pueden usar `StatEffect` y deben cancelarse o retirarse según su ciclo de vida.
- Separar las estadísticas de un enemigo vivo de las que determinan su generación. Un enemigo temporal no puede alterar su propia probabilidad antes de existir; esa probabilidad pertenece al componente de encuentros o al futuro director.
- Conservar el turno de `EnemyDirector` mientras exista una amenaza activa, con avisos y descansos razonables. El aumento de frecuencia o agresividad no sustituye la coordinación ni resuelve conflictos con trampas, que aún no participan en ella.
- Al implementar el futuro sistema de elecciones, definir explícitamente duración, acumulación, alcance y aplicación a enemigos actuales y futuros. Evitar añadir dos veces una misma fuente. Un receptor persistente puede escuchar la aparición, aplicar los modificadores correspondientes y retirar sus suscripciones al desactivarse. No existe aún un contrato genérico unificado entre los enums de enemigos.

## Flupin

`FlupinStat` y los métodos del propio `Flupin` ya permiten modificar cada instancia. `Aggressiveness` multiplica la velocidad radial y el ritmo de sus esperas; no cambia el daño. Las demás estadísticas incluyen daño, velocidades y tiempos. Consultar [[Runner - Flupin y enemigos]] para el detalle y sus límites.

## Dontsee

`DontseeStat` ofrece `Aggressiveness`, `ApproachSpeed`, `CurrentHealthFraction`, `WarningDuration` y `ContactRadius`. La agresividad tiene base 1 y mínimo efectivo 0,1: multiplica la velocidad de acercamiento y el ritmo del aviso, sin cambiar el daño ni la probabilidad de aparición. Velocidad, agresividad, daño y radio se evalúan durante la amenaza; la duración base del aviso se calcula al recibir turno y los cambios posteriores de agresividad alteran el ritmo del aviso restante. La fracción de daño se limita a 0–1 de la vida actual. La mirada sigue deteniendo el avance y el turno se conserva hasta finalizar.

`DontseeEncounterStat.SpawnChance` modifica la probabilidad del componente de encuentros, con resultado limitado a 0–1. Se evalúa al entrar en cada sala elegible; no cambia una selección ya hecha. La separación configurada de salas sin encuentros sigue vigente. La probabilidad no equivale a una frecuencia garantizada: también requiere marcadores y depende del azar. El modificador pertenece al gestor de esa torre, no a cada Dontsee creado.

```csharp
// Fuentes separadas permiten retirar cada elección por separado.
object eleccion = new object();
dontsee.AddModifier(eleccion, DontseeStat.ApproachSpeed, StatModifierType.Percent, 0.25f);
dontsee.AddModifier(eleccion, DontseeStat.CurrentHealthFraction, StatModifierType.Flat, 0.05f);
encuentros.AddModifier(eleccion, DontseeEncounterStat.SpawnChance, StatModifierType.Percent, 0.5f);
// Al terminar la elección, si corresponde:
dontsee.RemoveModifiers(eleccion);
encuentros.RemoveModifiers(eleccion);
```

Una elección que afecte a todos los Dontsee futuros necesita conservarse en un sistema persistente y aplicarse a cada `DontseeSpawnedEvent` de `RoomManager.Events`. La suscripción debe estar activa antes de la aparición. El evento se despacha en cola si aparece durante otro evento del bus; se publica antes de que Dontsee pueda recibir su turno. El sistema de elecciones no está implementado.

## Verificación pendiente

La compilación runtime/editor pasó. Faltan pruebas en Play Mode para modificadores combinados, retirada por fuente, cambios durante el aviso y el acercamiento, probabilidad entre salas, interacción con Flupin y balance de daño. Los buffs pueden hacer más difícil la partida; validar las elecciones y la legibilidad con jugadores antes de fijar valores.
