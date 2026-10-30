# Catálogo de ítems

Las 78 fichas seleccionadas están en `Assets/Proyect/Assets/Items/Catalogo`, separadas por tier. Cada `ItemDefinition` contiene nombre, representación, descripción del efecto, calidad, categoría, valoración, enemigo requerido, notas e icono opcional. Se pueden crear más desde `Assets > Create > Runner > Item de catálogo`.

Son datos de diseño: la descripción del efecto es texto, no una instrucción ejecutable. No están conectadas al `PlayerStatItem` existente y todavía no aplican modificadores, curación, inmunidades ni crean enemigos. Tampoco se han implementado inventario, selección, tienda o pools. `requiredEnemy` documenta el requisito; un futuro sistema de selección deberá comprobarlo, incluyendo enemigos habilitados que todavía no hayan aparecido.

Se respetaron los valores y textos de la selección. En particular, Botas sanguinarias no especifica si pierde 1 punto o 1 % de vida; Núcleo inestable no precisa qué velocidad reduce; Familia orbital solo indica un Flupin adicional; Rabia desesperada necesita precisar su coste. Esas fichas tienen notas para resolverlo antes de implementarlas. Suelas de hielo requiere definir la inmunidad. Los tiers no explícitos son una clasificación inicial editable, no una decisión definitiva.
