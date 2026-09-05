# Photopea On Unity

Paquete de Editor para [Unity](https://unity.com) que abre [Photopea](https://www.photopea.com) en el navegador con la textura seleccionada. **Archivo → Guardar** (o el botón *Guardar en Unity*) sobrescribe el asset original del proyecto.

Unity 6 no expone un WebView público, así que Photopea se abre en el navegador. Un servidor HTTP local en `127.0.0.1` recibe el archivo y lo escribe de vuelta en `Assets`.

Repositorio: [Asociacion-Pygenesis/PhotopeaOnUnity](https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity)

## Instalación (Package Manager)

En Unity: **Window → Package Manager → + → Add package from git URL…** y pega:

```
https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git
```

También puedes añadirlo a `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.pygenesis.photopea-on-unity": "https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git"
  }
}
```

Rama concreta:

```
https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git#main
```

### Alternativa: desde disco

**+ → Add package from disk…** y elige el `package.json` de este repositorio.

## Uso

- Menú **Tools → Photopea Editor**
- Clic derecho en una imagen del Project → **Editar en Photopea**

Para guardar en Unity usa **Archivo → Guardar** (`Ctrl+S`) o el botón verde *Guardar en Unity*. No uses *Exportar como…* (eso solo descarga al navegador).

El Editor de Unity debe seguir abierto mientras guardas.

### Formatos

PNG, JPG, PSD/PSB, TGA, TIFF, GIF, BMP, WebP, SVG, IFF, EXR.

## Requisitos

- Unity 2021.3 o posterior (probado en Unity 6)
- Conexión a internet (Photopea se carga desde `photopea.com`)
- Navegador (en Windows se intenta abrir Edge en modo app)

El código es solo de Editor: no se incluye en builds del juego.

## Desarrollo

Estructura del paquete UPM (`package.json` en la raíz, como exige Git URL):

```
PhotopeaOnUnity/
  package.json
  Editor/
    Photopea.cs
    PhotopeaEditor.asmdef
  README.md
  LICENSE
```

## Licencia

MIT. Photopea es un producto de terceros; este paquete solo lo abre y conecta el guardado con Unity.
