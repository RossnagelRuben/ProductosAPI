using System.Text;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Convierte RTF (Rich Text Format) a HTML para mostrar vista previa en el modal de Observaciones.
/// Soporta negritas, cursivas, subrayado, párrafos, viñetas y caracteres escapados \'XX.
/// Nota: en Blazor WebAssembly solo está disponible UTF‑8; los bytes \'XX se interpretan como Latin‑1.
/// </summary>
public static class RtfToHtmlConverter
{

    /// <summary>Convierte una cadena RTF a HTML seguro (solo tags permitidos). Si no parece RTF, escapa y devuelve el texto en párrafo.</summary>
    public static string ToHtml(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
            return "<p class=\"rtf-preview-empty\">Sin contenido.</p>";

        var trimmed = rtf.Trim();
        if (!trimmed.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
            return "<p class=\"rtf-preview-plain\">" + EscapeHtml(rtf) + "</p>";

        var sb = new StringBuilder();
        var i = 0;
        var bold = false;
        var italic = false;
        var underline = false;
        var inBullet = false;

        while (i < trimmed.Length)
        {
            var c = trimmed[i];

            if (c == '\\')
            {
                i++;
                if (i >= trimmed.Length) break;

                if (trimmed[i] == '\'')
                {
                    i++;
                    if (i + 2 <= trimmed.Length &&
                        IsHex(trimmed[i]) && IsHex(trimmed[i + 1]))
                    {
                        var hex = trimmed.Substring(i, 2);
                        i += 2;
                        try
                        {
                            var code = Convert.ToInt32(hex, 16);
                            if (code == 0)
                            {
                                // No emitir carácter nulo (se ve como ◆ o cuadro en el navegador)
                            }
                            else
                            {
                                var ch = (char)code;
                                sb.Append(EscapeHtml(ch.ToString()));
                            }
                        }
                        catch
                        {
                            sb.Append('?');
                        }
                        continue;
                    }
                }

                // Formato Unicode estándar de RTF: \uN?  (N decimal, ? = carácter de fallback que se debe ignorar)
                if (trimmed[i] == 'u' && i + 2 < trimmed.Length)
                {
                    var j = i + 1;
                    var negative = false;
                    if (j < trimmed.Length && trimmed[j] == '-')
                    {
                        negative = true;
                        j++;
                    }
                    var startDigits = j;
                    while (j < trimmed.Length && char.IsDigit(trimmed[j])) j++;
                    if (j > startDigits)
                    {
                        var numStr = trimmed.Substring(startDigits, j - startDigits);
                        if (int.TryParse(numStr, out var code))
                        {
                            if (negative && code < 0)
                                code = 65536 + code;
                            if (code > 0 && code <= 0xFFFF)
                            {
                                var ch = (char)code;
                                sb.Append(EscapeHtml(ch.ToString()));
                            }
                        }
                        i = j;
                        // Consumir espacio opcional
                        if (i < trimmed.Length && trimmed[i] == ' ')
                            i++;
                        // Consumir un carácter de fallback si existe (aunque Gemini use '3'/'1'/'3', lo ignoramos)
                        if (i < trimmed.Length)
                            i++;
                        continue;
                    }
                }

                // Formato alternativo: \00f3 = 4 dígitos hex = código Unicode (p. ej. Gemini genera esto)
                if (i + 4 <= trimmed.Length &&
                    IsHex(trimmed[i]) && IsHex(trimmed[i + 1]) && IsHex(trimmed[i + 2]) && IsHex(trimmed[i + 3]))
                {
                    var hex4 = trimmed.Substring(i, 4);
                    i += 4;
                    try
                    {
                        var code = Convert.ToInt32(hex4, 16);
                        if (code == 0)
                        {
                            // No emitir carácter nulo
                        }
                        else if (code >= 0 && code <= 0xFFFF)
                        {
                            var ch = (char)code;
                            sb.Append(EscapeHtml(ch.ToString()));
                        }
                        else
                            sb.Append('?');
                    }
                    catch
                    {
                        sb.Append('?');
                    }
                    continue;
                }

                var (cmd, len) = ReadControlWord(trimmed, i);
                // Si la palabra está vacía (ej. \0 o \1) no consumimos nada y el dígito se imprimiría; lo saltamos para evitar "0 texto"
                if (len == 0 && i < trimmed.Length && char.IsDigit(trimmed[i]))
                {
                    i++;
                    continue;
                }
                i += len;

                switch (cmd.ToLowerInvariant())
                {
                    case "b":
                        if (!bold) { sb.Append("<strong>"); bold = true; }
                        break;
                    case "b0":
                        if (bold) { sb.Append("</strong>"); bold = false; }
                        break;
                    case "i":
                        if (!italic) { sb.Append("<em>"); italic = true; }
                        break;
                    case "i0":
                        if (italic) { sb.Append("</em>"); italic = false; }
                        break;
                    case "ul":
                        if (!underline) { sb.Append("<u>"); underline = true; }
                        break;
                    case "ulnone":
                        if (underline) { sb.Append("</u>"); underline = false; }
                        break;
                    case "par":
                        if (inBullet) { sb.Append("</li>"); inBullet = false; }
                        sb.Append("<br/>");
                        break;
                    case "bullet":
                        if (inBullet) sb.Append("</li>");
                        sb.Append("<li>"); inBullet = true;
                        break;
                    case "tab":
                        sb.Append("&nbsp;&nbsp;&nbsp;");
                        break;
                    default:
                        break;
                }
                continue;
            }

            if (c == '{')
            {
                var j = i + 1;
                while (j < trimmed.Length && (trimmed[j] == ' ' || trimmed[j] == '\t' || trimmed[j] == '\n' || trimmed[j] == '\r')) j++;
                var skipGroup = false;
                if (j < trimmed.Length && trimmed[j] == '\\')
                {
                    var (skipCmd, _) = ReadControlWord(trimmed, j + 1);
                    var cmdLower = skipCmd.ToLowerInvariant();
                    if (cmdLower.StartsWith("fonttbl", StringComparison.Ordinal) ||
                        cmdLower.StartsWith("colortbl", StringComparison.Ordinal) ||
                        cmdLower.StartsWith("stylesheet", StringComparison.Ordinal) ||
                        cmdLower.StartsWith("*", StringComparison.Ordinal))
                        skipGroup = true;
                }
                if (skipGroup)
                {
                    var depth = 1;
                    i++;
                    while (i < trimmed.Length && depth > 0)
                    {
                        if (trimmed[i] == '{') depth++;
                        else if (trimmed[i] == '}') depth--;
                        i++;
                    }
                }
                else
                    i++;
                continue;
            }

            if (c == '}')
            {
                i++;
                continue;
            }

            sb.Append(EscapeHtml(c.ToString()));
            i++;
        }

        if (bold) sb.Append("</strong>");
        if (italic) sb.Append("</em>");
        if (underline) sb.Append("</u>");
        if (inBullet) sb.Append("</li>");

        var html = sb.ToString();
        // Quitar basura al principio del contenido (ej. �0, caracteres de control antes del primer texto o tag)
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"^\s*[^A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9<]*0?\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));
        // Limpieza agresiva del primer título en negrita: quitar caracteres raros (ej. �0) justo después de <strong>
        // Solo se aplica al PRIMER <strong> para no tocar el resto del contenido.
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"<strong>[^A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9]*0?\s*",
            "<strong>",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));
        // El modelo Gemini a veces deja restos "313313313" tras caracteres acentuados (\u... 313),
        // eliminamos esas secuencias numéricas cuando van inmediatamente después de una letra.
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"(?<=[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])313+",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));
        // Agrupar <li>...</li> consecutivos dentro de <ul>
        var firstLi = html.IndexOf("<li>", StringComparison.Ordinal);
        var lastLi = html.LastIndexOf("</li>", StringComparison.Ordinal);
        if (firstLi >= 0 && lastLi > firstLi)
        {
            html = html.Substring(0, firstLi) + "<ul>" + html.Substring(firstLi, lastLi - firstLi + 5) + "</ul>" + html.Substring(lastLi + 5);
        }
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<br/>\s*<br/>", "</p><p>");
        html = "<div class=\"rtf-preview\">" + html + "</div>";
        return html;
    }

    private static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    private static (string word, int length) ReadControlWord(string s, int start)
    {
        var i = start;
        while (i < s.Length && (char.IsLetter(s[i]) || (i > start && (char.IsDigit(s[i]) || s[i] == '-'))))
            i++;
        var word = s.Substring(start, i - start);
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        return (word, i - start);
    }

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
