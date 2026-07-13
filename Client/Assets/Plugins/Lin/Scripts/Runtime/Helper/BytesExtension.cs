/*
┌────────────────────────────┐
│　Description: 拓展方法
│　Remark: 
└────────────────────────────┘
*/
using System.Text;

namespace Lin.Runtime.Helper
{
    public static class BytesExtension
    {
        #region --------------- bytes加密、解密 ---------------

        /// <summary>
        /// 对数据进行加密
        /// </summary>
        /// <param name="data">要加密的数据</param>
        /// <param name="password">加密密钥</param>
        // A ^ Key = B, B ^ Key = A
        public static void Encrypt(this byte[] data, string password)
        {
            byte[] key_bytes = Encoding.Default.GetBytes(password);
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(data[i] ^ key_bytes[i % key_bytes.Length]);
        }

        /// <summary>
        /// 对数据进行解密
        /// </summary>
        /// <param name="data">要解密的数据</param>
        /// <param name="password">加密密钥</param>
        public static void Decrypt(this byte[] data, string password) => data.Encrypt(password);

        #endregion
    }
}