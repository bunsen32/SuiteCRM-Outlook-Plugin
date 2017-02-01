﻿/**
 * Outlook integration for SuiteCRM.
 * @package Outlook integration for SuiteCRM
 * @copyright SalesAgility Ltd http://www.salesagility.com
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU AFFERO GENERAL PUBLIC LICENSE as published by
 * the Free Software Foundation; either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU AFFERO GENERAL PUBLIC LICENSE
 * along with this program; if not, see http://www.gnu.org/licenses
 * or write to the Free Software Foundation,Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA 02110-1301  USA
 *
 * @author SalesAgility <info@salesagility.com>
 */
using System;
using System.Text;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using SuiteCRMClient.Logging;

namespace SuiteCRMClient
{
    public static class CrmRestServer
    {
        private static readonly JsonSerializer Serializer;
        private static ILogger Log;

        static CrmRestServer()
        {
            Serializer = new JsonSerializer();
            Serializer.Converters.Add(new Newtonsoft.Json.Converters.JavaScriptDateTimeConverter());
            Serializer.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        }

        public static void SetLog(ILogger log)
        {
            Log = log;
        }

        public static Uri SuiteCRMURL { get; set; }

        public static T GetCrmResponse<T>(string strMethod, object objInput)
        {
            try
            {
                var request = CreateCrmRestRequest(strMethod, objInput);
                var jsonResponse = GetResponseString(request);
                return DeserializeJson<T>(jsonResponse);
            }
            catch (Exception ex)
            {
                Log.Warn($"Tried calling '{strMethod}' with parameter '{objInput}'");
                Log.Error($"Failed calling '{strMethod}'", ex);
                throw;
            }
        }

        private static T DeserializeJson<T>(string responseJson)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(responseJson);
            }
            catch (JsonReaderException parseError)
            {
                throw new Exception($"Failed to parse JSON ({parseError.Message}): {responseJson}");
            }
        }

        private static HttpWebRequest CreateCrmRestRequest(string strMethod, object objInput)
        {
            try
            {
                var requestUrl = SuiteCRMURL.AbsoluteUri + "service/v4_1/rest.php";
                var restData = SerialiseJson(objInput);
                var jsonData =
                    $"method={WebUtility.UrlEncode(strMethod)}&input_type=JSON&response_type=JSON&rest_data={WebUtility.UrlEncode(restData)}";

                var contentTypeAndEncoding = "application/x-www-form-urlencoded; charset=utf-8";
                var bytes = Encoding.UTF8.GetBytes(jsonData);
                return CreatePostRequest(requestUrl, bytes, contentTypeAndEncoding);
            }
            catch (Exception problem)
            {
                throw new Exception($"Could not construct '{strMethod}' request", problem);
            }
        }

        private static string SerialiseJson(object parameters)
        {
            var buffer = new StringBuilder();
            var swriter = new StringWriter(buffer);
            Serializer.Serialize(swriter, parameters);
            return buffer.ToString();
        }

        private static string GetResponseString(HttpWebRequest request)
        {
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"{response.StatusCode} {response.StatusDescription} from {response.Method} {response.ResponseUri}");
                }

               return GetStringFromWebResponse(response);
            }
        }

        private static string GetStringFromWebResponse(HttpWebResponse response)
        {
            using (var input = response.GetResponseStream())
            using (var reader = new StreamReader(input))
            {
                return reader.ReadToEnd();
            }
        }

        private static HttpWebRequest CreatePostRequest(string requestUrl, byte[] bytes, string contentTypeAndEncoding)
        {
            var request = WebRequest.Create(requestUrl) as HttpWebRequest;

            request.Method = "POST";
            request.ContentLength = bytes.Length;
            request.ContentType = contentTypeAndEncoding;

            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }
            return request;
        }
    }
}