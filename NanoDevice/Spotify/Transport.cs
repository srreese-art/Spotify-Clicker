using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace SpotifyClicker.Spotify
{
    public sealed class Reply
    {
        public int Status;
        public string Body = "";
        public int RetrySeconds;
    }
    public sealed class Transport
    {
        // nanoFramework's PEM string overload appends the NUL terminator required
        // by its native parser. The byte[] overload passes bytes through unchanged.
        private readonly X509Certificate _roots = new X509Certificate(TrustAnchors.Pem);
        private string _stage = "idle";
        private long _stageStarted;
        public Transport()
        {
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(10000);
                    string stage = _stage;
                    if (stage != "idle") Console.WriteLine("Spotify HTTPS: " + stage + " (" + ((Clock.Milliseconds - _stageStarted) / 1000) + " seconds).");
                }
            }).Start();
        }
        private void Stage(string stage)
        {
            _stageStarted = Clock.Milliseconds;
            _stage = stage;
            Console.WriteLine("Spotify HTTPS: " + stage + ".");
        }
        // Both the polling and command connections honor server backoff.
        private static long _blockedUntil;
        private static readonly object BackoffSync = new object();
        public long BlockedUntil
        {
            get { lock (BackoffSync) { return _blockedUntil; } }
            private set { lock (BackoffSync) { if (value > _blockedUntil) _blockedUntil = value; } }
        }
        public bool Available { get { return Clock.IsSynchronized && Clock.Milliseconds >= BlockedUntil; } }
        public Reply Send(string url, string method, string body, string accessToken)
        {
            if (!Available) return new Reply { Status = 0 };
            if (!url.StartsWith("https://accounts.spotify.com/") && !url.StartsWith("https://api.spotify.com/")) throw new ArgumentException("Unexpected API host.");
            long started = Clock.Milliseconds;
            try
            {
                Stage(accessToken == null ? "starting token request" :
                    (method == "GET" ? "starting background playback status request" : "starting playback action " + method));
                using (HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url))
                {
                    request.Method = method; request.Timeout = 10000; request.ReadWriteTimeout = 10000;
                    // The installed nanoFramework stack can leave disposed sockets
                    // in its keep-alive pool. Use fresh connections until connection
                    // reuse has been verified on this device/runtime combination.
                    request.KeepAlive = false;
                    request.HttpsAuthentCert = _roots;
                    request.SslVerification = SslVerification.CertificateRequired;
                    request.SslProtocols = SslProtocols.Tls12;
                    // Restricted headers must use HttpWebRequest properties.
                    request.Accept = "application/json";
                    if (accessToken != null) request.Headers.Add("Authorization", "Bearer " + accessToken);
                    if (method == "POST" || method == "PUT")
                    {
                        byte[] data = Encoding.UTF8.GetBytes(body ?? "");
                        request.ContentType = accessToken == null ? "application/x-www-form-urlencoded" : "application/json";
                        request.ContentLength = data.Length;
                        if (data.Length > 0)
                        {
                            Stage("opening request stream (DNS/TCP/TLS)");
                            using (Stream stream = request.GetRequestStream())
                            {
                                Stage("writing request body");
                                stream.Write(data, 0, data.Length);
                            }
                        }
                    }
                    HttpWebResponse response;
                    Stage("waiting for response headers (may include DNS/TCP/TLS)");
                    try { response = (HttpWebResponse)request.GetResponse(); }
                    catch (WebException error)
                    {
                        response = error.Response as HttpWebResponse;
                        if (response == null) throw;
                    }
                    using (response)
                    {
                        Reply reply = new Reply { Status = (int)response.StatusCode };
                        Stage("HTTP " + reply.Status + "; reading response body");
                        string retry = response.Headers["Retry-After"];
                        if (reply.Status == 429)
                        {
                            reply.RetrySeconds = RetryAfter(retry);
                            BlockedUntil = Clock.Milliseconds + (long)reply.RetrySeconds * 1000;
                        }
                        // The response owns this stream. Disposing the stream
                        // directly closes its socket without removing it from the
                        // nanoFramework connection pool. Dispose only the response.
                        try
                        {
                            Stream stream = response.GetResponseStream();
                            // Playback responses include track metadata; keep a hard memory ceiling.
                            byte[] buffer = new byte[1024];
                            using (MemoryStream output = new MemoryStream())
                            {
                                int read;
                                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    if (output.Length + read > 24576) throw new IOException("API response exceeds device limit.");
                                    output.Write(buffer, 0, read);
                                }
                                byte[] bytes = output.ToArray(); reply.Body = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                            }
                        }
                        catch
                        {
                            // A partial/unread response cannot be reused. Response
                            // disposal will remove it from the pool and close it.
                            request.KeepAlive = false;
                            throw;
                        }
                        return reply;
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("Spotify HTTPS failed at " + _stage + ": " + error.GetType().Name);
                BlockedUntil = Clock.Milliseconds + 3000; return new Reply { Status = 0 };
            }
            finally
            {
                _stage = "idle";
                Console.WriteLine("Spotify HTTPS request completed in " + (Clock.Milliseconds - started) + " ms.");
            }
        }
        private static int RetryAfter(string value)
        {
            try { int seconds = int.Parse(value); return seconds < 1 ? 1 : seconds; }
            catch { return 30; }
        }
    }
}
