using System;
using UnityEditor;

namespace Bajoterra.Editor
{
    /// <summary>
    /// Every FBX under <c>Resources/Models</c> is a hero built by <c>unity/art/blender</c>: a skinned mesh with a
    /// generic rig and a handful of baked clips. This makes sure they import that way (an avatar of their own, no
    /// animation compression so the baked poses are exact, no cameras or lights, no tangents since the materials are
    /// flat colours) whatever the editor's defaults are.
    /// </summary>
    public sealed class CharacterModelImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder, StringComparison.Ordinal))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importTangents = ModelImporterTangents.None;
            importer.isReadable = false;
        }
    }
}
