using System;
using WizUnraveler.Cache;

namespace Imlight.Server.Game.Models;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ResultHandlerAttribute : Attribute
{
    public Type ResultType { get; }
    
    public ResultHandlerAttribute(Type resultType)
    {
        this.ResultType = resultType;
    }
}