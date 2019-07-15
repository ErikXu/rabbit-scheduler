using System.Text;

namespace RabbitScheduler.Processor
{
    public static class BytesExtensions
    {
        /// <summary>
        /// byte数组转字符串
        /// </summary>
        /// <param name="bytes">byte数组</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns></returns>
        public static string BytesToString(this byte[] bytes, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            return encoding.GetString(bytes);
        }

    }
}