using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Utilities
{
    /// <summary>
    /// Helper class with different fixity algorithm
    /// </summary>
    public static class ChecksumHelper
    {
        public static string Base64Encode(string plainText)
        {
            if (String.IsNullOrEmpty(plainText))
                return plainText;

            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            if (String.IsNullOrEmpty(base64EncodedData))
                return base64EncodedData;

            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        /// <summary>
        /// Creates the md5 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateMD5Checksum(System.IO.FileInfo currentFile, string url = "")
        {
            return RunChecksumThroughMicroService((currentFile) => ChecksumMD5Net(currentFile), currentFile, url);
        }
        /// <summary>
        /// Creates the sha1 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateSHA1Checksum(System.IO.FileInfo currentFile, string url = "")
        {
            return RunChecksumThroughMicroService((currentFile) => ChecksumSHA1Net(currentFile), currentFile, url);            
        }
        /// <summary>
        /// Creates the sha512 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateSHA512Checksum(System.IO.FileInfo currentFile, string url = "")
        {
            return RunChecksumThroughMicroService((currentFile) => ChecksumSHA512Net(currentFile), currentFile, url);            
        }
        /// <summary>
        /// Creates the sha256 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateSHA256Checksum(System.IO.FileInfo currentFile, string url = "")
        {
            return RunChecksumThroughMicroService((currentFile) => ChecksumSHA256Net(currentFile), currentFile, url);           
        }

        /// <summary>
        /// Creates the sha224 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateSHA224Checksum(System.IO.FileInfo currentFile, string url)
        {
            string responseBody = string.Empty;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    responseBody = response.Content.ReadAsStringAsync().Result;
                }

                if (!String.IsNullOrEmpty(responseBody))
                {
                    var results = responseBody.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    responseBody = results.FirstOrDefault();
                }
            }
            
            catch(Exception e)
            {
                responseBody = String.Format("Failed to retrieve checksum value! {0}", e.Message);
            }
            finally
            { }

            return responseBody;
        }

        /// <summary>
        /// Creates the sha384 checksum.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        public static string CreateSHA384Checksum(System.IO.FileInfo currentFile, string url)
        {
            string responseBody = string.Empty;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    responseBody = response.Content.ReadAsStringAsync().Result;
                }

                if (!String.IsNullOrEmpty(responseBody))
                {
                    var results = responseBody.Split(" ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    responseBody = results.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                responseBody = String.Format("Failed to retrieve checksum value! {0}", e.Message);
            }
            finally
            { }

            return responseBody;
        }

        /// <summary>
        /// Creates checksums with md5 in .Net.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Invalid parameter passed! Null reference detected.</exception>
        private static string ChecksumMD5Net(FileInfo currentFile)
        {
            StringBuilder sb = new StringBuilder();

            string path = currentFile.FullName;
            bool isTooLong = currentFile.FullName.Length > 245;
            if (isTooLong)
            {
                string tempFile = System.IO.Path.GetTempFileName();
                currentFile.CopyTo(tempFile, true);
                path = tempFile;
            }

            if (currentFile == null)
                throw new ArgumentNullException("Invalid parameter passed! Null reference detected.");

            using (System.IO.FileStream file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (MD5 md5 = MD5.Create())
                {
                    byte[] retVal = md5.ComputeHash(file);

                    for (int i = 0; i < retVal.Length; i++)
                        sb.Append(retVal[i].ToString("x2"));
                }
            }

            if (isTooLong)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch { }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Creates checksums with sha1 in .Net.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Invalid parameter passed! Null reference detected.</exception>
        private static string ChecksumSHA1Net(FileInfo currentFile)
        {
            StringBuilder sb = new StringBuilder();

            if (currentFile == null)
                throw new ArgumentNullException("Invalid parameter passed! Null reference detected.");

            string path = currentFile.FullName;
            bool isTooLong = currentFile.FullName.Length > 245;
            if (isTooLong)
            {
                string tempFile = System.IO.Path.GetTempFileName();
                currentFile.CopyTo(tempFile, true);
                path = tempFile;
            }

            using (System.IO.FileStream file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] retVal = sha1.ComputeHash(file);

                    for (int i = 0; i < retVal.Length; i++)
                        sb.Append(retVal[i].ToString("x2"));
                }
            }

            if (isTooLong)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch { }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Creates checksums with sha256 in .Net.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Invalid parameter passed! Null reference detected.</exception>
        private static string ChecksumSHA256Net(FileInfo currentFile)
        {
            StringBuilder sb = new StringBuilder();

            if (currentFile == null)
                throw new ArgumentNullException("Invalid parameter passed! Null reference detected.");

            string path = currentFile.FullName;
            bool isTooLong = currentFile.FullName.Length > 245;
            if (isTooLong)
            {
                string tempFile = System.IO.Path.GetTempFileName();
                currentFile.CopyTo(tempFile, true);
                path = tempFile;
            }

            using (System.IO.FileStream file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (SHA256 sha1 = SHA256.Create())
                {
                    byte[] retVal = sha1.ComputeHash(file);

                    for (int i = 0; i < retVal.Length; i++)
                        sb.Append(retVal[i].ToString("x2"));
                }
            }

            if (isTooLong)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch { }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Creates checksums with sha512 in .Net.
        /// </summary>
        /// <param name="currentFile">The current file.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">Invalid parameter passed! Null reference detected.</exception>
        private static string ChecksumSHA512Net(FileInfo currentFile)
        {
            StringBuilder sb = new StringBuilder();

            if (currentFile == null)
                throw new ArgumentNullException("Invalid parameter passed! Null reference detected.");

            string path = currentFile.FullName;
            bool isTooLong = currentFile.FullName.Length > 245;
            if (isTooLong)
            {
                string tempFile = System.IO.Path.GetTempFileName();
                currentFile.CopyTo(tempFile, true);
                path = tempFile;
            }

            using (System.IO.FileStream file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (SHA512 sha1 = SHA512.Create())
                {
                    byte[] retVal = sha1.ComputeHash(file);

                    for (int i = 0; i < retVal.Length; i++)
                        sb.Append(retVal[i].ToString("x2"));
                }
            }

            if (isTooLong)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch { }
            }

            return sb.ToString();
        }
        /// <summary>
        /// Runs the checksum through micro service.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="currentFileInfo">The current file information.</param>
        /// <param name="url">The URL.</param>
        /// <returns></returns>
        private static String RunChecksumThroughMicroService(Func<FileInfo, String> function, FileInfo currentFileInfo, String url)
        {
            string responseBody = string.Empty;
            if (string.IsNullOrEmpty(url))
            {
                responseBody = function(currentFileInfo);
                return responseBody;
            }

            try
            {
                Root dataResult;
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = Timeout.InfiniteTimeSpan;
                    HttpResponseMessage response = client.PostAsync(url, null).Result;
                    response.EnsureSuccessStatusCode();

                    dataResult = JsonConvert.DeserializeObject<Root>(response.Content.ReadAsStringAsync().Result);
                    responseBody = dataResult.Value;
                }

                if (String.IsNullOrEmpty(responseBody.ToString()))
                    throw new ApplicationException("Empty result from MicroService");
            }
            catch
            {
                responseBody = function(currentFileInfo);
            }
            finally
            { }

            return responseBody;
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
        internal class Root
        {
            [JsonProperty("hash")]
            public string Hash { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }
        }

        /// <summary>
        /// Generates the preingest unique identifier.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public static Guid GeneratePreingestGuid(String name)
        {
            MD5 md5 = MD5.Create();
            return new Guid(md5.ComputeHash(Encoding.Default.GetBytes(name)));
        }
    }
}
