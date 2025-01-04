﻿using NPrng.Generators;
using PACommon;
using PACommon.JsonUtils;
using ProgressAdventure.WorldManagement.Content;
using System.Diagnostics.CodeAnalysis;
using PACTools = PACommon.Tools;

namespace ProgressAdventure.WorldManagement
{
    /// <summary>
    /// Object, representing a tile in a chunk.
    /// </summary>
    public class Tile : IJsonConvertableExtra<Tile, SplittableRandom>
    {
        #region Public fields
        /// <summary>
        /// The relative position of the tile.
        /// </summary>
        public readonly (long x, long y) relativePosition;
        /// <summary>
        /// The terrain layer of the tile.
        /// </summary>
        public readonly TerrainContent terrain;
        /// <summary>
        /// The structure layer of the tile.
        /// </summary>
        public readonly StructureContent structure;
        /// <summary>
        /// The population on this tile.
        /// </summary>
        public readonly PopulationContent population;
        #endregion

        #region Public properties
        /// <summary>
        /// How many times the tile has been visited.
        /// </summary>
        public int Visited { get; private set; }
        #endregion

        #region Public constructors
        /// <summary>
        /// <inheritdoc cref="Tile"/><br/>
        /// Creates a new Tile object.
        /// </summary>
        /// <param name="absoluteX">The absolute x position of the tile.</param>
        /// <param name="absoluteY">The absolute y position of the tile.</param>
        /// <param name="chunkRandom">The parrent chunk's random generator.</param>
        public Tile(long absoluteX, long absoluteY, SplittableRandom? chunkRandom)
            : this(absoluteX, absoluteY, chunkRandom, null) { }

        /// <summary>
        /// <inheritdoc cref="Tile"/><br/>
        /// Load an existing Tile object, from json.
        /// </summary>
        /// <param name="chunkRandom">The parrent chunk's random generator.</param>
        /// <param name="position">The position of the Tile.</param>
        /// <param name="visited"><inheritdoc cref="Visited" path="//summary"/></param>
        /// <param name="terrain"><inheritdoc cref="terrain" path="//summary"/></param>
        /// <param name="structure"><inheritdoc cref="structure" path="//summary"/></param>
        /// <param name="population"><inheritdoc cref="structure" path="//summary"/></param>
        public Tile(SplittableRandom chunkRandom, (long x, long y) position, int? visited, TerrainContent terrain, StructureContent structure, PopulationContent population)
            : this(position.x, position.y, chunkRandom, visited, terrain, structure, population) { }
        #endregion

