using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grapevine
{
    public interface IContentFolder
    {
        /// <summary>
        /// The action to take if the file is not found but should be. This occures when prefix is found at the begining of the path info, but the file name specified isn't found in the content folder.
        /// </summary>
        /// <value>Action<IHttpContext></value>
        Func<IHttpContext, Task> FileNotFoundHandler { get; set; }

        /// <summary>
        /// Gets the folder used when scanning for static content requests
        /// </summary>
        string FolderPath { get; set; }

        /// <summary>
        /// Gets or sets the default file to return when a directory is requested
        /// </summary>
        string IndexFileName { get; set; }

        /// <summary>
        /// Gets or sets the optional prefix for specifying when static content should be returned
        /// </summary>
        string Prefix { get; set; }

        /// <summary>
        /// Returns a list of relative paths for all files and folders in the directory
        /// </summary>
        /// <value></value>
        IList<string> DirectoryListing();
        /// <summary>
        /// If the file specified in the path info of the request matches a file in the content folder, that file will be sent in the response.
        /// </summary>
        /// <param name="context"></param>
        /// <returns>Task</returns>
        Task SendFileAsync(IHttpContext context);

        /// <summary>
        /// If the file specified in the path info of the request matches a file in the content folder, that file will be sent in the response as an attachment using the specified file name.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filename"></param>
        /// <returns>Task</returns>
        Task SendFileAsync(IHttpContext context, string filename);
    }

    public static class IContentFolderExtensions
    {
        public static void Add(this ICollection<IContentFolder> collection, string folderPath)
        {
            collection.Add(new ContentFolder(folderPath));
        }
    }
}