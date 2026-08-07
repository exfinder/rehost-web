#nullable enable

using System.Collections.Generic;

namespace System.Web;

// The extensions IIS serves as static content, read from the <staticContent> section of a
// default applicationHost.config (IIS 10.0.26100, 2026-08-07, 424 mimeMap entries, 422 distinct
// extensions). IIS refused requests for extensions outside this list, so files an application
// never meant to expose (a backup beside web.config) did not download; this host replaces IIS
// and applies the same rule at DefaultHttpHandler's static fallback. Extensions ASP.NET forbids
// (.config, .cs, ...) also appear here, exactly as in IIS's own list - the forbidden handler
// mappings claim them first, on both stacks. An application serves an off-list extension by
// mapping it to StaticFileHandler in its web.config, which bypasses this gate as an explicit
// choice.
internal static class IisStaticContent
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".323", ".3g2", ".3gp", ".3gp2", ".3gpp", ".aac",
        ".aaf", ".aca", ".accdb", ".accde", ".accdt", ".acx",
        ".ad", ".adprototype", ".adt", ".adts", ".afm", ".ai",
        ".aif", ".aifc", ".aiff", ".appcache", ".application", ".appx",
        ".appxbundle", ".art", ".asax", ".ascx", ".asd", ".asf",
        ".asi", ".asm", ".asr", ".asx", ".atom", ".au",
        ".avi", ".axs", ".bas", ".bcpio", ".bin", ".bmp",
        ".browser", ".c", ".cab", ".calx", ".cat", ".cd",
        ".cdf", ".chm", ".class", ".clp", ".cmx", ".cnf",
        ".cod", ".compiled", ".config", ".cpio", ".cpp", ".crd",
        ".crl", ".crt", ".cs", ".csh", ".csproj", ".css",
        ".csv", ".cur", ".dcr", ".dd", ".deploy", ".der",
        ".dib", ".dir", ".disco", ".dll", ".dlm", ".doc",
        ".docm", ".docx", ".dot", ".dotm", ".dotx", ".dsdgm",
        ".dsp", ".dsprototype", ".dtd", ".dvi", ".dwf", ".dwp",
        ".dxr", ".eml", ".emz", ".eot", ".eps", ".esd",
        ".etx", ".evy", ".exclude", ".exe", ".fdf", ".fif",
        ".fla", ".flr", ".flv", ".gif", ".glb", ".gtar",
        ".gz", ".h", ".hdf", ".hdml", ".hhc", ".hhk",
        ".hhp", ".hlp", ".hqx", ".hta", ".htc", ".htm",
        ".html", ".htt", ".hxt", ".ico", ".ics", ".ief",
        ".iii", ".inf", ".ins", ".isp", ".ivf", ".jar",
        ".java", ".jck", ".jcz", ".jfif", ".jpb", ".jpe",
        ".jpeg", ".jpg", ".js", ".jsl", ".json", ".jsonld",
        ".jsx", ".latex", ".ldb", ".ldd", ".lddprototype", ".ldf",
        ".less", ".licx", ".lit", ".lpk", ".lsad", ".lsaprototype",
        ".lsf", ".lsx", ".lzh", ".m13", ".m14", ".m1v",
        ".m2ts", ".m3u", ".m4a", ".m4v", ".man", ".manifest",
        ".map", ".master", ".mdb", ".mdf", ".mdp", ".me",
        ".mht", ".mhtml", ".mid", ".midi", ".mix", ".mmf",
        ".mno", ".mny", ".mov", ".movie", ".mp2", ".mp3",
        ".mp4", ".mp4v", ".mpa", ".mpe", ".mpeg", ".mpg",
        ".mpp", ".mpv2", ".ms", ".msgx", ".msi", ".msix",
        ".msixbundle", ".mso", ".msu", ".mvb", ".mvc", ".nc",
        ".nsc", ".nws", ".ocx", ".oda", ".odc", ".ods",
        ".oga", ".ogg", ".ogv", ".one", ".onea", ".onepkg",
        ".onetmp", ".onetoc", ".onetoc2", ".osdx", ".otf", ".p10",
        ".p12", ".p7b", ".p7c", ".p7m", ".p7r", ".p7s",
        ".pbm", ".pcx", ".pcz", ".pdf", ".pfb", ".pfm",
        ".pfx", ".pgm", ".pko", ".pma", ".pmc", ".pml",
        ".pmr", ".pmw", ".png", ".pnm", ".pnz", ".pot",
        ".potm", ".potx", ".ppam", ".ppm", ".pps", ".ppsm",
        ".ppsx", ".ppt", ".pptm", ".pptx", ".prf", ".prm",
        ".prx", ".ps", ".psd", ".psm", ".psp", ".pub",
        ".qt", ".qtl", ".qxd", ".ra", ".ram", ".rar",
        ".ras", ".refresh", ".resources", ".resx", ".rf", ".rgb",
        ".rm", ".rmi", ".roff", ".rpm", ".rtf", ".rtx",
        ".rules", ".scd", ".sct", ".sd", ".sdm", ".sdmdocument",
        ".sea", ".setpay", ".setreg", ".sgml", ".sh", ".shar",
        ".sit", ".sitemap", ".skin", ".sldm", ".sldx", ".smd",
        ".smi", ".smx", ".smz", ".snd", ".snp", ".spc",
        ".spl", ".spx", ".src", ".ssdgm", ".ssm", ".ssmap",
        ".sst", ".stl", ".sv4cpio", ".sv4crc", ".svg", ".svgz",
        ".swf", ".t", ".tar", ".tcl", ".tex", ".texi",
        ".texinfo", ".tgz", ".thmx", ".thn", ".tif", ".tiff",
        ".toc", ".tr", ".trm", ".ts", ".tsv", ".ttf",
        ".tts", ".txt", ".u32", ".uls", ".ustar", ".vb",
        ".vbproj", ".vbs", ".vcf", ".vcs", ".vdx", ".vjsproj",
        ".vml", ".vsd", ".vsdisco", ".vss", ".vst", ".vsto",
        ".vsw", ".vsx", ".vtx", ".wasm", ".wav", ".wax",
        ".wbmp", ".wcm", ".wdb", ".webinfo", ".webm", ".wim",
        ".wks", ".wm", ".wma", ".wmd", ".wmf", ".wml",
        ".wmlc", ".wmls", ".wmlsc", ".wmp", ".wmv", ".wmx",
        ".wmz", ".woff", ".woff2", ".wps", ".wri", ".wrl",
        ".wrz", ".wsdl", ".wtv", ".wvx", ".x", ".xaf",
        ".xaml", ".xap", ".xbap", ".xbm", ".xdr", ".xht",
        ".xhtml", ".xla", ".xlam", ".xlc", ".xlm", ".xls",
        ".xlsb", ".xlsm", ".xlsx", ".xlt", ".xltm", ".xltx",
        ".xlw", ".xml", ".xof", ".xpm", ".xps", ".xsd",
        ".xsf", ".xsl", ".xslt", ".xsn", ".xtp", ".xwd",
        ".z", ".zip",
    };

    internal static bool Serves(string? extension) =>
        !string.IsNullOrEmpty(extension) && Extensions.Contains(extension);
}
