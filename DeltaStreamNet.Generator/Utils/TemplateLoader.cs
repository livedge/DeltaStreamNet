using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DeltaStreamNet.Utils;

internal static class TemplateLoader
{
    public static string Get(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames()
            .First(r => r.EndsWith(name, StringComparison.Ordinal));

        using var stream = asm.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}