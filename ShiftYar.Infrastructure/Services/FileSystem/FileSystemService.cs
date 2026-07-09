using Microsoft.AspNetCore.Hosting;
using ShiftYar.Application.Interfaces.IFileSystem;

namespace ShiftYar.Infrastructure.Services.FileSystem
{
    public class FileSystemService : IFileSystemService
    {
        private readonly IWebHostEnvironment _env;

        public FileSystemService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> ReadAllTextAsync(string path)
        {
            return await System.IO.File.ReadAllTextAsync(path);
        }

        public bool FileExists(string path)
        {
            return System.IO.File.Exists(path);
        }

        public string CombinePath(params string[] paths)
        {
            return Path.Combine(_env.ContentRootPath, Path.Combine(paths));
        }
    }
}
