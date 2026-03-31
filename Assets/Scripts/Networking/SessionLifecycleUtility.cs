using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

public static class SessionLifecycleUtility
{
    public static ISession TryGetSessionByType(string sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType) || MultiplayerService.Instance?.Sessions == null)
            return null;

        MultiplayerService.Instance.Sessions.TryGetValue(sessionType, out var session);
        return session;
    }

    public static ISession TryGetAnyActiveSession()
    {
        if (MultiplayerService.Instance?.Sessions == null)
            return null;

        foreach (var pair in MultiplayerService.Instance.Sessions)
        {
            if (pair.Value != null)
                return pair.Value;
        }

        return null;
    }

    public static async Task CleanupActiveSessionsAsync()
    {
        if (MultiplayerService.Instance?.Sessions == null || MultiplayerService.Instance.Sessions.Count == 0)
            return;

        var activeSessions = new List<ISession>(MultiplayerService.Instance.Sessions.Values);
        foreach (var session in activeSessions)
        {
            if (session == null)
                continue;

            if (session.IsHost)
                await TryDeleteSessionAsync(session);
            else
                await TryLeaveSessionAsync(session);
        }
    }

    public static async Task TryDeleteSessionAsync(object session)
    {
        if (session == null)
            return;

        if (await InvokeSessionTaskMethodIfExists(session, "DeleteAsync"))
            return;

        await InvokeSessionTaskMethodIfExists(session, "LeaveAsync");
    }

    public static async Task TryLeaveSessionAsync(object session)
    {
        if (session == null)
            return;

        if (await InvokeSessionTaskMethodIfExists(session, "LeaveAsync"))
            return;

        await InvokeSessionTaskMethodIfExists(session, "DeleteAsync");
    }

    private static async Task<bool> InvokeSessionTaskMethodIfExists(object session, string methodName)
    {
        var method = session.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (method == null)
            return false;

        var result = method.Invoke(session, null);
        if (result is Task task)
        {
            await task;
            return true;
        }

        return false;
    }
}
