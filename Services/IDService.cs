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
            hasher = new Hashids(config["hashids:salt"]);
        }

        public long FromHash(string hash)
        {
            return hasher.DecodeLong(hash)[0];
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
            System.Console.WriteLine("claims:\n--------------------\n" + JsonConvert.SerializeObject(controller.User.Claims.Select(c=>c.Value)));
            return (int)FromHash(controller.User.Claims.Where(c => c.Type == "uid").First().Value);
        }
    }

}