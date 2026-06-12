namespace LFM.Domain.Write.Commands.Auth
{
    public class LoginUserCommand : BaseCommand
    {
        public string Email { get; set; }
        
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}