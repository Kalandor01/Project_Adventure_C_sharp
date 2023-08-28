﻿using ProgressAdventure;
using ProgressAdventure.Extensions;
using ProgressAdventure.WorldManagement;
using ProgressAdventure.WorldManagement.Content;
using SaveFileManager;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using PAConstants = ProgressAdventure.Constants;
using PATools = ProgressAdventure.Tools;
using PAUtils = ProgressAdventure.Utils;

namespace PAVisualiser
{
    public static class LegacyVisualiser
    {
        #region Public function
        public static ColorData GetTileColor(Tile tile, WorldLayer layer, Dictionary<ContentTypeID, long> tileTypeCounts, double opacityMultiplier = 1)
        {
            ContentTypeID subtype;
            if (layer == WorldLayer.Structure)
            {
                subtype = tile.structure.subtype;
            }
            else if (layer == WorldLayer.Population)
            {
                subtype = tile.population.subtype;
            }
            else
            {
                subtype = tile.terrain.subtype;
            }

            if (tileTypeCounts.ContainsKey(subtype))
            {
                tileTypeCounts[subtype]++;
            }
            else
            {
                tileTypeCounts[subtype] = 1;
            }

            ColorData rawColor = VisualiserTools.contentSubtypeColorMap.TryGetValue(subtype, out ColorData rColor) ? rColor : Constants.Colors.MAGENTA;

            return new ColorData(rawColor.R, rawColor.G, rawColor.B, (byte)Math.Clamp(rawColor.A * opacityMultiplier, 0, 255));
        }

        /// <summary>
        /// Genarates an image, representing the different types of tiles, and their placements in the world.
        /// </summary>
        /// <param name="layer">Sets witch layer to export.</param>
        /// <param name="image">The generated image.</param>
        /// <param name="opacityMultiplier">The opacity multiplier for the tiles.</param>
        /// <returns>The tile count for all tile types.</returns>
        public static (Dictionary<ContentTypeID, long> tileTypeCounts, long totalTileCount)? CreateWorldLayerImage(WorldLayer layer, out Bitmap? image, double opacityMultiplier = 1)
        {
            (int x, int y) tileSize = (1, 1);


            var tileTypeCounts = new Dictionary<ContentTypeID, long>();
            var totalTileCount = 0L;

            var worldCorners = World.GetCorners();

            if (worldCorners is null)
            {
                image = null;
                return null;
            }

            var (minX, minY, maxX, maxY) = worldCorners.Value;

            (long x, long y) size = ((maxX - minX + 1) * tileSize.x, (maxY - minY + 1) * tileSize.y);

            image = new Bitmap((int)size.x, (int)size.y);
            var drawer = Graphics.FromImage(image);

            foreach (var chunk in World.Chunks.Values)
            {
                foreach (var tile in chunk.tiles.Values)
                {
                    var x = chunk.basePosition.x + tile.relativePosition.x - minX;
                    var y = chunk.basePosition.y + tile.relativePosition.y - minY;
                    var startX = x * tileSize.x;
                    var startY = size.y - y * tileSize.y - 1;
                    // find type
                    totalTileCount++;
                    var color = GetTileColor(tile, layer, tileTypeCounts, opacityMultiplier);
                    drawer.DrawRectangle(new Pen(color.ToDrawingColor()), startX, startY, tileSize.x, tileSize.y);
                }
            }
            return (tileTypeCounts, totalTileCount);
        }

        public static Dictionary<WorldLayer, (Dictionary<ContentTypeID, long> tileTypeCounts, long totalTileCount)> CreateCombinedImage(List<WorldLayer> layers, out Bitmap? image)
        {
            image = null;

            var layerCounts = new Dictionary<WorldLayer, (Dictionary<ContentTypeID, long> tileTypeCounts, long totalTileCount)>();

            foreach (var layer in Enum.GetValues<WorldLayer>())
            {
                CombineImageWithNewLayerImage(layers, layer, ref image, layerCounts);
            }

            return layerCounts;
        }

        public static void MakeImage(List<WorldLayer> layers, string exportPath)
        {
            Console.Write("Generating image...");
            var tileTypeCounts = CreateCombinedImage(layers, out Bitmap? image);
            Console.WriteLine("DONE!");

            if (tileTypeCounts is null || image is null)
            {
                return;
            }

            var txt = new StringBuilder();
            foreach (var layer in tileTypeCounts)
            {
                txt.AppendLine($"{layer.Key} tile types:");
                foreach (var type in layer.Value.tileTypeCounts)
                {
                    txt.AppendLine($"\t{type.Key.ToString()?.Split(".").Last()}: {type.Value}");
                }
                txt.AppendLine($"\tTOTAL: {layer.Value.totalTileCount}\n");
            }
            Console.WriteLine(txt);

            image.Save(exportPath);
        }

