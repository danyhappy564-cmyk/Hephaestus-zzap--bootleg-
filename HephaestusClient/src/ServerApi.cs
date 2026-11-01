using System;
using System.IO;
using System.Net;
using System.Text;

namespace HephaestusClient
{
    /// <summary>
    /// Minimal HTTP client for the server mod's settings routes. Plain JSON (requestcompressed/responsecompressed = 0),
    /// which the SPT server accepts. Called from a thread-pool thread, never from the game's main thread.
    /// </summary>
    internal static class ServerApi
    {
        public const string GetRoute = ClientLogic.GetRoute;
        public const string SetRoute = ClientLogic.SetRoute;

        private static string _backendUrl;
        private static string _sessionId;

        private static string SessionId => _sessionId ?? (_sessionId = FindArg("-token=") ?? "");

        private static string BackendUrl => _backendUrl ?? (_backendUrl = FindBackendUrl());

        public static string Post(string route, string body)
        {
            var request = (HttpWebRequest)WebRequest.Create(BackendUrl.TrimEnd('/') + route);
            request.Method = "POST";
            request.Timeout = 15000;
            request.ContentType = "application/json";
            request.Headers["requestcompressed"] = "0";
            request.Headers["responsecompressed"] = "0";
            request.ServicePoint.Expect100Continue = false;
            if (SessionId.Length > 0)
            {
                request.Headers["Cookie"] = "PHPSESSID=" + SessionId;
            }

            // the local SPT server uses a self-signed certificate; only this request is affected
            request.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;

            var bytes = Encoding.UTF8.GetBytes(body ?? "{}");
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream() ?? Stream.Null, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string FindArg(string prefix)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length);
                }
            }
            return null;
        }

        // The launcher starts the game with -config={"BackendUrl":"https://127.0.0.1:6969",...}
        private static string FindBackendUrl()
        {
            var config = FindArg("-config=");
            if (config != null)
            {
                try
                {
                    if (MiniJson.Parse(config) is System.Collections.Generic.Dictionary<string, object> obj
                        && obj.TryGetValue("BackendUrl", out var url) && url is string s && s.Length > 0)
                    {
                        return s;
                    }
                }
                catch
                {
                    // fall through to the default
                }
            }

            return "https://127.0.0.1:6969";
        }
    }
}
