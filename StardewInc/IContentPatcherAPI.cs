﻿using StardewModdingAPI;

namespace StardewInc;

/// <summary>The Content Patcher API which other mods can access.</summary>
public interface IContentPatcherAPI
{
    /// <summary>Register a simple token.</summary>
    /// <param name="mod">The manifest of the mod defining the token (see <see cref="Mod.ModManifest"/> in your entry class).</param>
    /// <param name="name">The token name. This only needs to be unique for your mod; Content Patcher will prefix it with your mod ID automatically, like <c>YourName.ExampleMod/SomeTokenName</c>.</param>
    /// <param name="getValue">A function which returns the current token value. If this returns a null or empty list, the token is considered unavailable in the current context and any patches or dynamic tokens using it are disabled.</param>
    void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>?> getValue);
}