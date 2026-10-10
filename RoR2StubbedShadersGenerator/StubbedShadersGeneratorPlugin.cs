using BepInEx;
using BepInEx.Logging;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using static RoR2.SteamworksRemoteStorageFileSystem;
using Path = System.IO.Path;

namespace RoR2StubbedShadersGenerator;

[BepInPlugin(GUID, NAME, VERSION)]
public class StubbedShadersGeneratorPlugin : BaseUnityPlugin
{
    public const string
        GUID = "groovesalad." + NAME,
        NAME = "RoR2StubbedShadersGenerator",
        VERSION = "1.0.0";

    void Awake()
    {
        const string ASSETS_PATH = "C:\\Users\\sunde\\Modding\\RoR2 Rips\\hc stubbed shaders rip 1.2.1\\RoR2 HC StubbedShaders\\Assets";
        const string OUTPUT_PATH = "C:\\Users\\sunde\\Documents\\GitHub\\RoR2StubbedShaders";

        var resourceLocator = Addressables.ResourceLocators.First(x => x.LocatorId == "AddressablesMainContentCatalog");
        HashSet<string> visitedShaderKeys = [];
        Dictionary<string, string> shaderKeysByName = [];
        foreach (var key in resourceLocator.Keys)
        {
            if (resourceLocator.Locate(key, typeof(Shader), out var locations))
            {
                foreach (var location in locations)
                {
                    if (!visitedShaderKeys.Add(location.PrimaryKey))
                    {
                        continue;
                    }
                    string internalId = location.InternalId;
                    if (internalId.StartsWith("Assets/RoR2/Base/Shaders/Optimized") || internalId.EndsWith("_switch.shader") || internalId.EndsWith("Optimized.shader") || internalId.EndsWith(" - optimized.shader"))
                    {
                        continue;
                    }
                    shaderKeysByName.Add(Path.GetFileName(internalId), location.PrimaryKey);
                    //Logger.LogInfo($"Primary Key: {location.PrimaryKey}");
                    //Logger.LogInfo($"InternalId: {location.InternalId}");
                    //Logger.LogInfo($"Shader name: {shader.name}");
                }
            }
        }

        foreach (string filePath in Directory.EnumerateFiles(ASSETS_PATH, "*.shader", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(filePath);
            if (!shaderKeysByName.TryGetValue(fileName, out var shaderKey))
            {
                //Logger.LogWarning($"Shader {filePath} has no addressables key!");
                continue;
            }
            shaderKeysByName.Remove(fileName);

            string relativeFilePath = Path.GetRelativePath(ASSETS_PATH, filePath);
            /*if (relativeFilePath.StartsWith("RoR2\\Base\\Shaders\\Optimized\\Switch") || fileName.EndsWith("_switch.shader") || fileName.EndsWith("Optimized.shader") || fileName.EndsWith(" - optimized.shader"))
            {
                continue;
            }*/

            //Console.WriteLine(relativeFilePath);

            string outputFilePath = Path.Combine(OUTPUT_PATH, relativeFilePath);
            if (File.Exists(outputFilePath))
            {
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

            string[] contents = File.ReadAllLines(filePath);

            ref string definitionLine = ref contents[0];
            string[] splitDefinitionLine = definitionLine.Split('"');
            if (splitDefinitionLine.Length != 3)
            {
                continue;
            }
            splitDefinitionLine[1] = "Stubbed" + Path.ChangeExtension(shaderKey, null);
            definitionLine = string.Join('"', splitDefinitionLine);

            int propertiesStartIndex = Array.IndexOf(contents, "\tProperties {");
            if (propertiesStartIndex >= 0)
            {
                for (int i = propertiesStartIndex + 1; i < contents.Length; i++)
                {
                    ref string propertyLine = ref contents[i];
                    if (propertyLine == "\t}")
                    {
                        break;
                    }
                    ModifyPropertyLine(ref propertyLine);
                }
            }

            File.WriteAllText(outputFilePath, string.Join("\r\n", contents));
            //File.WriteAllLines(outputFilePath, contents);

            static void ModifyPropertyLine(ref string line)
            {
                //int lastAttributeIndex = line.LastIndexOf(']');
                //string propertyName = lastAttributeIndex >= 0 ? line.Substring(lastAttributeIndex + 1) : line;
                //propertyName = propertyName.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries)[0];

                string[] splitLine = line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                string? propertyName = Array.Find(splitLine, x => x.StartsWith('_'));
                if (string.IsNullOrEmpty(propertyName))
                {
                    return;
                }
                if (propertyName.Contains("Color") || propertyName.Contains("Tint"))
                {
                    line = line.Replace("Vector", "Color");
                }
                else if (propertyName.Contains("Blend") && !propertyName.Contains("Internal") && !line.Contains("[Toggle(BINARYBLEND)]"))
                {
                    line = line.Replace("[HideInInspector] ", string.Empty);
                    if (!line.Contains("[Enum"))
                    {
                        line = "\t\t[Enum(UnityEngine.Rendering.BlendMode)] " + line.TrimStart();
                    }
                }
            }
        }

        Logger.LogWarning($"{shaderKeysByName.Values.Count} shader keys had no files associated!");
        foreach (var key in shaderKeysByName.Values)
        {
            Logger.LogWarning($"Shader key {key} didnt find a file!");
        }
    }
}