using HashidsNet;

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


        public string ToHash(long number)
        {
            return hasher.EncodeLong(number);
        }
    }

}