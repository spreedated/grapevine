using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Grapevine
{
    public class ContentFolder : ContentFolderBase, IContentFolder, IDisposable
    {
        private string _indexFileName = DefaultIndexFileName;
        private string _path = string.Empty;
        private string _prefix = string.Empty;
        public ContentFolder(string path) : this(path, null, null) { }

        public ContentFolder(string path, string prefix) : this(path, prefix, null) { }

        public ContentFolder(string path, Func<IHttpContext, Task> handler) : this(path, null, handler) { }

        public ContentFolder(string path, string prefix, Func<IHttpContext, Task> handler)
        {
            this.Logger = DefaultLogger.GetInstance<IContentFolder>();
            this.FolderPath = path;
            this.Prefix = prefix;
            this.FileNotFoundHandler = handler ?? DefaultFileNotFoundHandler;
        }

        public override string FolderPath
        {
            get => this._path;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;

                var path = Path.GetFullPath(value);
                if (this._path == path) return;

                if (!Directory.Exists(path)) path = Directory.CreateDirectory(path).FullName;
                this._path = path;
                this.DirectoryMapping?.Clear();

                this.Watcher?.Dispose();
                this.Watcher = new()
                {
                    Path = this._path,
                    Filter = "*",
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                };

                this.Watcher.Created += (sender, args) => { this.AddToDirectoryListing(args.FullPath); };
                this.Watcher.Deleted += (sender, args) => { this.RemoveFromDirectoryListing(args.FullPath); };
                this.Watcher.Renamed += (sender, args) => { this.RenameInDirectoryListing(args.OldFullPath, args.FullPath); };
            }
        }

        public override string IndexFileName
        {
            get { return this._indexFileName; }
            set
            {
                if (string.IsNullOrWhiteSpace(value) || this._indexFileName.Equals(value, StringComparison.CurrentCultureIgnoreCase)) return;
                this._indexFileName = value;
                this.DirectoryMapping?.Clear();
            }
        }

        public ILogger<IContentFolder> Logger { get; protected set; }
        public override string Prefix
        {
            get { return this._prefix; }
            set
            {
                var prefix = (string.IsNullOrWhiteSpace(value))
                    ? string.Empty
                    : $"/{value.Trim().TrimStart('/').TrimEnd('/').Trim()}";

                if (this._prefix.Equals(prefix, StringComparison.CurrentCultureIgnoreCase)) return;

                this._prefix = prefix;
                this.DirectoryMapping?.Clear();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.Watcher?.Dispose();
        }

        public async override Task SendFileAsync(IHttpContext context)
        {
            await this.SendFileAsync(context, null);
        }

        public async override Task SendFileAsync(IHttpContext context, string filename)
        {
            this.PopulateDirectoryListing();

            if (this.DirectoryMapping.ContainsKey(context.Request.Endpoint))
            {
                var filepath = this.DirectoryMapping[context.Request.Endpoint];
                context.Response.StatusCode = HttpStatusCode.Ok;

                var lastModified = File.GetLastWriteTimeUtc(filepath).ToString("R");
                context.Response.AddHeader("Last-Modified", lastModified);

                if (context.Request.Headers.AllKeys.Contains("If-Modified-Since") && context.Request.Headers["If-Modified-Since"].Equals(lastModified))
                {
                    await context.Response.SendResponseAsync(HttpStatusCode.NotModified).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(filename))
                    context.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{filename}\"");

                context.Response.ContentType = ContentType.FindKey(Path.GetExtension(filepath).TrimStart('.').ToLower());

                using (FileStream stream = new(filepath, FileMode.Open))
                {
                    await context.Response.SendResponseAsync(stream);
                }
            }

            // File not found, but should have been based on the path info
            else if (!string.IsNullOrEmpty(this.Prefix) && context.Request.Endpoint.StartsWith(this.Prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                context.Response.StatusCode = HttpStatusCode.NotFound;
            }
        }
    }

    public abstract class ContentFolderBase : IContentFolder
    {
        protected FileSystemWatcher Watcher;
        public static Func<IHttpContext, Task> DefaultFileNotFoundHandler { get; set; } = async (context) =>
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            var content = $"File Not Found: {context.Request.Endpoint}";
            await context.Response.SendResponseAsync(content);
        };

        public static string DefaultIndexFileName { get; } = "index.html";
        public ConcurrentDictionary<string, string> DirectoryMapping { get; protected set; }
        public Func<IHttpContext, Task> FileNotFoundHandler { get; set; } = DefaultFileNotFoundHandler;
        public abstract string FolderPath { get; set; }
        public abstract string IndexFileName { get; set; }

        public abstract string Prefix { get; set; }
        public virtual void AddToDirectoryListing(string fullPath)
        {
            this.DirectoryMapping ??= new();

            this.DirectoryMapping[this.CreateDirectoryListingKey(fullPath)] = fullPath;

            if (fullPath.EndsWith($"{Path.DirectorySeparatorChar}{this.IndexFileName}", StringComparison.CurrentCultureIgnoreCase))
                this.DirectoryMapping[this.CreateDirectoryListingKey(fullPath.Replace($"{Path.DirectorySeparatorChar}{this.IndexFileName}", ""))] = fullPath;
        }

        public virtual string CreateDirectoryListingKey(string item)
        {
            return $"{this.Prefix}{item.Replace(this.FolderPath, string.Empty).Replace(@"\", "/")}";
        }

        public IList<string> DirectoryListing()
        {
            return this.DirectoryMapping.Values.ToList();
        }
        public virtual void PopulateDirectoryListing()
        {
            if (this.DirectoryMapping?.Count > 0) return;

            Directory.GetFiles(this.FolderPath, "*", SearchOption.AllDirectories)
                .ToList()
                .ForEach(this.AddToDirectoryListing);
        }

        public virtual void RemoveFromDirectoryListing(string fullPath)
        {
            if (this.DirectoryMapping == null) return;

            this.DirectoryMapping.Where(x => x.Value == fullPath)
                .ToList()
                .ForEach(pair => this.DirectoryMapping.TryRemove(pair.Key, out string key));
        }

        public virtual void RenameInDirectoryListing(string oldFullPath, string newFullPath)
        {
            this.RemoveFromDirectoryListing(oldFullPath);
            this.AddToDirectoryListing(newFullPath);
        }

        public abstract Task SendFileAsync(IHttpContext context);

        public abstract Task SendFileAsync(IHttpContext context, string filename);
    }
}