namespace BlazorApp_ProductosAPI.Services.AsignarImagenes;

/// <summary>
/// Helper para construir las consultas de búsqueda de imágenes (API DRR /Integration/ImageSearch).
/// Reglas: con código de barra → "barra + descripción completa"; sin barra → solo descripción completa.
/// La descripción no se acorta: siempre se envía íntegra a la API DRR.
/// </summary>
public static class ImageSearchQueryHelper
{
    /// <summary>
    /// Número de palabras de la descripción usadas para la query inicial en el modal (evita enviar texto largo a la API).
    /// Ej.: 1 → "7791337601024 YOGURISIMO"; 2 → "7791337601024 YOGURISIMO C/FRUTAS"
    /// </summary>
    public const int NumPalabrasDescripcionQueryInicial = 2;

    /// <summary>
    /// Construye la query de búsqueda para la API: código de barra (si existe) + espacio + descripción.
    /// Sin código de barra se usa únicamente la descripción.
    /// Ej.: "7791337601024 YOGURISIMO C/FRUTAS REGIOLANES FRUTILLA X 150G" o solo "YOGURISIMO C/FRUTAS..."
    /// </summary>
    /// <param name="codigoBarra">Código de barra del producto (puede ser null o vacío).</param>
    /// <param name="descripcion">Descripción del producto (obligatoria para tener resultado no vacío).</param>
    /// <returns>Query lista para enviar a la API, o vacío si no hay descripción.</returns>
    public static string ConstruirQuery(string? codigoBarra, string? descripcion)
    {
        var barra = (codigoBarra ?? "").Trim();
        var desc = (descripcion ?? "").Trim();
        if (string.IsNullOrWhiteSpace(desc))
            return string.Empty;
        return string.IsNullOrWhiteSpace(barra) ? desc : $"{barra} {desc}";
    }

    /// <summary>
    /// Construye la query inicial corta para el modal y la primera llamada a la API: código de barra + primeras N palabras de la descripción.
    /// Así la API recibe por ejemplo "7791337601024 YOGURISIMO" y no la descripción completa.
    /// El usuario puede editar el textbox y usar "Buscar de nuevo" para enviar exactamente lo que escribe.
    /// </summary>
    /// <param name="codigoBarra">Código de barra (puede ser null o vacío).</param>
    /// <param name="descripcion">Descripción completa del producto.</param>
    /// <param name="numPalabras">Número de palabras a tomar de la descripción (por defecto <see cref="NumPalabrasDescripcionQueryInicial"/>).</param>
    /// <returns>Query corta, ej. "7791337601024 YOGURISIMO" o "7791337601024 YOGURISIMO C/FRUTAS".</returns>
    public static string ConstruirQueryInicialCorta(string? codigoBarra, string? descripcion, int? numPalabras = null)
    {
        var barra = (codigoBarra ?? "").Trim();
        var desc = (descripcion ?? "").Trim();
        if (string.IsNullOrWhiteSpace(desc))
            return string.IsNullOrWhiteSpace(barra) ? string.Empty : barra;
        var n = numPalabras ?? NumPalabrasDescripcionQueryInicial;
        var palabras = desc.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var parteDesc = n <= 0 || n >= palabras.Length
            ? desc
            : string.Join(" ", palabras.Take(n));
        return string.IsNullOrWhiteSpace(barra) ? parteDesc : $"{barra} {parteDesc}";
    }

    /// <summary>
    /// Genera variantes de descripción para reintentos cuando la API no devuelve imágenes.
    /// Orden: descripción completa, luego primeras 8, 5, 3 y 2 palabras (sin duplicados).
    /// Se usa en búsqueda masiva y en "Buscar de nuevo" del modal.
    /// </summary>
    /// <param name="descripcionCompleta">Descripción completa del producto.</param>
    /// <returns>Lista de variantes (completa primero, luego abreviadas).</returns>
    public static IReadOnlyList<string> ObtenerVariantesDescripcion(string descripcionCompleta)
    {
        var d = (descripcionCompleta ?? "").Trim();
        if (string.IsNullOrWhiteSpace(d))
            return Array.Empty<string>();

        var palabras = d.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var resultado = new List<string> { d };
        foreach (var numPalabras in new[] { 8, 5, 3, 2 })
        {
            if (palabras.Length <= numPalabras)
                continue;
            var acortada = string.Join(" ", palabras.Take(numPalabras)).Trim();
            if (!string.IsNullOrWhiteSpace(acortada) && !resultado.Contains(acortada))
                resultado.Add(acortada);
        }
        return resultado;
    }

    /// <summary>
    /// A partir del texto libre del modal (código de barra + descripción o solo descripción),
    /// genera la secuencia de queries a intentar: texto completo y, si el primer token parece código de barra, solo descripción (completa).
    /// No se acorta la descripción: siempre se envía barra + descripción completa a la API DRR.
    /// </summary>
    /// <param name="textoBusqueda">Texto tal cual está en el textbox (barra + desc o solo desc).</param>
    /// <returns>Orden de queries a probar contra la API.</returns>
    public static IReadOnlyList<string> ObtenerQueriesFallbackParaTextoLibre(string textoBusqueda)
    {
        var texto = (textoBusqueda ?? "").Trim();
        if (string.IsNullOrWhiteSpace(texto))
            return Array.Empty<string>();

        var queries = new List<string> { texto };
        var rest = texto.Trim();
        var firstSpace = rest.IndexOf(' ');

        if (firstSpace > 0)
        {
            var posibleBarra = rest.Substring(0, firstSpace);
            var restoDescripcion = rest.Substring(firstSpace + 1).Trim();
            // Si el primer token parece código de barra (8+ dígitos), añadir fallback "solo descripción" (completa, sin acortar).
            if (posibleBarra.Length >= 8 && posibleBarra.All(char.IsDigit) &&
                !string.IsNullOrWhiteSpace(restoDescripcion) && !queries.Contains(restoDescripcion))
            {
                queries.Add(restoDescripcion);
            }
        }

        return queries;
    }

    /// <summary>
    /// Indica si el primer token del texto parece un código de barra (8+ dígitos).
    /// Útil para decidir si probar fallback "solo descripción".
    /// </summary>
    public static bool PrimerTokenPareceCodigoBarra(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;
        var firstSpace = texto.Trim().IndexOf(' ');
        var firstToken = firstSpace > 0 ? texto.Trim().Substring(0, firstSpace) : texto.Trim();
        return firstToken.Length >= 8 && firstToken.All(char.IsDigit);
    }
}
