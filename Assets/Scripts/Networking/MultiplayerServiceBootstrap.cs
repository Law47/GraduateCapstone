using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

public static class MultiplayerServiceBootstrap
{
    public static async Task EnsureInitializedAndSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
