using System.Runtime.InteropServices;

namespace PKHeX.TemplateRegen;

public class MGDBPickler(string PKHeXLegality, string EventGalleryRepoPath)
{
    private const string LegalityOverrideCards = "PKHeX Legality";

    private static readonly Dictionary<string, string> BadCardSwap = new()
    {
        {"1053 XYORAS - 데세르시티 Arceus (KOR).wc6",
         "1053 XYORAS - 데세르시티 Arceus (KOR) - Form Fix.wc6"},
        {"0146 SWSH - サトシ Dracovish.wc8",
         "0146 SWSH - サトシ Dracovish - Gender Fix.wc8"},
    };

    public void Update()
    {
        var repoPath = EventGalleryRepoPath;
        if (!RepoUpdater.UpdateRepo("EventsGallery", repoPath, "master"))
            return;

        var released = Path.Combine(repoPath, "Released");
        string _9a = Path.Combine(released, "Gen 9");
        string _9 = Path.Combine(released, "Gen 9");
        string _8a = Path.Combine(released, "Gen 8");
        string _8b = Path.Combine(released, "Gen 8");
        string _8 = Path.Combine(released, "Gen 8");
        string _7b = Path.Combine(released, "Gen 7", "Switch", "Wondercards");
        string _7 = Path.Combine(released, "Gen 7", "3DS", "Wondercards");
        string _6 = Path.Combine(released, "Gen 6");
        string _5 = Path.Combine(released, "Gen 5");
        string _4 = Path.Combine(released, "Gen 4", "Wondercards");

        Bin(_4, "wc4");
        BinPGF(_5, "pgf");
        Bin(_6, "wc6", "wc6full");
        Bin(_7, "wc7", "wc7full");
        Bin(_7b, "wb7full");
        Bin(_8, "wc8");
        Bin(_8b, "wb8");
        Bin(_8a, "wa8");
        Bin(_9, "wc9");
        Bin(_9a, "wa9");
    }

    // Specialized pickler to extract receivability constraints from the gift filename
    private void BinPGF(string path, string ext)
    {
        if (!Directory.Exists(path))
        {
            LogUtil.Log($"input path not found ({ext})");
            return;
        }

        var dest = Path.Combine(PKHeXLegality, "mgdb");
        var outFile = Path.Combine(dest, $"{ext}.pkl");

        // Write the .pkl: all gifts, and all gifts-receivability
        using var stream = new FileStream(outFile, FileMode.Create);
        List<byte> receivability = [];

        var files = Directory.EnumerateFiles(path, $"*.{ext}", SearchOption.AllDirectories);
        int ctr = 0;
        foreach (var f in files)
        {
            var file = f;
            if (!f.EndsWith(ext)) // Double check
                continue;

            using var bytes = File.OpenRead(file);
            bytes.CopyTo(stream);
            ctr++;

            var fileName = Path.GetFileNameWithoutExtension(f);
            receivability.Add(GetReceivability5(fileName));
        }

        // Write the receivability constraints to the end of the file
        stream.Write(CollectionsMarshal.AsSpan(receivability));
        LogUtil.Log($"{ext}: {ctr}");
    }

    private static byte GetReceivability5(string fileName)
    {
        // 0035 W - 잔타 Golurk (KOR).pgf
        // Second word is receivability: BWB2W2 mapped to bitflags
        // Last (*) is language.

        // Get Receivability: second word in the filename
        var parts = fileName.Split(' ');
        if (parts.Length < 2)
            throw new ArgumentException($"Invalid filename format: {fileName}");

        var resultVersion = GetVersionFromTag(parts[1]);
        var language = parts[^1];
        if (language.Length != 5) // bad tag
            language = parts[^2];
        var resultLanguage = GetLanguageFromTag(language);

        // Merge them together. Version first 4 bits, then language top 4 bits.
        return (byte)((resultLanguage << 4) | resultVersion);
    }

    private static byte GetVersionFromTag(ReadOnlySpan<char> tag)
    {
        byte result = 0;
        // peel off bitflags from the tag.

        // W=0, B=1, W2=2, B2=3
        if (tag.EndsWith("W2"))
        {
            result |= 1 << 2; // W2 flag
            tag = tag[..^2];
        }
        if (tag.EndsWith("B2"))
        {
            result |= 1 << 3; // B2 flag
            tag = tag[..^2];
        }
        if (tag.EndsWith("W"))
        {
            result |= 1 << 0; // W flag
            tag = tag[..^1];
        }
        if (tag.EndsWith("B"))
        {
            result |= 1 << 1; // B flag
            tag = tag[..^1];
        }
        return result;
    }

    private static byte GetLanguageFromTag(ReadOnlySpan<char> tag) => tag switch
    {
        "(JPN)" => 1, // Japanese (日本語)
        "(ENG)" => 2, // English (US/UK/AU)
        "(FRE)" => 3, // French (Français)
        "(ITA)" => 4, // Italian (Italiano)
        "(GER)" => 5, // German (Deutsch)
        "(SPA)" => 7, // Spanish (Español)
        "(KOR)" => 8, // Korean (한국어)
        _ => throw new ArgumentException($"Unknown language tag: {tag}"),
    };

    private void Bin(string path, params ReadOnlySpan<string> type)
    {
        var dest = Path.Combine(PKHeXLegality, "mgdb");
        foreach (var z in type)
            BinWrite(dest, path, z);
    }

    private void BinWrite(string outDir, string path, string ext)
    {
        if (!Directory.Exists(path))
            LogUtil.Log($"input path not found ({ext})");
        else
            BinFiles(path, ext, Path.Combine(outDir, $"{ext}.pkl"));
    }

    private void BinFiles(string directory, string ext, string outfile)
    {
        // create/clear file
        File.WriteAllBytes(outfile, []);
        using var stream = new FileStream(outfile, FileMode.Append);

        var files = Directory.EnumerateFiles(directory, $"*.{ext}", SearchOption.AllDirectories);
        int ctr = 0;
        foreach (var f in files)
        {
            var file = f;
            if (!f.EndsWith(ext)) // Double check
                continue;

            var fileName = Path.GetFileName(f);
            if (BadCardSwap.TryGetValue(fileName, out var redirect))
                file = Path.Combine(EventGalleryRepoPath, LegalityOverrideCards, redirect);

            var bytes = File.ReadAllBytes(file);
            stream.Write(bytes);
            ctr++;
        }
        LogUtil.Log($"{ext}: {ctr}");
    }
}
