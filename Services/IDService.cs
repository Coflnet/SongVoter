using System.Linq;
using HashidsNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Coflnet.SongVoter.Service
{
    public class IDService
    {
        private IConfiguration config;
        Hashids hasher;

        public IDService(IConfiguration config)
        {
            this.config = config;
            hasher = new Hashids(config["hashids:salt"], 6);
        }

        public int FromHash(string hash)
        {
            if(hash == null)
                return -1;
            var values = FromHashMany(hash);
            return values.Length == 1 && values[0] <= int.MaxValue ? (int)values[0] : -1;
        }
        public long[] FromHashMany(string hash)
        {
            return hasher.DecodeLong(hash);
        }


        public string ToHash(params long[] numbers)
        {
            return hasher.EncodeLong(numbers);
        }

        public int UserId(ControllerBase controller)
        {
            return (int)FromHash(controller.User.Claims.Where(c => c.Type == "uid").FirstOrDefault()?.Value);
        }
    }

}