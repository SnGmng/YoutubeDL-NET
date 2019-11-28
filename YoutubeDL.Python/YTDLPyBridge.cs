﻿using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace YoutubeDL.Python
{
    internal class YTDLPyBridge : IDisposable
    {
        private YouTubeDL ytdl;
        private PyScope PyScope;

        public YTDLPyBridge(YouTubeDL youtubeDL)
        {
            ytdl = youtubeDL;
            using (Py.GIL())
            {
                PyScope = Py.CreateScope();
            }
        }

        public PyObject GetOptions() => ytdl.Options.ToPyObj();
        
        public async Task<HttpResponseMessage> PythonUrlOpen(PyObject pythonre)
        {
            string fullUrl;
            dynamic data;
            dynamic pheaders;
            string method;
            string datatype = null;

            using (Py.GIL())
            {
                dynamic pythonreq = (dynamic)pythonre;
                dynamic fullUrl_x = pythonreq.__dict__.get("_full_url", null);
                fullUrl = (string)fullUrl_x;
                //string origin_req_host = (string)pythonreq.__dict__.get("origin_req_host", null);
                data = pythonreq.__dict__.get("_data", null);
                if (data != null) datatype = (string)data.__class__.__name__;
                pheaders = pythonreq.headers;
                //bool unverifiable = (bool)pythonreq.__dict__.get("unverifiable", false);
                dynamic method_x = pythonreq.get_method();
                method = (string)method_x;
            }

            HttpRequestMessage req;

            switch (method)
            {
                default:
                case "GET":
                    req = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                    break;
                case "POST":
                    req = new HttpRequestMessage(HttpMethod.Post, fullUrl);
                    break;
                case "HEAD":
                    req = new HttpRequestMessage(HttpMethod.Head, fullUrl);
                    break;
                case "PUT":
                    req = new HttpRequestMessage(HttpMethod.Put, fullUrl);
                    break;
            }

            using (Py.GIL())
            {
                if (data != null)
                {
                    if (datatype == "str")
                    {
                        Dictionary<string, string> formdata = new Dictionary<string, string>();
                        foreach (string kv in (data as string).Split('&'))
                        {
                            var s = kv.Split('=');
                            formdata.Add(s[0], s[1]);
                        }
                        req.Content = new FormUrlEncodedContent(formdata);
                    }
                    else if (datatype == "bytearray")
                    {
                        req.Content = new ByteArrayContent((byte[])data);
                    }
                }

                if (pheaders != null)
                {
                    Dictionary<string, object> headers = PythonCompat.PythonDictToManaged(pheaders);
                    foreach (var header in headers)
                    {
                        req.Headers.Add(header.Key, (string)header.Value);
                    }
                }
            }

            return await ytdl.HttpClient.SendAsync(req).ConfigureAwait(false);
        }

        public async Task<PyObject> PythonResponseToBytearray(HttpResponseMessage resp)
        {
            byte[] content = await resp.Content.ReadAsByteArrayAsync();
            using (Py.GIL())
            {
                PyObject bytearray = PyScope.Eval("bytearray(" + content.Length + ")");
                PythonCompat.WriteToBuffer(bytearray, content);
                return bytearray;
            }
        }

        public void SetCookie(PyObject pycookie)
        {
            using (Py.GIL())
            {
                Cookie cookie = new Cookie();
                dynamic pc = (dynamic)pycookie;
                dynamic dict = pc.__dict__;

                cookie.Version = (int)dict.get("version", cookie.Version);
                cookie.Name = (string)dict.get("name");
                cookie.Value = (string)dict.get("value");
                if ((bool)dict.get("port_specified", false))
                    cookie.Port = (string)dict.get("port");
                if ((bool)dict.get("domain_specified", false))
                    cookie.Domain = (string)dict.get("domain");
                if ((bool)dict.get("path_specified", false))
                    cookie.Path = (string)dict.get("path");
                cookie.Secure = (bool)dict.get("secure");
                cookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)dict.get("expires")).DateTime;
                cookie.Discard = (bool)dict.get("discard");
                ytdl.HttpClientHandler.CookieContainer.Add(cookie);
            }
        }

        public string GetCookie(string url)
        {
            var cookies = ytdl.HttpClientHandler.CookieContainer.GetCookies(new Uri(url));
            string cookiestring = "";
            foreach (Cookie cookie in cookies)
            {
                cookiestring += cookie.ToString() + ";";
            }

            if (cookies.Count != 0 && cookiestring.Length > 0)
                return cookiestring.Substring(0, cookiestring.Length - 1);
            else
                return null;
        }

        /*
        public void SetupPython()
        {
            using (Py.GIL())
            {
                PyScope = Py.CreateScope();
            }
        }

        public PyList GetAllPythonExtractors()
        {
            using (Py.GIL())
            {
                PyScope.NewScope();
                PyScope.Exec("from .extractor import gen_extractor_classes");
                PyScope.Exec("extractors = gen_extractor_classes()");
                return (PyList)PyScope.Get("extractors");
            }
        }

        public PyObject GetPythonInfoExtractor(string name)
        {
            using (Py.GIL())
            {
                PyScope.NewScope();
                PyScope.Exec("from .extractor import get_info_extractor");
                PyScope.Exec("extractor = get_info_extractor(" + name + ")");
                return PyScope.Get("extractor");
            }
        }*/

        public void CacheStore(string section, string key, PyObject data)
        {

        }

        public void CacheLoad(string section, string key, string def = null)
        {

        }

        public void ToScreen(string message, bool skip_eol = false)
        {
            ytdl.Log(message, LogType.Info, new string[] { }, writeline: !skip_eol, ytdlpy: true);
        }

        public void ReportError(string message)
        {
            ytdl.Log(message, LogType.Error, new string[] { }, ytdlpy: true);
        }

        public void ReportWarning(string message)
        {
            ytdl.Log(message, LogType.Warning, new string[] { }, ytdlpy: true);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PyScope.Dispose();
                }

                ytdl = null;

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}