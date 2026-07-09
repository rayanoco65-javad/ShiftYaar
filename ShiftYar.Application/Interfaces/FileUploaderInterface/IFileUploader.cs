using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Interfaces.FileUploaderInterface
{
    public interface IFileUploader
    {
        string UploadFile(string fileBase64, string subfolder = "");
        string UpdateFile(string newFileBase64, string previousFilePath, string subfolder = "");
        void DeleteFileFomServer(string filePath);
    }
}
