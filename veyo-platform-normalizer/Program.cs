using System.Xml;
using System.Linq;
using System.Xml.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var workingDir = args?.FirstOrDefault() ?? Directory.GetCurrentDirectory();
        var foundProjs =
            Directory.GetFiles(workingDir, "*.csproj", SearchOption.AllDirectories);  /** test todo:
                                                                                        * csproj in subdir: ?
                                                                                        * csproj in top level dir: ?
                                                                                        * csproj in both: ?
                                                                                        * multiple csprojs ostensibly owning that tree: ?
                                                                                        *    so like, projs/1.csproj, projs/2.csproj, projs/helpers/3.csproj; we can do stuff but they can't all own the subdirectories
                                                                                        * no csprojs found: ?
                                                                                        * args[0] present but invalid: ?
                                                                                        **/
        foreach (var foundProjsFile in foundProjs)
        {
            var partialPath = Path.GetRelativePath(workingDir, foundProjsFile);
            var projDir = Path.GetDirectoryName(foundProjsFile);
            var problemFound = false;
            XElement doc;
            try
            {
                doc = XElement.Load(foundProjsFile);
            }
            catch (System.Xml.XmlException e)
            {
                Console.Error.WriteLine("error loading " + foundProjsFile);
                Console.Error.WriteLine(e.Message);
                continue;
            }
            var referencedFiles = doc.Descendants().Where(xe => xe.Name.LocalName == "Compile");
            if (referencedFiles.Count() > referencedFiles.Select(xe => xe.Attribute("Include")?.Value).Distinct().Count())
            {

                Console.WriteLine($"!! {partialPath} !!");
                problemFound = true;
                Console.WriteLine("duplicates found!");

                foreach (var refd in referencedFiles.ToList())
                {
                    if(referencedFiles.Count(xe => xe.Attribute("Include")?.Value == refd.Attribute("Include")?.Value) > 1)
                    {
                        Console.WriteLine($"removing {refd.Attribute("Include")?.Value}");
                        refd.Remove();
                    }
                }
            }

            foreach (var refd in referencedFiles.ToList())
            {
                var file = refd.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(file) && !File.Exists(file))
                {
                    refd.Remove();
                    if (!problemFound)
                    {
                        Console.WriteLine($"!! {partialPath} !!");
                        problemFound = true;
                    }

                    Console.WriteLine($"{file} missing, reference removed");
                }
            }

            //referencing existing shadow files
            var firstGroup = doc.Descendants().Where(xe =>
                xe.Name.LocalName == "ItemGroup"
                && xe.Attributes().Count() == 0
                && xe.Descendants().Where(igd =>
                    igd.Name.LocalName == "Compile"
                    && igd.Attribute("Include")?.Value?.EndsWith(".cs") == true
                    ).Any()
            ).FirstOrDefault();
            firstGroup = null;
            if (firstGroup == null)
            {
                firstGroup = new XElement("ItemGroup");
                doc.Add(firstGroup);
                //because it _does_ have one; that's why system.xml wants to badly to set one - you set it up with blank, so it's _overriding_ with blank
                firstGroup.Name = firstGroup.Parent.Name.Namespace + firstGroup.Name.LocalName;
            }
            foreach (var existingFile in Directory.GetFiles(projDir, "*.cs", SearchOption.AllDirectories))
            {
                var relativeFilePath = Path.GetRelativePath(projDir, existingFile);
                if (findUpward(existingFile, "obj", projDir))
                {
                    continue;
                }
                var foundVersion = referencedFiles.FirstOrDefault(xe =>
                    Path.GetFullPath(xe.Attribute("Include")?.Value ?? "/dev/null", projDir)
                    == existingFile);
                if (foundVersion == null)
                {
                    if (!problemFound)
                    {
                        Console.WriteLine($"!! {partialPath} !!");
                        problemFound = true;
                    }
                    Console.WriteLine($"does not reference file in directory: {relativeFilePath}");
                    var toAdd = new XElement("Compile");
                    toAdd.SetAttributeValue("Include", relativeFilePath);
                    firstGroup.Add(toAdd);
                }
            }

            //merge to one big itemgroup
            var plainItemgroups = doc.Descendants().Where(xe =>
                xe.Name.LocalName == "ItemGroup"
                && xe.Attributes().Count() == 0
                && xe.Descendants().Where(igd =>
                    igd.Name.LocalName == "Compile"
                    && igd.Attribute("Include")?.Value?.EndsWith(".cs") == true
                    ).Any()
            );
            if(plainItemgroups.Count() > 1)
            {
                if (!problemFound)
                {
                    Console.WriteLine($"!! {partialPath} !!");
                    problemFound = true;
                }
                Console.WriteLine($"many itemgroups found, merging to one");
                
                firstGroup = plainItemgroups.First();
                foreach (var otherGroup in plainItemgroups.Where(xe => xe != firstGroup).ToList())
                {
                    var immediateChildren = otherGroup.Descendants().Where(xe => xe.Parent == otherGroup);
                    otherGroup.Remove();
                    firstGroup.Add(immediateChildren);
                }
            }

            //still searchin throuh multiple itemgroups, in case they have attributes
            foreach (var group in doc.Descendants().Where(xe => xe.Name.LocalName == "ItemGroup"))
            {
                var compileItems = group.Descendants().Where(xe => xe.Name.LocalName == "Compile");
                if(!compileItems.Select(xe => xe.Attribute("Include")?.Value).SequenceEqual(compileItems.OrderBy(ci => ci.Attribute("Include")?.Value).Select(xe => xe.Attribute("Include")?.Value)))
                {
                    if (!problemFound)
                    {
                        Console.WriteLine($"!! {partialPath} !!");
                        problemFound = true;
                    }

                    Console.WriteLine($"sorting an itemgroup");
                    var compileItmesCopy = compileItems.OrderBy(ci => ci.Attribute("Include")?.Value).ToList();
                    var noncompileItems = group.Descendants().Where(xe => xe.Name.LocalName != "Compile" && xe.Parent == group).ToList();
                    group.RemoveAll();
                    group.Add(noncompileItems);
                    group.Add(compileItmesCopy);
                }
            }

            if (!problemFound)
            {
                Console.WriteLine($"{partialPath} fine");
            }
            else
            {
                doc.Save(foundProjsFile);
            }
        }
    }

    private static bool findUpward(string fullpath, string target, string stopAt = "C:\\")
    {
        var parent = Directory.GetParent(fullpath);
        if (parent == null || parent.FullName == stopAt)
        {
            return false;
        }
        else if (parent.Name == target) {
            return true;
        }
        else
        {
            return findUpward(parent.FullName, target, stopAt);
        }
    }
}