using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace PlantProcess.Analytics.Core.Kpi;
public sealed class KpiFormulaException : Exception
{
public KpiFormulaException(string message) : base(message) { }
}
/// <summary>Safe recursive-descent arithmetic evaluator (+ - * / ^, parentheses, unary minus, named variables).</summary>
public sealed class ExpressionEvaluator
{
private enum Tok { Number, Ident, Plus, Minus, Star, Slash, Caret, LParen, RParen }
private readonly record struct Token(Tok Type, double Number, string Text);
private List<Token> _tokens = new();
private int _pos;
private IReadOnlyDictionary<string, double> _vars = new Dictionary<string, double>();

public double Evaluate(string? formula, IReadOnlyDictionary<string, double> variables)
{
    if (string.IsNullOrWhiteSpace(formula)) throw new KpiFormulaException("KPI formula is empty.");
    _vars = variables;
    _tokens = Tokenize(formula);
    _pos = 0;
    double result = ParseExpression();
    if (_pos != _tokens.Count) throw new KpiFormulaException("Unexpected trailing tokens in KPI formula.");
    if (double.IsNaN(result) || double.IsInfinity(result)) throw new KpiFormulaException("KPI formula produced a non-finite value.");
    return result;
}

private static List<Token> Tokenize(string s)
{
    var list = new List<Token>();
    int i = 0;
    while (i < s.Length)
    {
        char c = s[i];
        if (char.IsWhiteSpace(c)) { i++; continue; }
        if (char.IsDigit(c) || c == '.')
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            var text = s.Substring(start, i - start);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                throw new KpiFormulaException($"Invalid number '{text}' in KPI formula.");
            list.Add(new Token(Tok.Number, num, text));
            continue;
        }
        if (char.IsLetter(c) || c == '_')
        {
            int start = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            list.Add(new Token(Tok.Ident, 0, s.Substring(start, i - start)));
            continue;
        }
        Tok t = c switch
        {
            '+' => Tok.Plus, '-' => Tok.Minus, '*' => Tok.Star, '/' => Tok.Slash,
            '^' => Tok.Caret, '(' => Tok.LParen, ')' => Tok.RParen,
            _ => throw new KpiFormulaException($"Unexpected character '{c}' in KPI formula.")
        };
        list.Add(new Token(t, 0, c.ToString()));
        i++;
    }
    if (list.Count == 0) throw new KpiFormulaException("KPI formula has no tokens.");
    return list;
}

private Token Peek() => _pos < _tokens.Count ? _tokens[_pos] : new Token(Tok.RParen, 0, "<eof>");
private bool Match(Tok t) { if (_pos < _tokens.Count && _tokens[_pos].Type == t) { _pos++; return true; } return false; }

private double ParseExpression()
{
    double left = ParseTerm();
    while (true)
    {
        if (Match(Tok.Plus)) left += ParseTerm();
        else if (Match(Tok.Minus)) left -= ParseTerm();
        else break;
    }
    return left;
}

private double ParseTerm()
{
    double left = ParseFactor();
    while (true)
    {
        if (Match(Tok.Star)) left *= ParseFactor();
        else if (Match(Tok.Slash))
        {
            double d = ParseFactor();
            if (d == 0) throw new KpiFormulaException("Division by zero in KPI formula.");
            left /= d;
        }
        else break;
    }
    return left;
}

private double ParseFactor()
{
    double b = ParseUnary();
    if (Match(Tok.Caret)) { double e = ParseFactor(); return Math.Pow(b, e); }
    return b;
}

private double ParseUnary()
{
    if (Match(Tok.Minus)) return -ParseUnary();
    return ParsePrimary();
}

private double ParsePrimary()
{
    var tk = Peek();
    if (tk.Type == Tok.Number) { _pos++; return tk.Number; }
    if (tk.Type == Tok.Ident)
    {
        _pos++;
        if (!_vars.TryGetValue(tk.Text, out var val)) throw new KpiFormulaException($"Unknown KPI variable '{tk.Text}'.");
        return val;
    }
    if (Match(Tok.LParen))
    {
        double e = ParseExpression();
        if (!Match(Tok.RParen)) throw new KpiFormulaException("Missing closing parenthesis in KPI formula.");
        return e;
    }
    throw new KpiFormulaException("Unexpected token in KPI formula.");
}
}