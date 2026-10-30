# Renderizado selectivo pixelado

La feature `SelectivePixelationFeature` renderiza objetos opacos de capas elegidas en una textura de baja resolución y los compone con filtrado Point después del skybox. Compara y escribe la profundidad en la cámara para respetar la oclusión con la escena y los demás grupos. No requiere un shader especial en los materiales: se pueden usar materiales compatibles con URP Forward.

## Configuración

Los assets `Assets/Settings/PC_Renderer.asset` y `Assets/Settings/Mobile_Renderer.asset` incluyen tres instancias independientes:

| Capa | Tamaño inicial (ancho × alto) | Inspector |
| --- | --- | --- |
| PixelatedFine (9) | 3 × 3 | Selective Pixelation Fine |
| Pixelated (8) | 6 × 6 | Selective Pixelation |
| PixelatedLarge (10) | 10 × 10 | Selective Pixelation Large |

Las tres capas están excluidas de Opaque Layer Mask y Prepass Layer Mask para evitar un segundo dibujo a resolución completa y siluetas de profundidad o de oclusión ambiental sobre otros objetos. La capa FlupinOverlay también está excluida de ambas máscaras. Cada feature debe conservar su máscara de capa y su referencia al shader `Hidden/SelectivePixelationComposite`. Modificar Pixel Width y Pixel Height en la instancia correspondiente para ajustar cada grupo. Un objeto cambia de grupo al cambiar su Layer; los hijos con renderizadores necesitan la misma capa. La cámara debe incluir estas capas en su Culling Mask. Las configuraciones de PC y móvil se editan por separado.

## Limitaciones

- Solo se dibujan objetos opacos con pases UniversalForward, UniversalForwardOnly o SRPDefaultUnlit. Los transparentes no están soportados.
- La profundidad de cada grupo tiene la misma resolución reducida que su color: en bordes e intersecciones puede haber escalones y pequeñas discrepancias con la geometría de resolución completa.
- Cada grupo activo añade una pasada y una textura, incluso si no hay objetos visibles de esa capa.
- Las texturas de profundidad generadas por URP antes de esta composición pueden no incluir estos objetos; probar los efectos que dependan de ellas.
- Los materiales con ajuste de vértices o cuantización de textura propio pueden añadir un aspecto PS1; para esta técnica usar preferentemente materiales URP normales.
- Comprobar visualmente en Unity los cruces entre grupos, las paredes y los objetos transparentes antes de usar en producción.

## Superposición por material

El shader `StylizedPaper/ScreenPixelatedObject`, usado por el material `pixel 1`, ofrece **Draw On Top**. Activarlo en una copia del material para dibujar esa malla sin comprobar la profundidad de la escena y en la cola Overlay; desactivado conserva su comportamiento transparente normal. La opción afecta a todos los objetos que comparten el material. Los objetos en la misma cola pueden ordenarse entre sí por distancia, y otros pases posteriores pueden dibujarse encima. No cambia la feature de Flupin ni su capa dedicada.

Código: `Assets/Proyect/Assets/Scripts/Rendering/SelectivePixelationFeature.cs`. Shader de composición: `Assets/Proyect/Assets/Shaders/SelectivePixelationComposite.shader`.
