using System;

namespace LFM.Domain.Write.ToDo
{
    public readonly struct ToDoOperationsEnum
    {
        public static readonly ToDoOperationsEnum AddMentorSubject = new ToDoOperationsEnum("AMS", "Add Mentor Subject", 1);
        public static readonly ToDoOperationsEnum EditMentorProfile = new ToDoOperationsEnum("EMP", "Edit Mentor Profile", 2);
        public static readonly ToDoOperationsEnum EditMentorSubject = new ToDoOperationsEnum("EMS", "Edit Mentor Subject", 3);
        public static readonly ToDoOperationsEnum CreateLfmRequest = new ToDoOperationsEnum("CLFMR", "Create Looking For Mentor Request", 4);

        private ToDoOperationsEnum(string code, string description, int id)
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
            return obj is ToDoOperationsEnum op && Code == op.Code;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Code);
        }
    }
}