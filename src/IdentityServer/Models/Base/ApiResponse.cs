using System;
using System.Collections.Generic;
using System.Linq;

namespace IdentityServer.Models.Base
{
    public class ApiResponse<TData>
    {
        public ApiResponse()
        {
            Errors = new List<string>();
        }

        public TData Data { get; set; }
        public bool HasError => Errors.Any();
        public string Message { get; set; }
        public List<string> Errors { get; set; }
        public int Total { get; set; }
        
        public static ApiResponse<TData> Fail(List<string> errors) => new ApiResponse<TData> { Errors = errors };
        public static ApiResponse<TData> Fail(string error) => new ApiResponse<TData> { Errors = new List<string> { error } };
        public static ApiResponse<TData> Success(TData data, string message = null) => new ApiResponse<TData> { Data = data, Message = message };
    }
}

