using System;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace EJR.Editor
{
    internal sealed class McpUnityBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static bool _restartServerAfterBuild;

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var server = GetServerInstance();
            if (server == null)
            {
                _restartServerAfterBuild = false;
                return;
            }

            _restartServerAfterBuild = GetIsListening(server);
            if (_restartServerAfterBuild)
            {
                Invoke(server, "StopServer");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!_restartServerAfterBuild)
            {
                return;
            }

            _restartServerAfterBuild = false;

            var server = GetServerInstance();
            if (server != null && !GetIsListening(server))
            {
                Invoke(server, "StartServer");
            }
        }

        private static object GetServerInstance()
        {
            var serverType = Type.GetType("McpUnity.Unity.McpUnityServer, McpUnity.Editor");
            return serverType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static bool GetIsListening(object server)
        {
            var property = server.GetType().GetProperty("IsListening", BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.GetValue(server) is bool isListening && isListening;
        }

        private static void Invoke(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)?.Invoke(target, null);
        }
    }
}
