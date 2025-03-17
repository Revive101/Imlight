/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Models.Player;
using System;

namespace Imlight.CoreLib.Game.Commands;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class CommandAttribute(string name) : Attribute {

    public string Name { get; } = name;

}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class AliasAttribute(params string[] aliases) : Attribute {

    public string[] Aliases { get; } = aliases;

}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class AuthRequiredAttribute(AuthLevel level) : Attribute {

    public AuthLevel Level { get; } = level;

}

[AttributeUsage(AttributeTargets.Parameter)]
internal class RemainderAttribute : Attribute {

}
