using System.Text;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Convierte RTF (Rich Text Format) a HTML para mostrar vista previa en el modal de Observaciones.
/// Soporta negritas, cursivas, subrayado, párrafos, viñetas y caracteres escapados \'XX.
/// Nota: en Blazor WebAssembly solo está disponible UTF‑8; los bytes \'XX se interpretan como Latin‑1.
/// </summary>
public static class RtfToHtmlConverter
{

    /// <summary>Convierte una cadena RTF a HTML seguro (solo tags permitidos). Si no parece RTF, escapa y devuelve el texto en párrafo. Nunca lanza excepciones.</summary>
    public static string ToHtml(string? rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
            return "<p class=\"rtf-preview-empty\">Sin contenido.</p>";

        rtf = SanitizeRtfInput(rtf);
        var trimmed = rtf.Trim();
        if (!trimmed.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
            return "<p class=\"rtf-preview-plain\">" + EscapeHtml(rtf) + "</p>";

        try
        {
            return ToHtmlCore(trimmed);
        }
        catch
        {
            return "<p class=\"rtf-preview-plain\">" + EscapeHtml(rtf) + "</p>";
        }
    }

    private static string SanitizeRtfInput(string rtf)
    {
        if (string.IsNullOrEmpty(rtf)) return rtf;
        var sb = new StringBuilder(rtf.Length);
        foreach (var c in rtf)
        {
            if (c == '\0') continue;
            if (char.IsSurrogate(c)) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ToHtmlCore(string trimmed)
    {

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

                // Formato Unicode RTF: \uN? (N decimal) o \uXXXX (hex, p. ej. \u00f3 = ó). Gemini a veces usa hex.
                if (trimmed[i] == 'u' && i + 2 < trimmed.Length)
                {
                    var j = i + 1;
                    // Intentar primero 4 dígitos hex (\u00f3 para ó, \u00f1 para ñ) para no interpretar \u00 como decimal 0 y perder F3
                    if (j + 4 <= trimmed.Length &&
                        IsHex(trimmed[j]) && IsHex(trimmed[j + 1]) && IsHex(trimmed[j + 2]) && IsHex(trimmed[j + 3]))
                    {
                        var hex4 = trimmed.Substring(j, 4);
                        if (int.TryParse(hex4, System.Globalization.NumberStyles.HexNumber, null, out var codeHex) &&
                            codeHex > 0 && codeHex <= 0xFFFF)
                        {
                            var ch = (char)codeHex;
                            sb.Append(EscapeHtml(ch.ToString()));
                            i = j + 4;
                            if (i < trimmed.Length && (trimmed[i] == ' ' || trimmed[i] == '\t')) i++;
                            if (i < trimmed.Length) i++; // carácter de fallback
                            continue;
                        }
                    }
                    // Decimal con signo opcional: \u243? o \u-123?
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
                        if (i < trimmed.Length && trimmed[i] == ' ') i++;
                        if (i < trimmed.Length) i++;
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

            // Saltos de línea literales en el RTF → <br/> para que la vista previa muestre los párrafos correctamente
            if (c == '\r')
            {
                i++;
                if (i < trimmed.Length && trimmed[i] == '\n') i++;
                if (!EndsWithBr(sb)) sb.Append("<br/>");
                continue;
            }
            if (c == '\n')
            {
                i++;
                // No duplicar <br/> si acabamos de emitir uno (ej. por \par\n)
                if (!EndsWithBr(sb)) sb.Append("<br/>");
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
        // Quitar todos los dígitos (0-9) del contenido de observaciones. El modelo a veces genera "este1", "3 opciones", etc.
        // y el usuario NO quiere ver números en las observaciones generadas ni en la vista previa.
        html = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"\d+",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));
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

    private static bool EndsWithBr(StringBuilder sb)
    {
        const string br = "<br/>";
        if (sb.Length < br.Length) return false;
        for (int k = 0; k < br.Length; k++)
            if (sb[sb.Length - br.Length + k] != br[k]) return false;
        return true;
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
