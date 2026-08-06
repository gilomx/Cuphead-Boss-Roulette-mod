using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildShaderBundle
{
    public const string BundleName = "gilomx-boss-roulette-shaders";

    public static void Build()
    {
        const string shaderPath =
            "Assets/BossRouletteSaturation.shader";
        var importer = AssetImporter.GetAtPath(shaderPath);
        if (importer == null)
            throw new InvalidOperationException(
                "No se encontró el shader: " + shaderPath);

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        const string uiShaderPath =
            "Assets/BossRouletteUiSaturation.shader";
        var uiImporter = AssetImporter.GetAtPath(uiShaderPath);
        if (uiImporter == null)
            throw new InvalidOperationException(
                "UI shader was not found: " + uiShaderPath);

        uiImporter.assetBundleName = BundleName;
        uiImporter.SaveAndReimport();

        const string outputDirectory = "AssetBundles";
        Directory.CreateDirectory(outputDirectory);
        var manifest = BuildPipeline.BuildAssetBundles(
            outputDirectory,
            BuildAssetBundleOptions.ForceRebuildAssetBundle |
            BuildAssetBundleOptions.DeterministicAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null)
            throw new InvalidOperationException(
                "Unity no pudo compilar el AssetBundle.");

        var outputPath = Path.GetFullPath(
            Path.Combine(outputDirectory, BundleName));
        if (!File.Exists(outputPath))
            throw new FileNotFoundException(
                "El AssetBundle no fue generado.", outputPath);

        var bundle = AssetBundle.LoadFromFile(outputPath);
        if (bundle == null)
            throw new InvalidOperationException(
                "The generated shader AssetBundle could not be opened.");
        try
        {
            var expectedShaderPaths = new[]
            {
                "Assets/BossRouletteSaturation.shader",
                "Assets/BossRouletteUiSaturation.shader"
            };
            for (var i = 0; i < expectedShaderPaths.Length; i++)
            {
                if (bundle.LoadAsset<Shader>(expectedShaderPaths[i]) == null)
                    throw new InvalidOperationException(
                        "Shader missing from AssetBundle: " +
                        expectedShaderPaths[i]);
            }
        }
        finally
        {
            bundle.Unload(true);
        }
        Debug.Log("GILOMX_SHADER_BUNDLE=" + outputPath);
    }
}
