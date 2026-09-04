using System;
using System.Net;
using System.Text;
using System.Threading;
using Formicarium.Core.Control;
using Formicarium.Core.State;
using nanoFramework.Json;

namespace Formicarium.Controller.Net
{
    /// <summary>
    /// The device's HTTP surface: a JSON API for the Blazor dashboard, and one small built-in
    /// page so the column is still usable with nothing else running.
    ///
    /// Commands take their arguments as query-string parameters rather than JSON bodies. That
    /// is a deliberate simplification — parsing a request body on a device with this little RAM
    /// buys nothing when every command here has at most two scalar arguments, and it removes a
    /// whole class of allocation failure from the path that turns the heater off.
    /// </summary>
    public sealed class HttpApiServer
    {
        private readonly ControlLoop _loop;
        private readonly object _stateLock = new object();

        private DeviceState _latest;
        private Thread _thread;

        public HttpApiServer(ControlLoop loop)
        {
            _loop = loop;
        }

        /// <summary>
        /// Publishes the most recent tick. The control loop never blocks on HTTP: it hands over
        /// a finished snapshot and moves on, so a slow or wedged client cannot delay the next
        /// temperature decision.
        /// </summary>
        public void Publish(DeviceState state)
        {
            lock (_stateLock)
            {
                _latest = state;
            }
        }

        public void Start()
        {
            _thread = new Thread(Listen);
            _thread.Start();
        }

        private void Listen()
        {
            HttpListener listener = new HttpListener("http", 80);
            listener.Start();

            while (true)
            {
                HttpListenerContext context = null;

                try
                {
                    context = listener.GetContext();
                    Handle(context);
                }
                catch
                {
                    // A malformed request must never be able to stop the server thread, because
                    // losing it means losing all remote visibility of a live colony.
                }
                finally
                {
                    if (context != null)
                    {
                        try
                        {
                            context.Response.Close();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            string path = context.Request.RawUrl;
            string query = string.Empty;

            int split = path.IndexOf('?');

            if (split >= 0)
            {
                query = path.Substring(split + 1);
                path = path.Substring(0, split);
            }

            if (path == "/api/state")
            {
                DeviceState snapshot;

                lock (_stateLock)
                {
                    snapshot = _latest;
                }

                if (snapshot == null)
                {
                    Send(context, 503, "application/json", "{\"error\":\"no state yet\"}");
                    return;
                }

                Send(context, 200, "application/json", JsonConvert.SerializeObject(snapshot));
                return;
            }

            if (path == "/api/lighting")
            {
                int mode = QueryInt(query, "mode", -1);

                if (mode < 0 || mode > 2)
                {
                    Send(context, 400, "application/json", "{\"error\":\"mode must be 0,1,2\"}");
                    return;
                }

                _loop.Lighting.Mode = (LightingMode)mode;
                Send(context, 200, "application/json", "{\"ok\":true}");
                return;
            }

            if (path == "/api/feed")
            {
                bool accepted = _loop.RequestManualFeed(DateTime.UtcNow);
                Send(context, 200, "application/json", "{\"ok\":" + (accepted ? "true" : "false") + "}");
                return;
            }

            if (path == "/api/service")
            {
                _loop.ServiceMode = QueryInt(query, "enabled", 0) == 1;
                Send(context, 200, "application/json", "{\"ok\":true}");
                return;
            }

            if (path == "/api/hydration/ack")
            {
                _loop.Hydration.Acknowledge();
                Send(context, 200, "application/json", "{\"ok\":true}");
                return;
            }

            if (path == "/" || path == "/index.html")
            {
                Send(context, 200, "text/html", FallbackPage.Html);
                return;
            }

            Send(context, 404, "application/json", "{\"error\":\"not found\"}");
        }

        private static int QueryInt(string query, string key, int fallback)
        {
            if (query == null || query.Length == 0)
            {
                return fallback;
            }

            string[] pairs = query.Split('&');

            for (int i = 0; i < pairs.Length; i++)
            {
                string[] kv = pairs[i].Split('=');

                if (kv.Length == 2 && kv[0] == key)
                {
                    try
                    {
                        return int.Parse(kv[1]);
                    }
                    catch
                    {
                        return fallback;
                    }
                }
            }

            return fallback;
        }

        private static void Send(HttpListenerContext context, int status, string contentType, string body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);

            context.Response.StatusCode = status;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
