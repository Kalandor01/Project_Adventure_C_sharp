﻿using ProgressAdventure;
using ProgressAdventure.Entity;
using ProgressAdventure.Enums;
using ProgressAdventure.Extensions;
using ProgressAdventure.ItemManagement;
using ProgressAdventure.SettingsManagement;
using ProgressAdventure.WorldManagement;
using SaveFileManager;
using System.Reflection;
using Xunit;
using Attribute = ProgressAdventure.Enums.Attribute;
using PAConstants = ProgressAdventure.Constants;

namespace ProgressAdventureTests
{
    public static class Tests
    {
        /// <summary>
        /// Checks if all item IDs can be turned into items.
        /// </summary>
        [Fact]
        public static TestResultDTO? AllItemTypesExistAndLoadable()
        {
            var itemAmount = 3;

            var allItems = new List<Item>();

            // all item IDs can turn into items
            foreach (var itemID in ItemUtils.GetAllItemTypes())
            {
                Item item;
                try
                {
                    item = new Item(itemID, itemAmount);
                }
                catch (Exception ex)
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"Couldn't create item from type \"{itemID}\": " + ex);
                }

                allItems.Add(item);
            }

            // items loadable from json and are the same as before load
            foreach (var item in allItems)
            {
                Item loadedItem;

                try
                {
                    var itemJson = item.ToJson();
                    loadedItem = Item.FromJson(itemJson, PAConstants.SAVE_VERSION);

                    if (loadedItem is null)
                    {
                        throw new ArgumentNullException(item.Type.ToString());
                    }
                }
                catch (Exception ex)
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"Loading item from json failed for \"{item.Type}\": " + ex);
                }

                if (
                    loadedItem.Type == item.Type &&
                    loadedItem.Amount == item.Amount &&
                    loadedItem.DisplayName == item.DisplayName &&
                    loadedItem.Consumable == item.Consumable
                )
                {
                    continue;
                }

                return new TestResultDTO(LogSeverity.FAIL, $"Original item, and item loaded from json are not the same for \"{item.Type}\"");
            }

            return null;
        }

        /// <summary>
        /// Checks if all entities have a type name, and can be loaded from json.
        /// </summary>
        [Fact]
        public static TestResultDTO? AllEntitiesLoadable()
        {
            RandomStates.Initialise();

            var entityType = typeof(Entity);
            var paAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == nameof(ProgressAdventure)).First();
            var entityTypes = paAssembly.GetTypes().Where(entityType.IsAssignableFrom);
            var filteredEntityTypes = entityTypes.Where(type => type != typeof(Entity) && type != typeof(Entity<>));

            var entities = new List<Entity>();

            // check if entity exists and loadable from jsom
            foreach (var type in filteredEntityTypes)
            {
                // get entity type name
                var entityTypeMapName = "entityTypeMap";
                IDictionary<string, Type> entityTypeMap;

                try
                {
                    entityTypeMap = Tools.GetInternalFieldFromStaticClass<IDictionary<string, Type>>(typeof(EntityUtils), entityTypeMapName);
                }
                catch (Exception ex)
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"Exeption because of (outdated?) test structure in {nameof(EntityUtils)}: " + ex);
                }

                string? typeName = null;
                foreach (var eType in entityTypeMap)
                {
                    if (eType.Value == type)
                    {
                        typeName = eType.Key;
                        break;
                    }
                }

                if (typeName is null)
                {
                    return new TestResultDTO(LogSeverity.FAIL, "Entity type has no type name in entity type map");
                }


                var defEntityJson = new Dictionary<string, object?>()
                {
                    ["type"] = typeName,
                };

                Entity entity;

                try
                {
                    entity = Entity.AnyEntityFromJson(defEntityJson);

                    if (entity is null)
                    {
                        throw new ArgumentNullException(typeName);
                    }
                }
                catch (Exception ex)
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"Entity creation from default json failed for \"{entityType}\": " + ex);
                }

                entities.Add(entity);
            }

            // entities loadable from json and are the same as before load
            foreach (var entity in entities)
            {
                Entity loadedEntity;

                try
                {
                    var entityJson = entity.ToJson();
                    loadedEntity = Entity.AnyEntityFromJson(entityJson);

                    if (loadedEntity is null)
                    {
                        throw new ArgumentNullException(entity.GetType().ToString());
                    }
                }
                catch (Exception ex)
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"Loading entity from json failed for \"{entity.GetType()}\": " + ex);
                }

                if (
                    loadedEntity.GetType() == entity.GetType() &&
                    loadedEntity.FullName == entity.FullName &&
                    loadedEntity.MaxHp == entity.MaxHp &&
                    loadedEntity.CurrentHp == entity.CurrentHp &&
                    loadedEntity.Attack == entity.Attack &&
                    loadedEntity.Defence == entity.Defence &&
                    loadedEntity.Agility == entity.Agility
                )
                {
                    continue;
                }

                return new TestResultDTO(LogSeverity.FAIL, $"Original entity, and entity loaded from json are not the same for \"{entity.GetType()}\"");
            }

            return null;
        }

        /// <summary>
        /// Checks if the Logger, logging values dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? LoggerLoggingValuesDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<LogSeverity>();
            var checkedDictionary = Logger.loggingValuesMap;
            var existingValues = new List<int>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out int value))
                {
                    if (existingValues.Contains(value))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the EntityUtils, facing to movement vector dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? EntityUtilsFacingToMovementVectorDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<Facing>();
            IDictionary<Facing, (int x, int y)> checkedDictionary;

            try
            {
                checkedDictionary = Tools.GetInternalFieldFromStaticClass<IDictionary<Facing, (int x, int y)>>(typeof(EntityUtils), "facingToMovementVectorMap");
            }
            catch (Exception ex)
            {
                return new TestResultDTO(LogSeverity.FAIL, $"Exeption because of (outdated?) test structure in {nameof(EntityUtils)}: " + ex);
            }

            var existingValues = new List<(int x, int y)>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out (int x, int y) value))
                {
                    if (
                        existingValues.Contains(value)
                    )
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        if (
                            value.x < -1 || value.x > 1 ||
                            value.y < -1 || value.y > 1
                        )
                        {
                            return new TestResultDTO(LogSeverity.FAIL, $"The value associated to \"{key}\" is wrong.");
                        }
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the EntityUtils, attributes stat change dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? EntityUtilsAttributeStatsChangeDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<Attribute>();
            IDictionary<Attribute, (double maxHp, double attack, double defence, double agility)> checkedDictionary;

            try
            {
                checkedDictionary = Tools.GetInternalFieldFromStaticClass<IDictionary<Attribute, (double maxHp, double attack, double defence, double agility)>>(typeof(EntityUtils), "attributeStatChangeMap");
            }
            catch (Exception ex)
            {
                return new TestResultDTO(LogSeverity.FAIL, $"Exeption because of (outdated?) test structure in {nameof(EntityUtils)}: " + ex);
            }

            var existingValues = new List<(double maxHp, double attack, double defence, double agility)>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out (double maxHp, double attack, double defence, double agility) value))
                {
                    if (existingValues.Contains(value))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the EntityUtils, attribute name dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? EntityUtilsAttributeNameDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<Attribute>();
            IDictionary<Attribute, string> checkedDictionary;

            try
            {
                checkedDictionary = Tools.GetInternalFieldFromStaticClass<IDictionary<Attribute, string>>(typeof(EntityUtils), "attributeNameMap");
            }
            catch (Exception ex)
            {
                return new TestResultDTO(LogSeverity.FAIL, $"Exeption because of (outdated?) test structure in {nameof(EntityUtils)}: " + ex);
            }

            var existingValues = new List<string>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out string? value) && value is not null)
                {
                    if (existingValues.Contains(value))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the ItemUtils, item attributes dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? ItemUtilsItemAttributesDictionaryCheck()
        {
            var requiredKeys = ItemUtils.GetAllItemTypes();
            var checkedDictionary = ItemUtils.itemAttributes;

            var existingValues = new List<string>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out ItemAttributesDTO value))
                {
                    if (value.typeName is null)
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The type name in the dictionary at \"{key}\" is null.");
                    }
                    if (existingValues.Contains(value.typeName))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the type name \"{value.typeName}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value.typeName);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the SettingsUtils, action type ignore mapping dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? SettingsUtilsActionTypeIgnoreMappingDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<ActionType>();
            var checkedDictionary = SettingsUtils.actionTypeIgnoreMapping;

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out List<GetKeyMode>? value))
                {
                    if (value is null)
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The ignore map in the dictionary at \"{key}\" is null.");
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the SettingsUtils, action type response mapping dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? SettingsUtilsActionTypeResponseMappingDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<ActionType>();
            var checkedDictionary = SettingsUtils.actionTypeResponseMapping;

            var existingValues = new List<Key>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out Key value))
                {
                    if (existingValues.Contains(value))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the SettingsUtils, special key name dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? SettingsUtilsSpecialKeyNameDictionaryCheck()
        {
            var checkedDictionary = SettingsUtils.specialKeyNameMap;

            var existingKeys = new List<ConsoleKey>();
            var existingValues = new List<string>();

            foreach (var element in checkedDictionary)
            {
                if (existingKeys.Contains(element.Key))
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the key \"{element.Key}\".");
                }
                else
                {
                    existingKeys.Add(element.Key);
                }

                if (existingValues.Contains(element.Value))
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{element.Value}\", associated with \"{element.Key}\".");
                }
                else
                {
                    existingValues.Add(element.Value);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the SettingsUtils, settings key name dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? SettingsUtilsSettingsKeyNameDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<SettingsKey>();
            var checkedDictionary = SettingsUtils.settingsKeyNames;

            var existingValues = new List<string>();

            foreach (var key in requiredKeys)
            {
                if (checkedDictionary.TryGetValue(key, out string? value))
                {
                    if (value is null)
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The value in the dictionary at \"{key}\" is null.");
                    }
                    if (existingValues.Contains(value))
                    {
                        return new TestResultDTO(LogSeverity.FAIL, $"The dictionary already contains the value \"{value}\", associated with \"{key}\".");
                    }
                    else
                    {
                        existingValues.Add(value);
                    }
                }
                else
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if the WorldUtils, tile noise offsets dictionary contains all required keys and correct values.
        /// </summary>
        [Fact]
        public static TestResultDTO? WorldUtilsTileNoiseOffsetsDictionaryCheck()
        {
            var requiredKeys = Enum.GetValues<TileNoiseType>();
            IDictionary<TileNoiseType, double> checkedDictionary;

            try
            {
                checkedDictionary = Tools.GetInternalFieldFromStaticClass<IDictionary<TileNoiseType, double>>(typeof(WorldUtils), "_tileNoiseOffsets");
            }
            catch (Exception ex)
            {
                return new TestResultDTO(LogSeverity.FAIL, $"Exeption because of (outdated?) test structure in {nameof(WorldUtils)}: " + ex);
            }

            foreach (var key in requiredKeys)
            {
                if (!checkedDictionary.TryGetValue(key, out double value))
                {
                    return new TestResultDTO(LogSeverity.FAIL, $"The dictionary doesn't contain a value for \"{key}\".");
                }
            }

            return null;
        }

        /// <summary>
        /// NOT WORKING!!!<br/>
        /// Checks if all objects that implement IJsonConvertable cab be converted to and from json.<br/>
        /// ONLY CHECKS FOR SUCCESFUL CONVERSION. NOT IF THE RESULTING OBJECT HAS THE SAME VALUES FOR ATTRIBUTES OR NOT!
        /// </summary>
        [Fact]
        public static TestResultDTO? BasicJsonConvertTest()
        {
            RandomStates.Initialise();

            var jsonConvertableType = typeof(IJsonConvertable<>);
            var paAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == nameof(ProgressAdventure)).First();
            var unfilteredTypes = paAssembly.GetTypes().Where(jsonConvertableType.IsGenericAssignableFromType);
            var filteredTypes = unfilteredTypes.Where(type => type != typeof(IJsonConvertable<>));


            return new TestResultDTO(LogSeverity.PASS, "Not realy implemented!");
        }
    }
}
