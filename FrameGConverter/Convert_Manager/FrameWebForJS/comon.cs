using System;
using System.Collections.Generic;
using System.Text;

namespace Convert_Manager.FrameWebForJS
{
    public static class comon
    {
        /// <summary>
        /// バイト数で文字列を抽出し、削除する
        /// </summary>
        /// <param name="str"></param>
        /// <param name="byteCount"></param>
        /// <returns></returns>
        public static string byteSubstr(ref string str, int byteCount)
        {
            string result = comon.GetByteSubstr(str, byteCount);
            str = str.Remove(0, result.Length);
            return result;
        }

        private static string GetByteSubstr(string str, int byteCount)
        {
            //Shift-JISのエンコーディングを取得する
            Encoding enc = Encoding.GetEncoding("Shift_JIS");

            //指定したバイト数が文字バイト数以上であれば文字列をそのまま返す
            if (enc.GetByteCount(str) <= byteCount)
            {
                return str;
            }

            //文字列のバイト配列を取得する
            byte[] b = enc.GetBytes(str);

            //①指定されたバイト数で文字を切り出す
            string result = enc.GetString(b, 0, byteCount);

            //②指定されたバイト数+1バイトで文字を切り出す
            string result2 = enc.GetString(b, 0, byteCount + 1);

            //①と②の文字数を比較する
            if (result.Length == result2.Length)
            {
                //同じなら①から最後の１文字を削除した文字列を返す
                return result.Remove(result.Length - 1);
            }
            else
            //異なれば①をそのまま返す
            {
                return result;
            }
        }
    }
}
