using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace TCNotifier
{
    public class BuildChecker
    {
        private const string SuccsessfullBuildUrl = @"http://necuda:8111/app/rest/buildTypes/id:{0}/builds/status:SUCCESS/id";
        private const string FailoreBuildUrl = @"http://necuda:8111/app/rest/buildTypes/id:{0}/builds/status:FAILURE/id";
        private const string ChangesUrl = @"http://necuda:8111/app/rest/changes?build=id:{0}";
        private const string ChangeUrl = @"http://necuda:8111{0}";

        public static bool CheckBuild(string buildID, out string person)
        {
            int lastSuccessfullBuildId;
            int lastFailoreBuildId;
            var successfullBuildUrl = string.Format(SuccsessfullBuildUrl, buildID);
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(successfullBuildUrl);
            SetAuthorization(request);
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            lastSuccessfullBuildId = GetBuildIdFromResponse(response);

            var failoreBuildUrl = string.Format(FailoreBuildUrl, buildID);
            request = (HttpWebRequest) WebRequest.Create(failoreBuildUrl);
            SetAuthorization(request);
            response = (HttpWebResponse) request.GetResponse();
            lastFailoreBuildId = GetBuildIdFromResponse(response);

            if (lastSuccessfullBuildId > lastFailoreBuildId)
            {
                person = string.Empty;
                return true;
            }
            else
            {
                person = GetPerson(lastFailoreBuildId);
                return false;
            }
        }

        private static string GetPerson(int lastFailureBuildId)
        {
            string person = string.Empty;

            var successfullBuildUrl = string.Format(ChangesUrl, lastFailureBuildId);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(successfullBuildUrl);
            SetAuthorization(request);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            var personUrl = GetChangeUrlFromResponse(response);

            if (!string.IsNullOrEmpty(personUrl))
            {
                request = (HttpWebRequest)WebRequest.Create(personUrl);
                SetAuthorization(request);
                response = (HttpWebResponse)request.GetResponse();
                person = GetPersonNameFromResponse(response);
            }
                
            return person;
        }

        private static string GetPersonNameFromResponse(HttpWebResponse response)
        {
            if (response == null)
                throw new ArgumentNullException("response");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception("response status code is not OK");

            var responseStream = response.GetResponseStream();
            if (responseStream != null)
            {
                var doc = new XmlDocument();
                doc.Load(responseStream);
                if (doc.DocumentElement != null)
                {
                    var personAttr = doc.DocumentElement.Attributes["username"];
                    if (personAttr != null)
                    {
                        return personAttr.Value;
                    }
                }
            }
            return string.Empty;
        }

        private static void SetAuthorization(HttpWebRequest request)
        {
            request.Headers["Authorization"] = "Basic " +
                                               Convert.ToBase64String(
                                                   Encoding.ASCII.GetBytes("Andriy.Vandych" + ":" + "Andriy.Vandych"));
        }

        public static int GetBuildIdFromResponse(HttpWebResponse response)
        {
            if (response == null)
                throw new ArgumentNullException("response");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception("response status code is not OK");

            var responseStream = response.GetResponseStream();
            string responseText = null;
            if (responseStream != null)
            {
                using (var sr = new StreamReader(responseStream))
                {
                    responseText = sr.ReadToEnd();
                }
            }
            if (responseText == null)
                throw new Exception("Response was null");
            return Int32.Parse(responseText);
        }

        private static string GetChangeUrlFromResponse(HttpWebResponse response)
        {
            if (response == null)
                throw new ArgumentNullException("response");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new Exception("response status code is not OK");

            var responseStream = response.GetResponseStream();
            if (responseStream != null)
            {
                var doc = new XmlDocument();
                doc.Load(responseStream);
                if (doc.DocumentElement != null)
                {
                    var node = doc.DocumentElement.FirstChild;
                    if (node != null && node.Attributes != null)
                    {
                        var href = node.Attributes["href"];
                        if (href != null)
                        {
                            var url = href.Value;
                            url = string.Format(ChangeUrl, url);
                            return url;
                        }
                    }
                }
            }
            return string.Empty;
        }
    }
}