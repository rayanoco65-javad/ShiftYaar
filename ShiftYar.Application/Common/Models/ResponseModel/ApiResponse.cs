using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShiftYar.Application.Common.Models.ResponseModel
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; } // موفقیت یا عدم موفقیت عملیات
        public string? Message { get; set; } // پیام
        public T? Data { get; set; } // داده

        public static ApiResponse<T> Success(T data, string? message = null)
            => new ApiResponse<T> { IsSuccess = true, Data = data, Message = message };

        public static ApiResponse<T> Fail(string message)
            => new ApiResponse<T> { IsSuccess = false, Message = message };
    }
}
