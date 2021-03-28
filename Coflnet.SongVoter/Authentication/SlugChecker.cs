using Coflnet.SongVoter.Middleware;

namespace Coflnet.SongVoter
{
    public static class ScopeChecker
    {
        public static void RequireScope(this System.Security.Claims.ClaimsPrincipal user, Scope scope)
        {
            if(!user.HasClaim(claim=>claim.Type == scope))
                throw new ApiException(System.Net.HttpStatusCode.Forbidden,$"Access token doesn't have required scope `{scope.ToString()}`. You may need to authenticate");
        }
    }

    public class Scope
    {
        public static readonly Scope Song = new Scope("song");
        public string Slug {get;}

        private Scope(string slug)
        {
            Slug = slug;
        }

        

        public static implicit operator string(Scope s) => s.Slug;

        public override bool Equals(object obj)
        {
            return Slug.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Slug.GetHashCode();
        }

        public override string ToString()
        {
            return Slug.ToString();
        }
    }
}