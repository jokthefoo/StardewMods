using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Objects;
using Object = StardewValley.Object;

namespace FishMod
{
    public static class ObjectIds
    {
        public const string SpritesPseudoPath = "Mods/Willy/FishMod/Sprites";
        public const string FishSpritesPath = "Assets/FishSprites.png";
        public static Texture2D fishingTextures;
        
        public const string SpecialRock = "willy.FishMod.SpecialRock";
        

        internal static void EditAssets(IDictionary<string, ObjectData> objects)
        {
            void AddItem(string id, string displayName, string description, int spriteIndex)
            {
                objects[id] = new()
                {
                    Name = id,
                    DisplayName = displayName,
                    Description = description,
                    Type = "Minerals",
                    Category = Object.GemCategory,
                    Price = 500,
                    Texture = SpritesPseudoPath,
                    SpriteIndex = spriteIndex,
                    ContextTags = new() { "not_giftable", "not_placeable"},
                };
            };
            AddItem(
                SpecialRock,
                "A Special Rock",
                "This rock looks normal, until you get it wet, and then it sparkles.",
                0);
        }
    }
}