using System;

namespace GesEventSpike.Matching
{
    public class DemographicsDocument
    {
        public readonly string DocumentId;
        public readonly string RecordId;
        public readonly string SessionId;
        public readonly string FirstName;
        public readonly string LastName;
        public readonly DateTime BirthDate;

        public DemographicsDocument(string documentId, string recordId, string sessionId, string firstName, string lastName, DateTime birthDate)
        {
            DocumentId = documentId;
            RecordId = recordId;
            SessionId = sessionId;
            FirstName = firstName;
            LastName = lastName;
            BirthDate = birthDate;
        }
    }
}