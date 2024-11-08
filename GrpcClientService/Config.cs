using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrpcClientService
{
    public class GrpcConfig
    {
        public required string ServerAddress{ get; set; }
    }
    public class Config
    { 
        public required GrpcConfig Grpc { get; set; }
    }
}