        #region Private constructors
        /// <summary>
        /// <inheritdoc cref="Tile"/><br/>
        /// The coordinates only need to be absolute, if a new tile is created.
        /// </summary>
        /// <param name="absoluteX">The absolute x coordinate of the Tile.</param>
        /// <param name="absoluteY">The absolute y coordinate of the Tile.</param>
        /// <param name="chunkRandom">The parrent chunk's random generator.</param>
        /// <param name="visited"><inheritdoc cref="Visited" path="//summary"/></param>
        /// <param name="terrain"><inheritdoc cref="terrain" path="//summary"/></param>
        /// <param name="structure"><inheritdoc cref="structure" path="//summary"/></param>
        /// <param name="population"><inheritdoc cref="population" path="//summary"/></param>
        private Tile(long absoluteX, long absoluteY, SplittableRandom? chunkRandom = null, int? visited = null, TerrainContent? terrain = null, StructureContent? structure = null, PopulationContent? population = null)
        {
            relativePosition = (Utils.Mod(absoluteX, Constants.CHUNK_SIZE), Utils.Mod(absoluteY, Constants.CHUNK_SIZE));
            Visited = visited ?? 0;
            if (terrain is null || structure is null || population is null)
            {
                chunkRandom ??= Chunk.GetChunkRandom((absoluteX, absoluteY));
                var noiseValues = WorldUtils.GetNoiseValues(absoluteX, absoluteY);
                WorldUtils.ShiftNoiseValues(noiseValues);
                terrain ??= WorldUtils.CalculateClosestContent<TerrainContent>(chunkRandom, noiseValues);
                if (structure is null)
                {
                    // less structures on water
                    var noStructureDL = WorldUtils.noStructureDifferenceLimit;
                    if (terrain.subtype == ContentType.Terrain.OCEAN)
                    {
                        noStructureDL -= 0.1;
                    }
                    else if (terrain.subtype == ContentType.Terrain.SHORE)
                    {
                        noStructureDL -= 0.05;
                    }
                    structure = WorldUtils.CalculateClosestContent<StructureContent>(chunkRandom, noiseValues, noStructureDL);
                }
                if (population is null)
                {
                    // less population on not structures
                    var noPopulationDL = WorldUtils.noPopulationDifferenceLimit;
                    if (structure.subtype == ContentType.Structure.NONE)
                    {
                        noPopulationDL -= 0.1;
                    }
                    population = WorldUtils.CalculateClosestContent<PopulationContent>(chunkRandom, noiseValues, noPopulationDL);
                }
            }
            this.terrain = terrain;
            this.structure = structure;
            this.population = population;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Should be called, if a player is on this tile.
        /// </summary>
        public void Visit()
        {
            Visited++;
            terrain.Visit(this);
            structure.Visit(this);
            population.Visit(this);
        }
        #endregion

        #region JsonConvert
        #region Protected properties
        static List<(Action<JsonDictionary, SplittableRandom> objectJsonCorrecter, string newFileVersion)> IJsonConvertableExtra<Tile, SplittableRandom>.VersionCorrecters { get; } =
        [
            // 2.1.1 -> 2.2
            ((oldJson, random) =>
            {
                // snake case rename
                JsonDataCorrecterUtils.RemapKeysIfExist(oldJson, new Dictionary<string, string>
                {
                    ["xPos"] = "x_position",
                    ["yPos"] = "y_position",
                });
            }, "2.2"),
        ];
        #endregion

        public JsonDictionary ToJson()
        {
            return new JsonDictionary
            {
                [Constants.JsonKeys.Tile.RELATIVE_POSITION_X] = relativePosition.x,
                [Constants.JsonKeys.Tile.RELATIVE_POSITION_Y] = relativePosition.y,
                [Constants.JsonKeys.Tile.VISITED] = Visited,
                [Constants.JsonKeys.Tile.TERRAIN] = terrain.ToJson(),
                [Constants.JsonKeys.Tile.STRUCTURE] = structure.ToJson(),
                [Constants.JsonKeys.Tile.POPULATION] = population.ToJson(),
            };
        }

        static bool IJsonConvertableExtra<Tile, SplittableRandom>.FromJsonWithoutCorrection(JsonDictionary objectJson, SplittableRandom extraData, string fileVersion, [NotNullWhen(true)] ref Tile? convertedObject)
        {
            if (!(
                PACTools.TryParseJsonValue<Tile, long>(objectJson, Constants.JsonKeys.Tile.RELATIVE_POSITION_X, out var xPos, isCritical: true) &&
                PACTools.TryParseJsonValue<Tile, long>(objectJson, Constants.JsonKeys.Tile.RELATIVE_POSITION_Y, out var yPos, isCritical: true)
            ))
            {
                return false;
            }

            var success = true;
            success &= PACTools.TryParseJsonValue<Tile, int?>(objectJson, Constants.JsonKeys.Tile.VISITED, out var visited);
            success &= PACTools.TryCastJsonAnyValue<Tile, JsonDictionary>(objectJson, Constants.JsonKeys.Tile.TERRAIN, out var terrainJson, isStraigthCast: true);
            success &= TerrainContent.FromJson(extraData, terrainJson, fileVersion, out var terrain);
            success &= PACTools.TryCastJsonAnyValue<Tile, JsonDictionary>(objectJson, Constants.JsonKeys.Tile.STRUCTURE, out var structureJson, isStraigthCast: true);
            success &= StructureContent.FromJson(extraData, structureJson, fileVersion, out var structure);
            success &= PACTools.TryCastJsonAnyValue<Tile, JsonDictionary>(objectJson, Constants.JsonKeys.Tile.POPULATION, out var populationJson, isStraigthCast: true);
            success &= PopulationContent.FromJson(extraData, populationJson, fileVersion, out var population);

            convertedObject = new Tile(xPos, yPos, extraData, visited, terrain, structure, population);
            return success;
        }
        #endregion
    }
}