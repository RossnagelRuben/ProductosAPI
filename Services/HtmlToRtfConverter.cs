using System.Text;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Convierte HTML (subconjunto generado por RtfToHtmlConverter) de vuelta a RTF
/// para sincronizar la vista previa editable con el texto RTF guardado.
/// </summary>
public static class HtmlToRtfConverter
{
    private const string RtfHeader = "{\\rtf1\\ansi\\deff0 {\\fonttbl{\\f0 Arial;}} \\pard\\sa200\\sl276\\slmult1\\f0\\fs24 ";

    /// <summary>Convierte HTML a RTF (negritas, cursivas, subrayado, párrafos, viñetas). Caracteres especiales y no-ASCII se escapan correctamente.</summary>
    public static string ToRtf(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        var s = UnescapeHtml(html);
        // Quitar div.rtf-preview y tags que no usamos
        s = System.Text.RegularExpressions.Regex.Replace(s, @"</?div[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"<p[^>]*class=""[^""]*""[^>]*>", "<p>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var sb = new StringBuilder();
        sb.Append(RtfHeader);

        var i = 0;
        while (i < s.Length)
        {
            if (s[i] == '<')
            {
                if (TryConsumeTag(s, i, "strong", false, out var end) || TryConsumeTag(s, i, "b", false, out end))
                {
                    sb.Append("\\b ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "/strong", true, out end) || TryConsumeTag(s, i, "/b", true, out end))
                {
                    sb.Append("\\b0 ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "em", false, out end) || TryConsumeTag(s, i, "i", false, out end))
                {
                    sb.Append("\\i ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "/em", true, out end) || TryConsumeTag(s, i, "/i", true, out end))
                {
                    sb.Append("\\i0 ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "u", false, out end))
                {
                    sb.Append("\\ul ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "/u", true, out end))
                {
                    sb.Append("\\ulnone ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "br", true, out end) || TryConsumeTag(s, i, "br/", true, out end))
                {
                    sb.Append("\\par ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "p", false, out end) || TryConsumeTag(s, i, "/p", true, out end))
                {
                    sb.Append("\\par ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "li", false, out end))
                {
                    sb.Append("\\bullet ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "/li", true, out end))
                {
                    sb.Append("\\par ");
                    i = end;
                    continue;
                }
                if (TryConsumeTag(s, i, "ul", false, out end) || TryConsumeTag(s, i, "/ul", true, out end))
                {
                    i = end;
                    continue;
                }
                // Tag desconocido: saltar hasta >
                var gt = s.IndexOf('>', i);
                if (gt >= 0) { i = gt + 1; continue; }
            }

            AppendCharToRtf(sb, s[i]);
            i++;
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static bool TryConsumeTag(string s, int start, string tagName, bool selfClosing, out int end)
    {
        end = start;
        var open = "<" + tagName;
        if (start + open.Length > s.Length) return false;
        for (int j = 0; j < open.Length; j++)
            if (char.ToLowerInvariant(s[start + j]) != char.ToLowerInvariant(open[j]))
                return false;
        var i = start + open.Length;
        while (i < s.Length && s[i] != '>')
            i++;
        if (i >= s.Length) return false;
        end = i + 1;
        return true;
    }

    private static void AppendCharToRtf(StringBuilder sb, char c)
    {
        if (c == '\\') { sb.Append("\\\\"); return; }
        if (c == '{') { sb.Append("\\{"); return; }
        if (c == '}') { sb.Append("\\}"); return; }
        if (c >= 0x20 && c <= 0x7E)
        {
            sb.Append(c);
            return;
        }
        if (c == '\n' || c == '\r') { sb.Append("\\par "); return; }
        var code = (int)c;
        // Latin-1 (0x80-0xFF): usar \'XX para que acentos y ñ se guarden y muestren bien en sistemas que usan ANSI/Latin-1
        if (code >= 0x80 && code <= 0xFF)
        {
            sb.Append("\\'").Append(code.ToString("x2"));
            return;
        }
        if (code > 0 && code <= 0xFFFF)
            sb.Append("\\u").Append(code).Append('?');
        else
            sb.Append('?');
    }

    private static string UnescapeHtml(string html)
    {
        return html
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal);
    }
}
