using System;
using System.Reflection;

namespace Weaver;

public static class ReflectionExtensions
{
    public static void RaiseActionEvent<T>(this T instance, string eventName)
    {
        var backingEventField = typeof(T).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
        var eventActions = (backingEventField.GetValue(instance) as MulticastDelegate)?.GetInvocationList();

        if (eventActions == null || eventActions.Length == 0)
        {
            return;
        }

        WeaverFixes.Logger.LogMessage($"Raised event {eventName}");
        foreach (var eventAction in eventActions)
        {
            eventAction.DynamicInvoke();
        }
    }
}
