using System;

namespace Lfm.Domain.Manager.Services.ActivityLogs
{
    public class ActivityLogTypesEnum
    {
        public static readonly ActivityLogTypesEnum AddMentorSubject = new ActivityLogTypesEnum("REVR", "Взяти запит на перегляд", 1);
        public static readonly ActivityLogTypesEnum EditMentorProfile = new ActivityLogTypesEnum("APPR", "Підтвердження запиту", 2);
        public static readonly ActivityLogTypesEnum EditMentorSubject = new ActivityLogTypesEnum("REJR", "Відхилення запиту", 3);

        private ActivityLogTypesEnum(string code, string description, int id)
        {
            Code = code;
            Description = description;
            Id = id;
        }

        public readonly int Id;

        public string Code { get; }
        
        public string Description { get; }
        
        public override string ToString()
        {
            return Code;
        }

        public override bool Equals(object? obj)
        {
            return obj is ActivityLogTypesEnum op && Code == op.Code;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Code);
        }
    }
}