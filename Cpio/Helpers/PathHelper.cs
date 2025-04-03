namespace Cpio.Helpers
{
    /// <summary>
    /// 路径处理辅助类
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// 规范化路径，去除多余斜杠
        /// </summary>
        /// <param name="path">要规范化的路径</param>
        /// <returns>规范化后的路径</returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return string.Join("/", path.Split('/')
                .Where(part => !string.IsNullOrEmpty(part)));
        }
    }
}
