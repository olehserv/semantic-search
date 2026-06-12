namespace LFM.Domain.Write.Commands.Auth
{
    public class RegisterStudentCommand : BaseCommand
    {
        public string Name { get; set; }
        
        public string Email { get; set; }

        public string PhoneNumber { get; set; }
        
        public string Password { get; set; }
    }
}