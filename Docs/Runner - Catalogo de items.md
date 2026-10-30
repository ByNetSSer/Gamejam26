# Catálogo de ítems

Las 78 fichas seleccionadas están en `Assets/Proyect/Assets/Items/Catalogo`, separadas por tier. Cada `ItemDefinition` contiene nombre, representación, descripción del efecto, calidad, categoría, valoración, enemigo requerido, notas e icono opcional. Se pueden crear más desde `Assets > Create > Runner > Item de catálogo`.

Son datos de diseño: la descripción del efecto es texto, no una instrucción ejecutable. No están conectadas al `PlayerStatItem` existente y todavía no aplican modificadores, curación, inmunidades ni crean enemigos. Tampoco se han implementado inventario, selección, tienda o pools. `requiredEnemy` documenta el requisito; un futuro sistema de selección deberá comprobarlo, incluyendo enemigos habilitados que todavía no hayan aparecido.

Se respetaron los valores y textos de la selección. En particular, Botas sanguinarias no especifica si pierde 1 punto o 1 % de vida; Núcleo inestable no precisa qué velocidad reduce; Familia orbital solo indica un Flupin adicional; Rabia desesperada necesita precisar su coste. Esas fichas tienen notas para resolverlo antes de implementarlas. Suelas de hielo requiere definir la inmunidad. Los tiers no explícitos son una clasificación inicial editable, no una decisión definitiva.


## Adquisiciones en pantalla y maldición de torre (2026-10-30)

`ItemAcquisitionHud` se crea en el jugador al recibir la primera adquisición. Presenta una tarjeta superior de 520 × 156 (referencia 1920 × 1080), icono con salto de 0,5 s, círculo degradado de color, nombre y efecto. Dura 3,6 s y desvanece durante los últimos 0,55 s; usa tiempo no escalado, no captura input y encola adquisiciones simultáneas. `Display Seconds` y `Glow Sprite` se pueden configurar agregando previamente el componente al jugador; sin imagen de resplandor genera un degradado radial.

Orígenes: Tienda (dorado), Maldición (violeta), La torre (rojo), Hallazgo/pickup (turquesa). Se conecta tras compras exitosas, selección de maldición, `StatPickup` y `PlayerStatItem.Apply` (parámetro opcional de origen). Las compras fallidas no notifican. Los iconos provienen de `ItemDefinition.Icon`; pickups también permiten un icono explícito o su SpriteRenderer. Sin imagen se muestra un símbolo provisional y el texto. No se generan miniaturas de modelos 3D. Las descripciones de tienda reflejan los efectos realmente implementados.

Cada dos **torres completas**, no salas, se aplica una sola maldición automática. Prioriza elegir al azar entre adiciones de Flupin/máscara o aumento de frecuencia de Dontsee disponibles. Si todos están al máximo o no hay roster utilizable, elige una penalización de estadísticas. Conserva las maldiciones elegidas al inicio de torre. No aplica la automática al completar la torre final; para 12 torres ocurre en 2, 4, 6, 8 y 10. Existe protección contra repetirla para la misma torre. Los enemigos no se activan en la tienda al conceder esa mejora.

Verificado: compilación runtime con scripts actuales; regla de cadencia 2/4/6/8/10, duplicados y victoria. Pendiente en Unity: compra exitosa/fallida, pickup, varias adquisiciones seguidas, distintas resoluciones, salto/desvanecimiento y legibilidad con movimiento.


### Maldiciones que potencian enemigos

La selección manual y la automática incluyen ahora Flupin impaciente/furioso, Máscaras sin respiro/furiosas, Acecho implacable y Dontsee furioso, siempre que el enemigo correspondiente esté seleccionado. Se aplican a copias existentes y futuras, se acumulan entre torres y usan la misma notificación de nombre/efecto/origen. Ver `Runner - Flupin y enemigos.md` para porcentajes y comportamiento. La alternativa de penalización al jugador solo se usa si no hay opciones de enemigos disponibles.
