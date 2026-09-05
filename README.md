# Photopea On Unity

[English](#english) · [Español](#español)

Unity Editor package that opens [Photopea](https://www.photopea.com) in the browser with the selected texture. **File → Save** (or the *Save to Unity* button) overwrites the original project asset.

---

## English

Unity 6 does not expose a public WebView, so Photopea opens in the browser. A local HTTP server on `127.0.0.1` receives the file and writes it back into `Assets`.

Repository: [Asociacion-Pygenesis/PhotopeaOnUnity](https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity)

### Install (Package Manager)

In Unity: **Window → Package Manager → + → Add package from git URL…** and paste:

```
https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.pygenesis.photopea-on-unity": "https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git"
  }
}
```

Specific branch:

```
https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity.git#main
```

#### Alternative: from disk

**+ → Add package from disk…** and pick the `package.json` in this repository.

### Usage

- Menu **Tools → Photopea Editor**
- Right-click an image in the Project window → **Editar en Photopea**

To save back into Unity, use **File → Save** (`Ctrl+S`) or the green *Guardar en Unity* button. Do not use *Export As…* (that only downloads to the browser).

Keep the Unity Editor open while saving.

#### Formats

PNG, JPG, PSD/PSB, TGA, TIFF, GIF, BMP, WebP, SVG, IFF, EXR.

### Requirements

- Unity 2021.3 or later (tested on Unity 6)
- Internet connection (Photopea loads from `photopea.com`)
- A browser (on Windows, Edge is launched in app mode when available)

Editor-only code: it is not included in player builds.

### Development

UPM package layout (`package.json` at the repo root, as required by Git URL installs):

```
PhotopeaOnUnity/
  package.json
  Editor/
    Photopea.cs
    PhotopeaEditor.asmdef
  README.md
  LICENSE
```

### License

MIT. Photopea is a third-party product; this package only opens it and wires save back to Unity.

---

## Español

Paquete de Editor para [Unity](https://unity.com) que abre [Photopea](https://www.photopea.com) en el navegador con la textura seleccionada. **Archivo → Guardar** (o el botón *Guardar en Unity*) sobrescribe el asset original del proyecto.

Unity 6 no expone un WebView público, así que Photopea se abre en el navegador. Un servidor HTTP local en `127.0.0.1` recibe el archivo y lo escribe de vuelta en `Assets`.

Repositorio: [Asociacion-Pygenesis/PhotopeaOnUnity](https://github.com/Asociacion-Pygenesis/PhotopeaOnUnity)

### Instalación (Package Manager)

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

#### Alternativa: desde disco

**+ → Add package from disk…** y elige el `package.json` de este repositorio.

### Uso

- Menú **Tools → Photopea Editor**
- Clic derecho en una imagen del Project → **Editar en Photopea**

Para guardar en Unity usa **Archivo → Guardar** (`Ctrl+S`) o el botón verde *Guardar en Unity*. No uses *Exportar como…* (eso solo descarga al navegador).

El Editor de Unity debe seguir abierto mientras guardas.

#### Formatos

PNG, JPG, PSD/PSB, TGA, TIFF, GIF, BMP, WebP, SVG, IFF, EXR.

### Requisitos

- Unity 2021.3 o posterior (probado en Unity 6)
- Conexión a internet (Photopea se carga desde `photopea.com`)
- Navegador (en Windows se intenta abrir Edge en modo app)

El código es solo de Editor: no se incluye en builds del juego.

### Desarrollo

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

### Licencia

MIT. Photopea es un producto de terceros; este paquete solo lo abre y conecta el guardado con Unity.
