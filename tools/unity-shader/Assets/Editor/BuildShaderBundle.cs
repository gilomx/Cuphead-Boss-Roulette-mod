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
        Debug.Log("GILOMX_SHADER_BUNDLE=" + outputPath);
    }
}
