using System;
using UnityEditor;

namespace Bajoterra.Editor
{
    /// <summary>
    /// Every FBX under <c>Resources/Models</c> is built by <c>unity/art/blender</c>. The heroes are skinned meshes with
    /// a generic rig and a handful of baked clips: they import with an avatar of their own and no animation
    /// compression, so the baked poses are exact. The fired slugs in <c>Resources/Models/Slugs</c> are plain meshes
    /// the game moves as a whole: no rig and no clips. Neither brings cameras, lights or tangents (the materials are
    /// flat colours), whatever the editor's defaults are.
    /// </summary>
    public sealed class CharacterModelImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Models/";
        private const string SlugFolder = Folder + "Slugs/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            if (assetPath.StartsWith(SlugFolder, StringComparison.Ordinal))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.optimizeGameObjects = false;
            }
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importTangents = ModelImporterTangents.None;
            importer.isReadable = false;
        }
    }
}
