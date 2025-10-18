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
        public List<string> Errors { get; set; }
        public int Total { get; set; }
    }
}

