# README – Búsqueda de imágenes en Google (código de barras y descripción)

Documentación de la lógica usada en el proyecto para buscar imágenes de productos en Google usando **código de barras** y **descripción del producto**.

---

## 1. API utilizada

- **Nombre:** Google Custom Search JSON API (búsqueda de imágenes).
- **Documentación oficial:** [Custom Search JSON API](https://developers.google.com/custom-search/v1/overview).
- **Tipo de uso:** Búsqueda de imágenes por texto (en este caso, código de barras y/o descripción).

---

## 2. URL base y parámetros

**URL base:**

```
https://www.googleapis.com/customsearch/v1?
```

**Parámetros utilizados:**

| Parámetro     | Obligatorio | Descripción |
|---------------|-------------|-------------|
| `key`         | Sí          | API Key de Google (ver sección de tokens). |
| `cx`          | Sí          | ID del motor de búsqueda personalizado (Custom Search Engine). |
| `q`           | Sí          | Texto de búsqueda (código de barras, descripción o ambos). |
| `searchType`  | Sí (imagen) | Valor fijo: `image`. |
| `fileType`    | Opcional    | En el código: `jpeg,png`. |

**Ejemplo de URL completa (sin codificar):**

```
https://www.googleapis.com/customsearch/v1?key=TU_API_KEY&cx=012225991981024570250:pketlhy4f0h&q=7891234567890+COCA+COLA+500ML&searchType=image&fileType=jpeg,png
```

- El valor de `q` debe ir **codificado en URL** (espacios como `+` o `%20`, etc.) si se arma la petición a mano.

---

## 3. Tokens (API Keys) y Search Engine ID

### 3.1 API Keys (parámetro `key`)

En el código se usan **varios tokens** en rotación. Si uno falla o supera el límite, se prueba con el siguiente.

**Lista de tokens configurados** (clase `LibreriaBusquedaGoogle`, constructor):

| # | Token (API Key) |
|---|------------------|
| 1 | `AIzaSyAtCM8IhepCqm3Q4dTtjMMbkswS7uhmOPA` |
| 2 | `AIzaSyD9gYtuCXPT5uE13GFXgwJmyeQSvcRxEK0` |
| 3 | `AIzaSyBlT4TAoetmH3jskBvikwr-QAxHZFdujBs` |
| 4 | `AIzaSyDX53Ohye3jvoReOIra0_NewoyOH7KZkbU` |
| 5 | `AIzaSyBc74LJwuRkjLpJnNhOFzuDYo2mcMgYJjg` |
| 6 | `AIzaSyBNdCJH18Kj8WzH6THTfabYinq8c1uLs1M` |
| 7 | `AIzaSyDzd8ayT8mUjiVYI7KhbGIukRHKFYyS_rU` |
| 8 | `AIzaSyAeIUsFPz9IEtTmJvw5qwMc9-vJZAsheg4` |
| 9 | `AIzaSyBL3onWqLLZ6HN_fjQHkfaaY8_wOiFfpFA` |
| 10 | `AIzaSyCKhhfGId2I1QcMBeAoUlsAaWk8TAm6YdA` |



AIzaSyAtCM8IhepCqm3Q4dTtjMMbkswS7uhmOPA, 
AIzaSyD9gYtuCXPT5uE13GFXgwJmyeQSvcRxEK0, 
AIzaSyBlT4TAoetmH3jskBvikwr-QAxHZFdujBs,
AIzaSyDX53Ohye3jvoReOIra0_NewoyOH7KZkbU, 
AIzaSyBc74LJwuRkjLpJnNhOFzuDYo2mcMgYJjg, 
AIzaSyBNdCJH18Kj8WzH6THTfabYinq8c1uLs1M,
AIzaSyDzd8ayT8mUjiVYI7KhbGIukRHKFYyS_rU, 
AIzaSyAeIUsFPz9IEtTmJvw5qwMc9-vJZAsheg4,
AIzaSyBL3onWqLLZ6HN_fjQHkfaaY8_wOiFfpFA,
AIzaSyCKhhfGId2I1QcMBeAoUlsAaWk8TAm6YdA

- **Base de datos local (no Azure):** se usan solo los tokens 1–7.
- **Azure:** se usan los 10.
- Si un token devuelve error más de 3 veces, se marca como inactivo y no se vuelve a usar en esa sesión.

### 3.2 Search Engine ID (parámetro `cx`)

**Valor por defecto en el código:**

```
012225991981024570250:pketlhy4f0h
```

- Corresponde a un **Custom Search Engine** configurado en [Programmable Search Engine](https://programmablesearchengine.google.com/).
- Para búsqueda de imágenes, ese motor debe tener habilitada la búsqueda de imágenes.

---

## 4. Cómo se arma el texto de búsqueda (código de barras y descripción)

El texto que se envía en `q` se forma así:

### 4.1 En Alta de producto (Punto de venta)

- **Archivo:** `AlmaNET_App\Windows\PuntoVenta\AltaProducto.xaml.cs`
- **Formato:**  
  `[Código de barras] | [Descripción larga del producto]`  
  Ejemplo: `7891234567890 | Coca Cola 500ml`

- Se rellena en el manejador `Image1_BeforeSearchImageWeb`:
  - `e.TextoBuscar = txtCodigoBarra.Text + " | " + productoActual.DescripcionLarga;`
- Solo se permite buscar cuando el producto está en estado 1 (existente) o 2 (nuevo).

### 4.2 En ventana Producto (ABM)

- **Archivo:** `AlmaNET_App\Windows\Producto\Producto.xaml.cs`
- **Método:** `getTextBeforeImageSearch()`
- **Lógica:**
  - Se obtiene el código de barras de la presentación del producto (presentación 1 o 0).
  - Si hay código de barras:  
    `[Código de barras] | [Descripción larga base del producto]`  
  - Si no hay código: solo `[Descripción larga base del producto]`.

En ambos casos, ese texto es el que después se usa como `q` (o se divide por `|` para hacer varias búsquedas; ver siguiente sección).

---

## 5. Flujo paso a paso (de la pantalla a Google)

### Paso 1: Usuario pide “Buscar imagen web”

- El usuario está en:
  - **Alta producto** (PuntoVenta): ventana `AltaProducto`, control de imagen del producto, o  
  - **Producto:** ventana `Producto`, imagen del producto base o colección de imágenes.
- En el menú contextual del control de imagen elige **“Buscar imagen web”**.

### Paso 2: Control ImageComplex dispara el evento

- **Archivo:** `LibreriasTony\Controles\ImageComplex.xaml.cs`
- Método: `buscarImagenWeb()`
  - Crea `BeforeSearchImageWebEventArgs`.
  - Dispara el evento `BeforeSearchImageWeb`.
  - La ventana (AltaProducto o Producto) asigna en el evento:  
    `e.TextoBuscar = [código de barras] + " | " + [descripción]` (o solo descripción si no hay código).
  - Si `e.Cancelar` es true, se corta el flujo.
  - Si no, se crea `ImageSearch.ParameterArgs` con `TextoBuscar = e.TextoBuscar` y se abre la ventana `ImageSearch`.

### Paso 3: Ventana ImageSearch ejecuta la búsqueda

- **Archivo:** `LibreriasTony\Controles\ImageSearch.xaml.cs`
- Al cargar (o al pulsar Buscar):
  - Se toma el texto de búsqueda (ej. `7891234567890 | Coca Cola 500ml`).
  - Se llama a `buscarImagenes()`, que lanza un `BackGroundWorker`.
- En `Bgw_DoWork`:
  - Se hace `textoBuscar.Split('|')` → por ejemplo: `["7891234567890 ", " Coca Cola 500ml"]`.
  - Para **cada parte** (trimmed):
    - Se obtiene `LibreriaBusquedaGoogle.Instancia`.
    - Se llama `libreriaBusquedaGoogle.Busqueda(str)`.
  - Para cada `item` en `respuestaApi.items` se llama `GetImage(item.link)` para descargar la imagen.
- Se muestran hasta 10 imágenes; el usuario elige una (o varias según modo).

### Paso 4: LibreriaBusquedaGoogle llama a la API de Google

- **Archivo:** `LibreriaLuis\Clases\LibreriaBusquedaGoogle.cs` (o equivalente en `LibreriasTony\Clases\LibreriaBusquedaGoogle.cs`)
- `Busqueda(datos)`:
  - Recorre la lista de tokens (7 o 10 según si es Azure o no).
  - Para cada token no inactivo:
    - Crea `ParametrosBusqueda`: Token, SearchEngineID por defecto, Datos = texto a buscar, Imagenes = true.
    - Llama a `ObtenerImagenes(parametros)`.
  - Si la respuesta es válida, la devuelve y deja de intentar con más tokens.
- `ObtenerImagenes(parametros)`:
  - Construye la URL:  
    `https://www.googleapis.com/customsearch/v1?key=...&cx=...&q=...&searchType=image&fileType=jpeg,png`
  - Realiza `HttpWebRequest` a esa URL.
  - Lee el JSON de la respuesta y lo deserializa a `RespuestaApi`.
  - Devuelve `RespuestaApi` (con la lista `items`).

### Paso 5: Descarga de cada imagen

- En `ImageSearch.xaml.cs`, método `GetImage(string url)`:
  - Se ignora si `url` contiene `.pdf` o empieza por `x-raw-image:`.
  - Con `WebClient.DownloadData(url)` se descargan los bytes.
  - Se valida que sean una imagen (p. ej. `System.Drawing.Image.FromStream`).
  - Se guardan en `ArrayImage1` … `ArrayImage10` y se muestran en la ventana.

### Paso 6: Usuario acepta y se asigna la imagen

- Al cerrar `ImageSearch` con Aceptar:
  - La imagen seleccionada se devuelve en `args.SelectedItem` (o en `SelectedItems`).
  - `ImageComplex.buscarImagenWeb()` asigna esa imagen al producto (o la redimensiona si está activada la opción “Reducir”).

---

## 6. Respuesta de la API (estructura relevante)

La API devuelve JSON que se deserializa a la clase `RespuestaApi`. Para las imágenes interesa sobre todo:

- `items`: lista de resultados.
- Cada `item` tiene:
  - `link`: URL de la imagen (la que se usa para descargar en `GetImage`).
  - `title`, `snippet`, `image` (thumbnailLink, width, height, etc.).

Clases en `LibreriaBusquedaGoogle.cs`: `RespuestaApi`, `Item`, `Image`, `SearchInformation`, `Queries`, etc.

---

## 7. Archivos involucrados (resumen)

| Archivo | Responsabilidad |
|---------|------------------|
| `AlmaNET_App\Windows\PuntoVenta\AltaProducto.xaml.cs` | Formar texto “código de barras \| descripción” y suscribir `BeforeSearchImageWeb` en la imagen. |
| `AlmaNET_App\Windows\Producto\Producto.xaml.cs` | `getTextBeforeImageSearch()`, manejadores `BeforeSearchImageWeb` para imagen y colección. |
| `LibreriasTony\Controles\ImageComplex.xaml.cs` | Menú “Buscar imagen web”, evento `BeforeSearchImageWeb`, apertura de `ImageSearch`, asignación de imagen elegida. |
| `LibreriasTony\Controles\ImageSearch.xaml.cs` | Ventana de búsqueda: partir texto por `\|`, llamar a `LibreriaBusquedaGoogle.Busqueda`, descargar imágenes con `GetImage(item.link)`. |
| `LibreriaLuis\Clases\LibreriaBusquedaGoogle.cs` | Singleton, lista de tokens, `Busqueda()`, `ObtenerImagenes()`, construcción de URL y petición HTTP a Google. |
| `LibreriasTony\Clases\LibreriaBusquedaGoogle.cs` | Versión alternativa/copia de la misma lógica (mismo API, mismos tokens y `cx`). |

---

## 8. Configuración opcional (JSON)

La clase `Conf_Automatica_BusquedaGoogle` en `LibreriaBusquedaGoogle.cs` permite guardar y leer configuración desde un archivo:

- **Ruta del archivo:** `[Carpeta del ejecutable]\jsonConfigBusquedaGoogle.txt`
- **Contenido (JSON):** `Token` y `SearchEngineID`.
- **Métodos:** `GenerarJson()` para guardar, `ConvertirClass()` para leer.

En el flujo actual, **no se usa este JSON**: los tokens y el `cx` por defecto están fijados en código (lista de tokens y `ParametrosBusqueda.SearchEngineID = "012225991981024570250:pketlhy4f0h"`).

---

## 9. Resumen rápido

- **API:** Google Custom Search JSON API (`https://www.googleapis.com/customsearch/v1`).
- **Parámetros:** `key` (API Key), `cx` (ID del motor de búsqueda), `q` (código de barras y/o descripción), `searchType=image`, `fileType=jpeg,png`.
- **Tokens:** 10 API Keys en rotación (7 en entorno no Azure).
- **Search Engine ID:** `012225991981024570250:pketlhy4f0h`.
- **Texto de búsqueda:** `[Código de barras] | [Descripción del producto]` (o solo descripción), formado en AltaProducto o Producto y pasado a ImageSearch → LibreriaBusquedaGoogle → Google.

Si necesitas cambiar tokens, límites o el motor de búsqueda, los puntos a tocar son la lista de tokens en el constructor de `LibreriaBusquedaGoogle` y la propiedad `SearchEngineID` en `ParametrosBusqueda`.
