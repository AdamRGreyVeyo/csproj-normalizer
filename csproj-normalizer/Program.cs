using System;
using System.Text;
using System.Text.RegularExpressions;

public static class Program
{
    public enum Errors
    {
        csprojNotFound, unrecognizedSwitch, reverseTrimSeemsEmpty
    }
    public static void Main(string[] args)
    {
        //might even work on *nix?
        string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/source/repos/veyo-platform/Infrastructure/Infrastructure.csproj";
        var targetUnix = true;
        if (args?.Length > 0)
        {
            var pathSpecified = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-") && !pathSpecified)
                {
                    pathSpecified = true;
                    path = args[i];
                }
                else if (args[i].ToLower() == "-path" && !pathSpecified)
                {
                    i++;
                    path = args[i];
                    pathSpecified = true;
                }
                else if (args[i].ToLower() == "-dos")
                {
                    targetUnix = false;
                }
                else
                {
                    Console.Error.WriteLine("couldn't understand switch " + args[i]);
                    Environment.Exit((int)Errors.unrecognizedSwitch);
                }
            }
        }

        Console.WriteLine("using " + path);
        if (File.Exists(path))
        {
            Console.WriteLine("found csproj.");
        }
        else
        {
            Console.Error.WriteLine("couldn't find infrastructure.csproj there.");
            Environment.Exit((int)Errors.csprojNotFound);
        }

        var csProjTextBlock = File.ReadAllText(path);
        List<string> csProjLines;

        if (targetUnix)
        {
            csProjTextBlock = unixLineEndingsIfy(csProjTextBlock);
            csProjLines = csProjTextBlock.Split('\n').ToList();
        }
        else
        {
            csProjTextBlock = dosLineEndingsIfy(csProjTextBlock);
            csProjLines = csProjTextBlock.Split("\r\n").ToList();
        }

        csProjLines = DeepTrim(csProjLines);

        csProjLines = DeDuplicate(csProjLines);

        csProjLines = ReverseTrim(csProjLines);

        //TODO: csProjLines = XMLValidate(csProjLines);

        File.WriteAllLines(path, csProjLines);
    }

    private static List<string> ReverseTrim(List<string> csProjLines)
    {
        for (int i = csProjLines.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(csProjLines[i]))
            {
                return csProjLines.Take(i + 1).ToList();
            }
        }
        Console.Error.WriteLine("somehow, suddenly csProj lines look like theyr'e all null.");
        Environment.Exit((int)Errors.reverseTrimSeemsEmpty);
        return csProjLines;
    }

    private static List<string> DeDuplicate(List<string> csProjLines)
    {
        Console.WriteLine("purging duplicates...");
        var seen = new List<string>();
        for (int i = 0; i < csProjLines.Count; i++)
        {
            var line = csProjLines[i];
            if (seen.Contains(line) && line?.ToLower().Contains("<compile include=\"") == true)
            {
                Console.WriteLine("duplicate spotted: " + line);
            }
            else
            {
                seen.Add(line ?? "");
            }
        }
        return seen;
    }

    private static List<string> DeepTrim(List<string> csProjLines)
    {
        Console.WriteLine("deep trimming...");
        for (int i = 0; i < csProjLines.Count; i++)
        {
            csProjLines[i] = csProjLines[i]?.TrimEnd() ?? "";
        }
        return csProjLines;
    }

    private static string dosLineEndingsIfy(string csProjTextBlock)
    {
        Console.WriteLine("dos-ifying line endings...");
        return Regex.Replace(csProjTextBlock, "([^\\r])\\n", "$1\\n");
    }

    private static string unixLineEndingsIfy(string csProjTextBlock)
    {
        Console.WriteLine("unix-ifying line endings...");
        return csProjTextBlock.Replace("\r\n", "\n");
    }
}