using UnityEngine;

namespace Platform
{
    public class APICollection : MonoBehaviour
    {
        private static string _baseURL;

        public static void SetEnvironment(string environment)
        {
            if (environment == "Production")
                _baseURL = "https://hint.8chili.com";
            else if (environment == "Staging")
                _baseURL = "https://staging.8chili.com";

        }

        public static string PinLogin(string pin)
        {
            return string.Concat(_baseURL, "/api/v1.0/ca/vr_login?pin=", pin);
        }
        public static string Login()
        {
            return string.Concat(_baseURL, "/api/v1.0/ca/login");
        }

        public static string GetModules()
        {
            return string.Concat(_baseURL, "/api/v1.0/consim/modules/list");
        }

        public static string GetSimulations(int moduleID)
        {
            return string.Concat(_baseURL, "/api/v1.0/consim/", moduleID, "/simulations/list");
        }

    }
}