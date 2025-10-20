using System.Xml;
using Microsoft.CodeAnalysis;

namespace EmmyLua.Unity.Generator;

/// <summary>
/// Parser for XML documentation comments from C# symbols
/// </summary>
public static class XmlDocumentationParser
{
    /// <summary>
    /// Extract the summary comment from a symbol's XML documentation
    /// </summary>
    public static string GetSummary(ISymbol symbol)
    {
        var comment = GetDocumentationXml(symbol);
        if (string.IsNullOrEmpty(comment))
            return string.Empty;

        try
        {
            using var xmlDoc = XmlReader.Create(new StringReader(comment));
            while (xmlDoc.Read())
            {
                if (!xmlDoc.IsStartElement())
                    continue;

                if (xmlDoc.Name is "summary" or "para")
                {
                    xmlDoc.Read();
                    if (xmlDoc.NodeType == XmlNodeType.Text) return xmlDoc.Value.Trim();
                }
            }
        }
        catch (XmlException)
        {
            // Invalid XML, return empty string
        }

        return string.Empty;
    }

    /// <summary>
    /// Extract all documentation elements (summary, params, returns) from a symbol
    /// </summary>
    public static Dictionary<string, string> GetAllDocumentation(ISymbol symbol)
    {
        var result = new Dictionary<string, string>();
        var comment = GetDocumentationXml(symbol);

        if (string.IsNullOrEmpty(comment))
            return result;

        try
        {
            using var xmlDoc = XmlReader.Create(new StringReader(comment));
            while (xmlDoc.Read())
            {
                if (!xmlDoc.IsStartElement())
                    continue;

                switch (xmlDoc.Name)
                {
                    case "summary" or "para":
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text) result["<summary>"] = xmlDoc.Value.Trim();
                        break;

                    case "param":
                        var paramName = xmlDoc.GetAttribute("name");
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text && !string.IsNullOrEmpty(paramName))
                            result[paramName] = xmlDoc.Value.Trim();
                        break;

                    case "returns":
                        xmlDoc.Read();
                        if (xmlDoc.NodeType == XmlNodeType.Text) result["<returns>"] = xmlDoc.Value.Trim();
                        break;
                }
            }
        }
        catch (XmlException)
        {
            // Invalid XML, return what we have so far
        }

        return result;
    }

    /// <summary>
    /// Get and normalize the XML documentation string from a symbol
    /// </summary>
    private static string GetDocumentationXml(ISymbol symbol)
    {
        var comment = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(comment))
            return string.Empty;

        comment = comment.Replace('\r', ' ').Trim();

        // Validate that it's valid XML
        if (!comment.StartsWith("<member") && !comment.StartsWith("<summary"))
            return string.Empty;

        // Wrap summary tags in a parent element for proper XML parsing
        if (comment.StartsWith("<summary")) comment = $"<parent>{comment}</parent>";

        return comment;
    }
}