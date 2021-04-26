using System.Linq;
using HashidsNet;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.SongVoter.Service
{
    public class IDService
    {
        Hashids hasher = new Hashids(SimplerConfig.SConfig.Instance["hashids:salt"]);

        public static IDService Instance = new IDService();

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