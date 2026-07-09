using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.IFileSystem
{
    public interface IFileSystemService
    {
        Task<string> ReadAllTextAsync(string path);
        bool FileExists(string path);
        string CombinePath(params string[] paths);
    }
}
