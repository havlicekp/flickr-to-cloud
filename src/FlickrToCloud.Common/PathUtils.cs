using System.Text;

namespace FlickrToCloud.Common
{
    public static class PathUtils
    {
        /// <summary>
        /// Combines strings in <paramref name="parameters"/> into a path.
        /// Any duplicate backslashes are removed.
        /// ['/Folder/', '/Subfolder/'] => /Folder/Subfolder
        /// </summary>
        public static string CombinePath(params string[] parameters)
        {
            // /Test + /
            var builder = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                var pathPart = parameters[i].TrimStart('/').TrimEnd('/');
                if (pathPart.Length != 0)
                {
                    builder.Append($"/{pathPart}");
                }
            }

            return builder.Length == 0 ? "/" : builder.ToString();
        }

        /// <summary>
        /// Returns true if <paramref name="path"/> is a root folder.
        /// '/' => true
        /// </summary>
        public static bool PathHasSubfolders(string path)
        {
            return path.LastIndexOf('/') > 0;
        }

        /// <summary>
        /// Returns last folder for a path.
        /// '/Folder/Subfolder' => Subfolder
        /// '/' => empty string
        /// </summary>
        public static string GetLastFolder(string path)
        {
            var slashIdx = path.LastIndexOf('/');
            return path.Substring(slashIdx + 1);
        }

        /// <summary>
        /// Returns <paramref name="path"/> except the last folder.
        /// '/Folder/Subfolder' => /Folder
        /// '/Folder' => /Folder
        /// '/' => '/'
        /// </summary>
        public static string RemoveLastFolder(string path)
        {
            var slashIdx = path.LastIndexOf('/');
            return slashIdx == 0 ? path : path.Remove(slashIdx);
        }

    }
}