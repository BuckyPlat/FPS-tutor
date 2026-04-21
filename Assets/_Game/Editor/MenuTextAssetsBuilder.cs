using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public static class MenuTextAssetsBuilder
{
    private const string WarmupCharacters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
    private const string BodySourceFontPath = "Assets/ThirdParty/Photon/PhotonChat/Demos/Demo Chat/Resources/OpenSans/OpenSans-Regular.ttf";
    private const string HeadingSourceFontPath = "Assets/ThirdParty/EffectExamples/Shared/Fonts/RobotoCondensed-Bold.ttf";
    private const string ResourcesRoot = "Assets/_Game/UI/Resources";
    private const string TextRoot = "Assets/_Game/UI/Resources/Text";
    private const string FontRoot = "Assets/_Game/UI/Resources/Text/Fonts";
    private const string BodyFontAssetPath = "Assets/_Game/UI/Resources/Text/Fonts/MenuBody-Regular.asset";
    private const string HeadingFontAssetPath = "Assets/_Game/UI/Resources/Text/Fonts/MenuHeading-Bold.asset";
    private const string PanelTextSettingsPath = "Assets/_Game/UI/Resources/Text/MenuPanelTextSettings.asset";
    private const string PanelSettingsPath = "Assets/_Game/UI/MenuPanelSettings.asset";

    [MenuItem("Tools/UI Toolkit/Rebuild Menu Text Assets")]
    public static void Rebuild()
    {
        EnsureFolder("Assets", "_Game");
        EnsureFolder("Assets/_Game", "UI");
        EnsureFolder("Assets/_Game/UI", "Resources");
        EnsureFolder(ResourcesRoot, "Text");
        EnsureFolder(TextRoot, "Fonts");

        var bodySource = AssetDatabase.LoadAssetAtPath<Font>(BodySourceFontPath);
        var headingSource = AssetDatabase.LoadAssetAtPath<Font>(HeadingSourceFontPath);
        if (bodySource == null || headingSource == null)
        {
            Debug.LogError("MenuTextAssetsBuilder: missing source font asset.");
            return;
        }

        // Use the official Text Core creation flow from the docs so Unity generates
        // a valid atlas + material payload for the font asset.
        var bodyFont = GetOrCreateFontAssetViaMenu(BodyFontAssetPath, "MenuBody-Regular", bodySource);
        var headingFont = GetOrCreateFontAssetViaMenu(HeadingFontAssetPath, "MenuHeading-Bold", headingSource);
        if (bodyFont == null || headingFont == null)
        {
            Debug.LogError("MenuTextAssetsBuilder: failed to create one or more font assets.");
            return;
        }

        WarmupFontAsset(bodyFont);
        WarmupFontAsset(headingFont);

        headingFont.fallbackFontAssetTable ??= new List<FontAsset>();
        headingFont.fallbackFontAssetTable.Clear();
        headingFont.fallbackFontAssetTable.Add(bodyFont);
        EditorUtility.SetDirty(bodyFont);
        EditorUtility.SetDirty(headingFont);

        var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(PanelTextSettingsPath);
        if (textSettings == null)
        {
            textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            textSettings.name = "MenuPanelTextSettings";
            AssetDatabase.CreateAsset(textSettings, PanelTextSettingsPath);
        }

        textSettings.defaultFontAsset = bodyFont;
        textSettings.defaultFontAssetPath = "Text/Fonts";
        textSettings.fallbackFontAssets = new List<FontAsset> { headingFont };
        textSettings.clearDynamicDataOnBuild = false;
        EditorUtility.SetDirty(textSettings);

        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings == null)
        {
            Debug.LogError($"MenuTextAssetsBuilder: missing panel settings at {PanelSettingsPath}.");
            return;
        }

        panelSettings.textSettings = textSettings;
        EditorUtility.SetDirty(panelSettings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "MenuTextAssetsBuilder: rebuilt menu text assets.\n" +
            $"Body: {AssetDatabase.GetAssetPath(bodyFont)}\n" +
            $"Heading: {AssetDatabase.GetAssetPath(headingFont)}\n" +
            $"Text Settings: {AssetDatabase.GetAssetPath(textSettings)}");
    }

    private static FontAsset GetOrCreateFontAssetViaMenu(string assetPath, string assetName, Font sourceFont)
    {
        var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
        if (existing != null && existing.atlasTexture != null)
        {
            existing.isMultiAtlasTexturesEnabled = true;
            return existing;
        }

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        var sourceAssetPath = AssetDatabase.GetAssetPath(sourceFont);
        var sourceFolder = Path.GetDirectoryName(sourceAssetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(sourceFolder))
        {
            Debug.LogError($"MenuTextAssetsBuilder: unable to resolve folder for source font {sourceAssetPath}.");
            return null;
        }

        var before = new HashSet<string>(AssetDatabase.FindAssets("t:FontAsset", new[] { sourceFolder }));
        var previousSelection = Selection.activeObject;
        Selection.activeObject = sourceFont;

        if (!EditorApplication.ExecuteMenuItem("Assets/Create/Text Core/Font Asset/SDF"))
        {
            Selection.activeObject = previousSelection;
            Debug.LogError("MenuTextAssetsBuilder: failed to execute 'Assets/Create/Text Core/Font Asset/SDF'.");
            return null;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = previousSelection;

        FontAsset created = null;
        foreach (var guid in AssetDatabase.FindAssets("t:FontAsset", new[] { sourceFolder }))
        {
            if (before.Contains(guid))
            {
                continue;
            }

            var candidatePath = AssetDatabase.GUIDToAssetPath(guid);
            var candidate = AssetDatabase.LoadAssetAtPath<FontAsset>(candidatePath);
            if (candidate != null && candidate.sourceFontFile == sourceFont)
            {
                created = candidate;
                break;
            }
        }

        if (created == null)
        {
            Debug.LogError($"MenuTextAssetsBuilder: could not locate generated font asset for {sourceFont.name}.");
            return null;
        }

        var createdPath = AssetDatabase.GetAssetPath(created);
        if (createdPath != assetPath)
        {
            var moveError = AssetDatabase.MoveAsset(createdPath, assetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError($"MenuTextAssetsBuilder: failed to move font asset. {moveError}");
                return null;
            }

            created = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
        }

        created.name = assetName;
        created.isMultiAtlasTexturesEnabled = true;
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static void WarmupFontAsset(FontAsset fontAsset)
    {
        fontAsset.ReadFontAssetDefinition();
        fontAsset.TryAddCharacters(WarmupCharacters, out _, false);
        EditorUtility.SetDirty(fontAsset);
    }

    private static void EnsureFolder(string parentPath, string childFolderName)
    {
        var fullPath = $"{parentPath}/{childFolderName}";
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentPath, childFolderName);
        }
    }
}
