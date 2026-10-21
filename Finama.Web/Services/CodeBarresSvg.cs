using System.Globalization;
using System.Text;

namespace Finama.Web.Services;

/// <summary>
/// Génère un code-barres EAN-13 en SVG (aucune dépendance). Lisible par toutes les douchettes et par la caméra.
/// Encodage vérifié contre un décodeur ZXing. Pour un code non EAN-13, utiliser un QR code (finama-codes.js).
/// </summary>
public static class CodeBarresSvg
{
    private static readonly string[] L = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] G = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];
    private static readonly string[] R = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];
    private static readonly string[] Parites = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    /// <summary>Vrai si le code est un EAN-13 valide (13 chiffres + clé de contrôle correcte).</summary>
    public static bool EstEan13(string? code)
        => code is { Length: 13 } && code.All(char.IsAsciiDigit) && Cle(code[..12]) == code[12];

    /// <summary>Clé de contrôle GS1 d'un corps de 12 chiffres.</summary>
    public static char Cle(string douzeChiffres)
    {
        var somme = 0;
        for (var i = 0; i < 12; i++)
            somme += (douzeChiffres[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (char)('0' + (10 - somme % 10) % 10);
    }

    /// <summary>SVG redimensionnable (largeur 100 %) : barres + chiffres lisibles. Null si le code n'est pas un EAN-13 valide.</summary>
    public static string? Ean13(string code)
    {
        if (!EstEan13(code)) return null;

        var premier = code[0] - '0';
        var bits = new StringBuilder("101");
        for (var i = 0; i < 6; i++)
            bits.Append((Parites[premier][i] == 'L' ? L : G)[code[i + 1] - '0']);
        bits.Append("01010");
        for (var i = 7; i < 13; i++)
            bits.Append(R[code[i] - '0']);
        bits.Append("101");

        const int marge = 11;                 // zone blanche réglementaire à gauche (≥ 11 modules)
        const int largeur = marge + 95 + 7;
        const double hauteurBarres = 50;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {largeur} 62\" preserveAspectRatio=\"xMidYMid meet\" class=\"code-barres\" shape-rendering=\"crispEdges\">");
        svg.Append($"<rect width=\"{largeur}\" height=\"62\" fill=\"#fff\"/>");

        // Barres : on fusionne les modules noirs consécutifs ; les gardes descendent un peu plus bas (aspect standard)
        var b = bits.ToString();
        for (var i = 0; i < b.Length;)
        {
            if (b[i] != '1') { i++; continue; }
            var debut = i;
            while (i < b.Length && b[i] == '1') i++;
            var garde = debut < 3 || (debut >= 45 && debut < 50) || debut >= 92;
            var h = garde ? hauteurBarres + 5 : hauteurBarres;
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{marge + debut}\" y=\"0\" width=\"{i - debut}\" height=\"{h}\" fill=\"#000\"/>");
        }

        // Chiffres sous le code (1er chiffre à gauche, puis deux groupes de 6)
        const string police = "font-family=\"Arial,Helvetica,sans-serif\" font-size=\"9\" fill=\"#000\" text-anchor=\"middle\"";
        svg.Append($"<text x=\"{marge - 5}\" y=\"60\" {police}>{code[0]}</text>");
        svg.Append($"<text x=\"{marge + 3 + 21}\" y=\"60\" {police} letter-spacing=\"1.2\">{code[1..7]}</text>");
        svg.Append($"<text x=\"{marge + 50 + 21}\" y=\"60\" {police} letter-spacing=\"1.2\">{code[7..]}</text>");
        svg.Append("</svg>");
        return svg.ToString();
    }
}
