namespace SureStacks.O365Logs2LA
{
    public interface IManagedIdentityTokenService
    {
        Task<string> GetToken(string resource = "manage.office.com");
    }
}