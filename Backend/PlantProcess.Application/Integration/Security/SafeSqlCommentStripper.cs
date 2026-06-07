namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// PPIQ_REALIZATION_T004_LITERAL_AWARE_SQL_COMMENT_STRIPPER
/// SQL comment stripper that is aware of:
/// - single-quoted string literals
/// - escaped single quotes
/// - double-quoted identifiers
/// - square-bracket identifiers
/// - nested block comments
/// - line comments
///
/// This prevents dangerous false parsing where /* */ or -- inside a string
/// are treated as real SQL comments.
/// </summary>
public static class SafeSqlCommentStripper
{
    public static string Strip(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return string.Empty;

        var output = new System.Text.StringBuilder(sql.Length);
        var i = 0;
        var blockDepth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketIdentifier = false;

        while (i < sql.Length)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';

            if (blockDepth > 0)
            {
                if (current == '/' && next == '*')
                {
                    blockDepth++;
                    i += 2;
                    continue;
                }

                if (current == '*' && next == '/')
                {
                    blockDepth--;
                    i += 2;
                    continue;
                }

                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBracketIdentifier)
            {
                if (current == '-' && next == '-')
                {
                    while (i < sql.Length && sql[i] != '\r' && sql[i] != '\n')
                        i++;

                    output.Append(' ');
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    blockDepth = 1;
                    i += 2;
                    output.Append(' ');
                    continue;
                }
            }

            if (!inDoubleQuote && !inBracketIdentifier && current == '\'')
            {
                output.Append(current);

                if (inSingleQuote && next == '\'')
                {
                    output.Append(next);
                    i += 2;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
                i++;
                continue;
            }

            if (!inSingleQuote && !inBracketIdentifier && current == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                output.Append(current);
                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '[')
            {
                inBracketIdentifier = true;
                output.Append(current);
                i++;
                continue;
            }

            if (inBracketIdentifier && current == ']')
            {
                inBracketIdentifier = false;
                output.Append(current);
                i++;
                continue;
            }

            output.Append(current);
            i++;
        }

        return output.ToString();
    }
}
