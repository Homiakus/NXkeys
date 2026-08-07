using System.Xml.Linq;

namespace NxEskd.Configurator.Tests;

internal static class XDocumentExtensions
{
    public static IEnumerable<XElement> DescendantsAndSelf(this XDocument document)
        => document.Root?.DescendantsAndSelf() ?? [];
}