        /// <summary>
        /// Visualises the data in a save file.
        /// </summary>
        /// <param name="saveName">The name of the save to read.</param>
        public static void SaveVisualizer(string saveName)
        {
            var now = DateTime.Now;
            var visualizedSaveFolderName = $"{saveName}_{PAUtils.MakeDate(now)}_{PAUtils.MakeTime(now, ";")}";
            var displayVisualizedSavePath = Path.Join(Constants.EXPORT_FOLDER, visualizedSaveFolderName);
            var visualizedSavePath = Path.Join(Constants.EXPORT_FOLDER_PATH, visualizedSaveFolderName);

            // load
            try
            {
                SaveManager.LoadSave(saveName, false, false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ERROR: {e}");
                return;
            }

            // display
            var txt = new StringBuilder();
            txt.AppendLine($"---------------------------------------------------------------------------------------------------------------");
            txt.AppendLine($"EXPORTED DATA FROM \"{SaveData.saveName}\"");
            txt.AppendLine($"Loaded {PAConstants.SAVE_FILE_NAME_DATA}.{PAConstants.SAVE_EXT}:");
            txt.AppendLine(VisualiserTools.GetGeneralSaveData());
            txt.Append("\n---------------------------------------------------------------------------------------------------------------");
            PAUtils.PressKey(txt.ToString());
            if (MenuManager.AskYesNoUIQuestion($"Do you want export the data from \"{SaveData.saveName}\" into \"{Path.Join(displayVisualizedSavePath, Constants.EXPORT_DATA_FILE)}\"?"))
            {
                PATools.RecreateFolder(Constants.EXPORT_FOLDER);
                PATools.RecreateFolder(visualizedSaveFolderName, Constants.EXPORT_FOLDER_PATH);
                File.AppendAllText(Path.Join(visualizedSavePath, Constants.EXPORT_DATA_FILE), $"{txt}\n\n");
            }


            if (!MenuManager.AskYesNoUIQuestion($"Do you want export the world data from \"{SaveData.saveName}\" into an image at \"{displayVisualizedSavePath}\"?"))
            {
                return;
            }

            // get chunks data
            PATools.RecreateFolder(Constants.EXPORT_FOLDER);
            PATools.RecreateFolder(visualizedSaveFolderName, Constants.EXPORT_FOLDER_PATH);
            World.LoadAllChunksFromFolder(showProgressText: "Getting chunk data...");

            // make rectangle
            if (MenuManager.AskYesNoUIQuestion($"Do you want to generates the rest of the chunks in a way that makes the world rectangle shaped?", false))
            {
                World.MakeRectangle("Generating chunks...");
            }

            // fill
            if (MenuManager.AskYesNoUIQuestion($"Do you want to fill in ALL tiles in ALL generated chunks?", false))
            {
                World.FillAllChunks("Filling chunks...");
            }

            // select layers
            var layers = Enum.GetValues<WorldLayer>();
            var layerElements = new List<BaseUI?>();
            foreach (var layer in layers)
            {
                layerElements.Add(new Toggle(true, $"{layer.ToString().Capitalize()}: "));
            }
            layerElements.Add(null);
            layerElements.Add(new Button(new UIAction(GenerateImageCommand, new List<object?> { layerElements, layers, visualizedSavePath }), text: "Generate image"));

            new OptionsUI(layerElements, "Select the layers to export the data and image from:").Display();
        }
        #endregion

        #region Pivate fields
        private static void CombineImageWithNewLayerImage(List<WorldLayer> layers, WorldLayer layer, ref Bitmap? image, Dictionary<WorldLayer, (Dictionary<ContentTypeID, long> tileTypeCounts, long totalTileCount)> layerCounts)
        {
            if (!layers.Contains(layer))
            {
                return;
            }

            var structureTileTypeCounts = CreateWorldLayerImage(layer, out Bitmap? structureImage, 1.0 / layers.Count);

            if (structureTileTypeCounts is null || structureImage is null)
            {
                return;
            }

            layerCounts.Add(layer, ((Dictionary<ContentTypeID, long> tileTypeCounts, long totalTileCount))structureTileTypeCounts);

            if (image is null)
            {
                image = structureImage;
            }
            else
            {
                var graphics = Graphics.FromImage(image);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.DrawImage(structureImage, 0, 0);
            }
        }

        private static void GenerateImageCommand(List<BaseUI?> layerElements, WorldLayer[] layers, string visualizedSavePath)
        {
            // get selected layers
            var selectedLayers = new List<WorldLayer>();
            for (int x = 0; x < layers.Length; x++)
            {
                if (((layerElements[x] as Toggle)?.Value) ?? false)
                {
                    selectedLayers.Add(layers[x]);
                }
            }

            // generate image
            if (selectedLayers.Any())
            {
                var imageName = string.Join("-", selectedLayers) + ".png";
                MakeImage(selectedLayers, Path.Join(visualizedSavePath, imageName));
                PAUtils.PressKey($"Generated image as \"{imageName}\"");
            }
        }
        #endregion
    }
}
