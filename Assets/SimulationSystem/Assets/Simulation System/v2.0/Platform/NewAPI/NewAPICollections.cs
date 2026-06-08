
using System;

public class NewAPICollections
{
    private static string _baseURL = string.Empty;

    public NewAPICollections(string env)
    {
        SetEnvironment(env);
    }

    public static void SetEnvironment(string environment)
    {
        // Use StringComparison.OrdinalIgnoreCase to handle production vs Production bugs
        if (string.Equals(environment, "production", StringComparison.OrdinalIgnoreCase))
        {
            _baseURL = "https://api.8chili.com/";
        }
        else if (string.Equals(environment, "staging", StringComparison.OrdinalIgnoreCase))
        {
            _baseURL = "https://p6mhete3gf.ap-south-1.awsapprunner.com/";
        }
        else if (string.Equals(environment, "dev", StringComparison.OrdinalIgnoreCase))
        {
            _baseURL = "http://localhost:3000/";
        }

        // Ensure trailing slash exists if not empty
        if (!string.IsNullOrEmpty(_baseURL) && !_baseURL.EndsWith("/"))
        {
            _baseURL += "/";
        }
    }

    public string DeviceRegister() => _baseURL + "api/hintvr/device/provision";

    public string DeviceValidation() => _baseURL + "api/hintvr/device/validate";

    public string PinLogin(string pin) => $"{_baseURL}api/hintvr/device/pin-login?pin={pin}";

    public string GetModules(string user) => $"{_baseURL}api/hintvr/device/getmodules?user_id={user}";

    public string GetSimulations(string module) => $"{_baseURL}api/hintvr/device/getsimulations?module_id={module}";

    public string GetLeaderboard() => $"{_baseURL}api/hintvr/device/leaderboard";
    public string GetuserProgress() => $"{_baseURL}api/hintvr/device/progress";

    public string GetSessionStart() => $"{_baseURL}api/hintvr/device/session-start";
    public string GetSessionUpdate() => $"{_baseURL}api/hintvr/device/session-update";
    public string GetSessionEnd() => $"{_baseURL}api/hintvr/device/session-end";
}