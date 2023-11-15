using Imlight.CoreLib.Login.Models;
using System;

namespace Imlight.CoreLib.Game.Commands;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class CommandAttribute : Attribute {
    public string Name { get; }

    public CommandAttribute(string name) {
        Name = name;
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class AliasAttribute : Attribute {
    public string[] Aliases { get; }

    public AliasAttribute(params string[] aliases) {
        Aliases = aliases;
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class AuthRequiredAttribute : Attribute {
    public AuthLevel Level { get; }

    public AuthRequiredAttribute(AuthLevel level) {
        Level = level;
    }
}
