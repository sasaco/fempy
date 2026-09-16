using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.IO.Compression;

namespace FramePrintPDF
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string testStr = req.Query["test"];
            if(testStr != null)
            {
                string responseMessage = string.IsNullOrEmpty(testStr)
                    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                    : $"Hello, {testStr}. This HTTP triggered function executed successfully.";

                return new OkObjectResult(responseMessage);
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            //// base64をデコード
            //byte[] a = Convert.FromBase64String(requestBody);
            //String stCsvData = Encoding.UTF8.GetString(a);

            //// カンマ区切りで分割して配列に格納する
            //string[] stArrayData = stCsvData.Split(',');

            //// byte 配列に変換する
            //byte[] b = new byte[stArrayData.Length];
            //for (int i = 0; i < stArrayData.Length; i++)
            //    b[i] = Convert.ToByte(stArrayData[i]);

            //// gzip解凍
            //String jsonString = Unzip(b);

            var myPrintInput = new ConvertManager(requestBody);
            string base64str = "myPrintInput.getPdfSource()";

            return new OkObjectResult(base64str);
        }

        /// <summary>
        /// 圧縮データを文字列として復元します。
        /// </summary>
        public static string Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    gs.CopyTo(mso);
                }

                // System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); // memo: Shift-JISを扱うためのおまじない
                // Encoding sjisEnc = Encoding.GetEncoding("Shift_JIS");
                // return sjisEnc.GetString(mso.ToArray());
                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

    }
}
