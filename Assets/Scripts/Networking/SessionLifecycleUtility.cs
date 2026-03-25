using System;
using System.Reflection;
using System.Threading.Tasks;

public static class SessionLifecycleUtility
{
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
