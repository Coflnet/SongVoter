using System.Linq;
using HashidsNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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

        public long UserId(ControllerBase controller)
        {
            return FromHash(controller.User.Claims.Where(c => c.Type == "uid").First().Value);
        }
    }

}